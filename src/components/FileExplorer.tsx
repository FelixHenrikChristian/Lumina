import {
  useEffect,
  useMemo,
  useRef,
  useState,
  type CSSProperties,
  type DragEvent,
  type KeyboardEvent,
  type PointerEvent,
} from "react";
import { tagStyleFor, useLumina, useT, type FileTransferConflict } from "../state/store";
import type { TransferConflictAction, TransferConflictResolutions } from "../fs/types";
import type { FileItem, FileSortField, LocationPathSegment } from "../core/models";
import {
  CARD_WIDTH_ZOOM_LEVELS,
  cardHeightForWidth,
  cssColorFor,
  glyphKindFor,
} from "../core/models";
import { formatModified, formatSize } from "../core/format";
import { getDisplayNameWithoutExtension } from "../core/tagParser";
import { useLazyThumbnail } from "./useThumbnail";
import {
  endTagDrag,
  beginTagDrag,
  getActiveTagDrag,
  hasTagDrag,
  readTagDrag,
} from "./tagDrag";
import { ConfirmDialog, GlassDialog, Popover, useOverlay } from "./overlays";
import { LiquidGlassButton } from "./LiquidGlassButton";
import {
  BackIcon,
  CheckIcon,
  ChevronRightIcon,
  CloseIcon,
  FilterIcon,
  FolderIcon,
  ForwardIcon,
  GlyphIcon,
  MinusIcon,
  OpenIcon,
  PlusIcon,
  RefreshIcon,
  RenameIcon,
  SearchIcon,
  SortIcon,
  TrashIcon,
  UpIcon,
} from "./icons";

const SORT_FIELDS: { field: FileSortField; labelKey: string }[] = [
  { field: "name", labelKey: "SortName" },
  { field: "modified", labelKey: "SortModified" },
  { field: "type", labelKey: "SortType" },
  { field: "size", labelKey: "SortSize" },
];

interface PendingTransfer {
  readonly paths: string[];
  readonly move: boolean;
  readonly conflicts: FileTransferConflict[];
  readonly index: number;
  readonly resolutions: TransferConflictResolutions;
}

export function FileExplorer() {
  const t = useT();
  const selectedLocationId = useLumina((s) => s.selectedLocationId);
  const currentPath = useLumina((s) => s.currentPath);
  const files = useLumina((s) => s.files);
  const isBusy = useLumina((s) => s.isBusy);
  const errorMessage = useLumina((s) => s.errorMessage);
  const selectedCount = useLumina((s) => s.selectedPaths.size);
  const zoomLevelIndex = useLumina((s) => s.zoomLevelIndex);
  const trashDelete = useLumina(
    (s) => s.locations.find((l) => l.id === s.selectedLocationId)?.kind === "native",
  );
  const cardWidth = CARD_WIDTH_ZOOM_LEVELS[zoomLevelIndex];

  const [confirmDelete, setConfirmDelete] = useState(false);
  const [permanentDelete, setPermanentDelete] = useState(false);
  const [pendingTransfer, setPendingTransfer] = useState<PendingTransfer | null>(null);
  const [cutPaths, setCutPaths] = useState<Set<string>>(() => new Set());
  const gridRef = useRef<HTMLDivElement | null>(null);
  const searchRef = useRef<HTMLInputElement | null>(null);
  const clipboardRef = useRef<{ paths: string[]; cut: boolean } | null>(null);
  const clipboardWriteRef = useRef<Promise<void> | null>(null);

  // Ctrl+wheel zoom needs preventDefault, which React's passive wheel
  // listeners cannot deliver — attach natively.
  useEffect(() => {
    const grid = gridRef.current;
    if (!grid) return;
    const onWheel = (event: WheelEvent) => {
      if (!event.ctrlKey) return;
      event.preventDefault();
      useLumina.getState().zoomByWheelDelta(-Math.sign(event.deltaY));
    };
    grid.addEventListener("wheel", onWheel, { passive: false });
    return () => grid.removeEventListener("wheel", onWheel);
  }, [selectedLocationId]);

  useEffect(() => {
    let disposed = false;
    let stopWatching: () => void = () => undefined;
    void useLumina
      .getState()
      .watchCurrentDirectory(() => {
        if (!disposed) void useLumina.getState().refresh();
      })
      .then((stop) => {
        if (disposed) stop();
        else stopWatching = stop;
      })
      .catch(() => undefined);
    return () => {
      disposed = true;
      stopWatching();
    };
  }, [selectedLocationId, currentPath]);

  useEffect(() => {
    let cancelled = false;
    const syncSystemClipboard = async () => {
      const clipboard = await useLumina.getState().readSystemClipboard();
      if (cancelled || !clipboard.supported) return;
      clipboardRef.current = null;
      setCutPaths(clipboard.move ? new Set(clipboard.paths) : new Set());
    };
    const onFocus = () => void syncSystemClipboard();
    void syncSystemClipboard();
    window.addEventListener("focus", onFocus);
    return () => {
      cancelled = true;
      window.removeEventListener("focus", onFocus);
    };
  }, [selectedLocationId]);

  // App-level shortcuts (skipped while typing in a field).
  useEffect(() => {
    const onKeyDown = (event: globalThis.KeyboardEvent) => {
      const target = event.target as HTMLElement;
      const typing =
        target.tagName === "INPUT" || target.tagName === "TEXTAREA" || target.isContentEditable;
      const state = useLumina.getState();
      if (typing) return;
      if (event.ctrlKey && (event.code === "KeyF" || event.code === "KeyE")) {
        event.preventDefault();
        searchRef.current?.focus();
        searchRef.current?.select();
        return;
      }
      if (event.key === "F5") {
        event.preventDefault();
        void state.refresh();
      } else if (event.altKey && event.key === "ArrowLeft") {
        event.preventDefault();
        void state.navigateBack();
      } else if (event.altKey && event.key === "ArrowRight") {
        event.preventDefault();
        void state.navigateForward();
      } else if (event.altKey && event.key === "ArrowUp") {
        event.preventDefault();
        void state.navigateToParent();
      } else if (event.key === "Backspace") {
        event.preventDefault();
        void state.navigateBack();
      }
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, []);

  const columnsOf = (): number => {
    const grid = gridRef.current;
    if (!grid) return 1;
    const tracks = getComputedStyle(grid).gridTemplateColumns.split(" ").length;
    return Math.max(1, tracks);
  };

  const restoreGridFocus = () => {
    window.requestAnimationFrame(() => gridRef.current?.focus({ preventScroll: true }));
  };

  const moveFocus = (delta: number, extend: boolean, focusOnly: boolean) => {
    const state = useLumina.getState();
    if (state.files.length === 0) return;
    const current = state.focusedPath ?? [...state.selectedPaths][0] ?? null;
    const currentIndex = current
      ? state.files.findIndex((f) => f.path === current)
      : -1;
    const target =
      currentIndex < 0
        ? 0
        : Math.min(Math.max(currentIndex + delta, 0), state.files.length - 1);
    const path = state.files[target].path;
    if (extend) state.extendSelectionTo(path);
    else if (focusOnly) state.focusPath(path);
    else state.selectOnly(path);
    gridRef.current
      ?.querySelector(`[data-path="${CSS.escape(path)}"]`)
      ?.scrollIntoView({ block: "nearest" });
  };

  // File-operation shortcuts belong to the explorer window, not to the grid
  // element's DOM focus. A pointer-selected card is a non-focusable div, so
  // grid-only handlers silently miss Ctrl+C/X/V (and Ctrl+Z/Y) after a click.
  useEffect(() => {
    const onFileShortcut = (event: globalThis.KeyboardEvent) => {
      const target = event.target as HTMLElement;
      const typing =
        target.tagName === "INPUT" || target.tagName === "TEXTAREA" || target.isContentEditable;
      const overlayOpen = document.querySelector('[role="dialog"], [role="menu"], .lg-popover');
      if (typing || overlayOpen || !event.ctrlKey || event.altKey) return;

      const state = useLumina.getState();
      if (!state.selectedLocationId) return;
      const key = event.key.toLowerCase();
      const isLetter = (letter: string) =>
        event.code === `Key${letter.toUpperCase()}` || key === letter.toLowerCase();

      if (isLetter("a") && !event.shiftKey) {
        event.preventDefault();
        state.selectAll();
        return;
      }

      if ((isLetter("c") || isLetter("x")) && !event.shiftKey) {
        if (state.selectedPaths.size === 0) return;
        event.preventDefault();
        const cut = isLetter("x");
        const paths = [...state.selectedPaths];
        clipboardRef.current = { paths, cut };
        setCutPaths(cut ? new Set(paths) : new Set());
        clipboardWriteRef.current = state.copyPathsToSystemClipboard(paths, cut);
        return;
      }

      if (isLetter("v") && !event.shiftKey) {
        event.preventDefault();
        void (async () => {
          await clipboardWriteRef.current;
          const result = await useLumina.getState().pasteSystemClipboard();
          if (result.supported) {
            if (result.pasted) {
              clipboardRef.current = null;
              setCutPaths(new Set());
            }
            return;
          }

          const clipboard = clipboardRef.current;
          if (!clipboard) return;
          const currentState = useLumina.getState();
          try {
            const conflicts = await currentState.inspectTransfer(clipboard.paths, clipboard.cut);
            if (conflicts.length > 0) {
              setPendingTransfer({
                paths: clipboard.paths,
                move: clipboard.cut,
                conflicts,
                index: 0,
                resolutions: {},
              });
            } else {
              await currentState.transferPaths(clipboard.paths, clipboard.cut);
              if (clipboard.cut) setCutPaths(new Set());
            }
          } catch (error) {
            useLumina.setState({
              errorMessage: error instanceof Error ? error.message : String(error),
            });
          }
        })();
        return;
      }

      if (isLetter("d") && !event.shiftKey) {
        if (state.selectedPaths.size === 0) return;
        event.preventDefault();
        const nativeLocation = state.locations.find(
          (location) => location.id === state.selectedLocationId,
        )?.kind === "native";
        if (nativeLocation) void state.deleteSelected(false);
        else {
          setPermanentDelete(false);
          setConfirmDelete(true);
        }
        return;
      }

      if (isLetter("n") && event.shiftKey) {
        event.preventDefault();
        void state.createFolder();
        return;
      }

      if (isLetter("z")) {
        event.preventDefault();
        if (event.shiftKey) void state.redoLastFileOperation();
        else void state.undoLastFileOperation();
        return;
      }

      if (isLetter("y") && !event.shiftKey) {
        event.preventDefault();
        void state.redoLastFileOperation();
      }
    };

    window.addEventListener("keydown", onFileShortcut);
    return () => window.removeEventListener("keydown", onFileShortcut);
  }, []);

  const resolveTransferConflict = (action: TransferConflictAction) => {
    if (!pendingTransfer) return;
    const conflict = pendingTransfer.conflicts[pendingTransfer.index];
    const resolutions = {
      ...pendingTransfer.resolutions,
      [conflict.sourcePath.toLowerCase()]: action,
    };
    const next = pendingTransfer.index + 1;
    if (next < pendingTransfer.conflicts.length) {
      setPendingTransfer({ ...pendingTransfer, index: next, resolutions });
      return;
    }
    setPendingTransfer(null);
    void useLumina.getState().transferPaths(pendingTransfer.paths, pendingTransfer.move, resolutions);
    if (pendingTransfer.move) setCutPaths(new Set());
  };

  const requestDelete = (permanently: boolean) => {
    if (trashDelete) {
      void useLumina.getState().deleteSelected(permanently);
      return;
    }
    setPermanentDelete(permanently);
    setConfirmDelete(true);
  };

  const onGridKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    if ((event.target as HTMLElement).tagName === "INPUT") return;
    const state = useLumina.getState();
    switch (event.key) {
      case "ArrowLeft":
        event.preventDefault();
        moveFocus(-1, event.shiftKey, event.ctrlKey && !event.shiftKey);
        break;
      case "ArrowRight":
        event.preventDefault();
        moveFocus(1, event.shiftKey, event.ctrlKey && !event.shiftKey);
        break;
      case "ArrowUp":
        if (event.altKey) break;
        event.preventDefault();
        moveFocus(-columnsOf(), event.shiftKey, event.ctrlKey && !event.shiftKey);
        break;
      case "ArrowDown":
        event.preventDefault();
        moveFocus(columnsOf(), event.shiftKey, event.ctrlKey && !event.shiftKey);
        break;
      case "Home":
        event.preventDefault();
        moveFocus(-state.files.length, event.shiftKey, event.ctrlKey && !event.shiftKey);
        break;
      case "End":
        event.preventDefault();
        moveFocus(state.files.length, event.shiftKey, event.ctrlKey && !event.shiftKey);
        break;
      case "Enter": {
        if (event.altKey) break;
        const focused = state.files.find((f) => f.path === state.focusedPath);
        if (focused) {
          event.preventDefault();
          void state.openFile(focused);
        }
        break;
      }
      case "F2":
        if (state.focusedPath) {
          event.preventDefault();
          state.beginRename(state.focusedPath);
        }
        break;
      case "Delete":
        if (state.selectedPaths.size > 0) {
          event.preventDefault();
          requestDelete(event.shiftKey);
        }
        break;
      case " ":
        if (event.ctrlKey && state.focusedPath) {
          event.preventDefault();
          state.toggleSelect(state.focusedPath);
        }
        break;
      case "Escape":
        state.clearSelection();
        break;
    }
  };

  const onGridPointerDown = (event: PointerEvent<HTMLDivElement>) => {
    const target = event.target as HTMLElement;
    if (!target.closest("input, button, [contenteditable='true']")) {
      gridRef.current?.focus({ preventScroll: true });
    }
    if (event.target === event.currentTarget) {
      useLumina.getState().clearSelection();
    }
  };

  if (!selectedLocationId) {
    return (
      <div className="explorer">
        <div className="explorer-empty">
          <GlyphIcon kind="folder" size={44} className="explorer-empty-icon" />
          <h3>{t("NoLocationSelected")}</h3>
          <p>{t("NoLocationHint")}</p>
        </div>
      </div>
    );
  }

  return (
    <div className="explorer">
      <ExplorerToolbar
        searchRef={searchRef}
        onRequestDelete={() => requestDelete(false)}
      />
      <Breadcrumbs />

      {errorMessage && (
        <div className="explorer-error" role="alert">
          {errorMessage}
          <button
            type="button"
            className="row-more"
            title={t("Cancel")}
            onClick={() => useLumina.setState({ errorMessage: null })}
          >
            <CloseIcon size={13} />
          </button>
        </div>
      )}

      <div
        ref={gridRef}
        className="file-grid"
        style={
          {
            // Explorer-style distribution: tracks flex to share leftover row
            // space while the card itself stays at --card-width, centered in
            // its track (fixed rows: intrinsic row sizing of column-flex
            // cards is unreliable in Chromium).
            "--card-width": `${cardWidth}px`,
            gridTemplateColumns: `repeat(auto-fill, minmax(min(${cardWidth}px, 100%), 1fr))`,
            gridAutoRows: `${cardHeightForWidth(cardWidth)}px`,
          } as CSSProperties
        }
        tabIndex={0}
        role="grid"
        onKeyDown={onGridKeyDown}
        onPointerDown={onGridPointerDown}
      >
        {files.map((file) => (
          <FileCard
            key={file.path}
            file={file}
            isCut={cutPaths.has(file.path)}
            onRenameComplete={restoreGridFocus}
            onRequestDelete={() => requestDelete(false)}
          />
        ))}
        {!isBusy && files.length === 0 && !errorMessage && (
          <div className="explorer-empty">
            <GlyphIcon kind="folder" size={44} className="explorer-empty-icon" />
            <h3>{t("EmptyFolder")}</h3>
            <p>{t("EmptyFolderHint")}</p>
          </div>
        )}
        {isBusy && (
          <div className="explorer-loading">
            <span className="lg-spinner" />
            {t("Loading")}
          </div>
        )}
      </div>

      <footer className="explorer-status">
        <span>{t("ItemsCount", files.length)}</span>
        {selectedCount > 0 && <span>{t("SelectedCount", selectedCount)}</span>}
      </footer>

      {confirmDelete && (
        <ConfirmDialog
          title={t("Delete")}
          message={t(!permanentDelete && trashDelete ? "DeleteConfirmTrash" : "DeleteConfirm", selectedCount)}
          confirmLabel={t("Delete")}
          cancelLabel={t("Cancel")}
          danger
          onConfirm={() => {
            setConfirmDelete(false);
            void useLumina.getState().deleteSelected(permanentDelete);
          }}
          onCancel={() => setConfirmDelete(false)}
        />
      )}
      {pendingTransfer && (
        <TransferConflictDialog
          conflict={pendingTransfer.conflicts[pendingTransfer.index]}
          move={pendingTransfer.move}
          onAction={resolveTransferConflict}
          onCancel={() => setPendingTransfer(null)}
        />
      )}
    </div>
  );
}

function TransferConflictDialog({
  conflict,
  move,
  onAction,
  onCancel,
}: {
  conflict: FileTransferConflict;
  move: boolean;
  onAction(action: TransferConflictAction): void;
  onCancel(): void;
}) {
  const describe = (file: FileItem) =>
    `${file.isDirectory ? "文件夹" : formatSize(file.size)} · ${file.modified ? formatModified(file.modified) : "未知日期"}`;
  if (conflict.sameDirectory) {
    return (
      <GlassDialog title="粘贴已中断" onDismiss={onCancel} width={460}>
        <p className="lg-dialog-message">源文件夹和目标文件夹相同：{conflict.source.name}</p>
        <div className="lg-dialog-actions">
          <LiquidGlassButton onClick={onCancel}>取消</LiquidGlassButton>
          <LiquidGlassButton variant="primary" onClick={() => onAction("skip")} autoFocus>
            跳过
          </LiquidGlassButton>
        </div>
      </GlassDialog>
    );
  }
  return (
    <GlassDialog title="文件冲突" onDismiss={onCancel} width={520}>
      <p className="lg-dialog-message">目标文件夹已包含同名项目：{conflict.source.name}</p>
      <div className="file-conflict-details">
        <div><strong>源</strong><br />{describe(conflict.source)}</div>
        {conflict.destination && <div><strong>目标</strong><br />{describe(conflict.destination)}</div>}
      </div>
      <div className="lg-dialog-actions">
        <LiquidGlassButton onClick={onCancel}>取消</LiquidGlassButton>
        <LiquidGlassButton onClick={() => onAction("skip")}>跳过</LiquidGlassButton>
        <LiquidGlassButton onClick={() => onAction("keepBoth")}>保留两个</LiquidGlassButton>
        <LiquidGlassButton variant={move ? "danger" : "primary"} onClick={() => onAction("replace")} autoFocus>
          替换
        </LiquidGlassButton>
      </div>
    </GlassDialog>
  );
}

function ExplorerToolbar({
  searchRef,
  onRequestDelete,
}: {
  searchRef: React.RefObject<HTMLInputElement | null>;
  onRequestDelete(): void;
}) {
  const t = useT();
  const backStack = useLumina((s) => s.backStack);
  const forwardStack = useLumina((s) => s.forwardStack);
  const scope = useLumina((s) => s.scope);
  const currentPath = useLumina((s) => s.currentPath);
  const searchQuery = useLumina((s) => s.searchQuery);
  const zoomLevelIndex = useLumina((s) => s.zoomLevelIndex);
  const selectedCount = useLumina((s) => s.selectedPaths.size);
  const filterCount = useLumina((s) => s.selectedTagFilterIds.size);
  const locations = useLumina((s) => s.locations);
  const selectedLocationId = useLumina((s) => s.selectedLocationId);

  const [sortAnchor, setSortAnchor] = useState<HTMLElement | null>(null);
  const [filterAnchor, setFilterAnchor] = useState<HTMLElement | null>(null);
  const searchTimer = useRef<number | undefined>(undefined);

  const canGoUp = scope !== null && scope.tryGetParentPath(currentPath) !== null;
  const locationName =
    locations.find((l) => l.id === selectedLocationId)?.name ?? "";
  const searchScopeName = useMemo(() => {
    if (!scope || !currentPath) return locationName;
    try {
      const breadcrumbs = scope.getBreadcrumbs(currentPath, locationName);
      return breadcrumbs.at(-1)?.name ?? locationName;
    } catch {
      return locationName;
    }
  }, [scope, currentPath, locationName]);

  const onSearchChange = (value: string) => {
    useLumina.getState().setSearchQuery(value);
    window.clearTimeout(searchTimer.current);
    searchTimer.current = window.setTimeout(() => {
      void useLumina.getState().runSearch();
    }, 300);
  };

  return (
    <div className="explorer-toolbar">
      <div className="toolbar-group">
        <button
          type="button"
          className="nav-button"
          title={t("Back")}
          disabled={backStack.length === 0}
          onClick={() => void useLumina.getState().navigateBack()}
        >
          <BackIcon />
        </button>
        <button
          type="button"
          className="nav-button"
          title={t("Forward")}
          disabled={forwardStack.length === 0}
          onClick={() => void useLumina.getState().navigateForward()}
        >
          <ForwardIcon />
        </button>
        <button
          type="button"
          className="nav-button"
          title={t("UpOneLevel")}
          disabled={!canGoUp}
          onClick={() => void useLumina.getState().navigateToParent()}
        >
          <UpIcon />
        </button>
        <button
          type="button"
          className="nav-button"
          title={t("Refresh")}
          onClick={() => void useLumina.getState().refresh()}
        >
          <RefreshIcon />
        </button>
      </div>

      <div className="explorer-search">
        <SearchIcon size={14} />
        <input
          ref={searchRef}
          type="search"
          placeholder={searchScopeName ? t("SearchInFolder", searchScopeName) : t("Search")}
          value={searchQuery}
          onChange={(e) => onSearchChange(e.currentTarget.value)}
          onKeyDown={(e) => {
            if (e.key === "Enter") {
              window.clearTimeout(searchTimer.current);
              void useLumina.getState().runSearch();
            } else if (e.key === "Escape" && searchQuery) {
              e.stopPropagation();
              onSearchChange("");
            }
          }}
        />
        {searchQuery && (
          <button
            type="button"
            className="row-more"
            title={t("Cancel")}
            onClick={() => onSearchChange("")}
          >
            <CloseIcon size={12} />
          </button>
        )}
      </div>

      <div className="toolbar-group">
        <button
          type="button"
          className="nav-button"
          title={t("NewFolder")}
          onClick={() => void useLumina.getState().createFolder()}
        >
          <PlusIcon />
        </button>
        <button
          type="button"
          className="nav-button"
          title={t("Delete")}
          disabled={selectedCount === 0}
          onClick={onRequestDelete}
        >
          <TrashIcon />
        </button>
        <button
          type="button"
          className="nav-button"
          title={t("SortBy")}
          onClick={(e) => setSortAnchor(sortAnchor ? null : e.currentTarget)}
        >
          <SortIcon />
        </button>
        <button
          type="button"
          className={`nav-button${filterCount > 0 ? " is-active" : ""}`}
          title={t("Tags")}
          onClick={(e) => setFilterAnchor(filterAnchor ? null : e.currentTarget)}
        >
          <FilterIcon />
          {filterCount > 0 && <span className="nav-badge">{filterCount}</span>}
        </button>
        <button
          type="button"
          className="nav-button"
          title="−"
          disabled={zoomLevelIndex === 0}
          onClick={() => useLumina.getState().zoomByWheelDelta(-1)}
        >
          <MinusIcon />
        </button>
        <button
          type="button"
          className="nav-button"
          title="+"
          disabled={zoomLevelIndex === CARD_WIDTH_ZOOM_LEVELS.length - 1}
          onClick={() => useLumina.getState().zoomByWheelDelta(1)}
        >
          <PlusIcon />
        </button>
      </div>

      {sortAnchor && <SortPopover anchor={sortAnchor} onClose={() => setSortAnchor(null)} />}
      {filterAnchor && (
        <TagFilterPopover anchor={filterAnchor} onClose={() => setFilterAnchor(null)} />
      )}
    </div>
  );
}

function SortPopover({ anchor, onClose }: { anchor: HTMLElement; onClose(): void }) {
  const t = useT();
  const sort = useLumina((s) => s.sort);
  const setSort = useLumina((s) => s.setSort);
  return (
    <Popover anchor={anchor} onClose={onClose} align="end">
      <div className="lg-menu-static">
        {SORT_FIELDS.map(({ field, labelKey }) => (
          <button
            key={field}
            type="button"
            className="lg-menu-item"
            onClick={() => void setSort(field, undefined)}
          >
            <span className="lg-menu-icon">{sort.field === field && <CheckIcon />}</span>
            <span>{t(labelKey)}</span>
          </button>
        ))}
        <div className="lg-menu-separator" />
        {(["ascending", "descending"] as const).map((direction) => (
          <button
            key={direction}
            type="button"
            className="lg-menu-item"
            onClick={() => void setSort(undefined, direction)}
          >
            <span className="lg-menu-icon">{sort.direction === direction && <CheckIcon />}</span>
            <span>{t(direction === "ascending" ? "SortAscending" : "SortDescending")}</span>
          </button>
        ))}
      </div>
    </Popover>
  );
}

function TagFilterPopover({ anchor, onClose }: { anchor: HTMLElement; onClose(): void }) {
  const t = useT();
  const tagGroups = useLumina((s) => s.tagGroups);
  const tagStyles = useLumina((s) => s.tagStyles);
  const selectedIds = useLumina((s) => s.selectedTagFilterIds);
  const toggleTagFilter = useLumina((s) => s.toggleTagFilter);
  const clearTagFilters = useLumina((s) => s.clearTagFilters);
  const hasTags = tagGroups.some((g) => g.tags.length > 0);

  return (
    <Popover anchor={anchor} onClose={onClose} align="end">
      <div className="tag-filter">
        {!hasTags && <p className="sidebar-empty">{t("TagFilterEmpty")}</p>}
        {tagGroups.map(
          (group) =>
            group.tags.length > 0 && (
              <div key={group.id} className="tag-filter-group">
                <span className="tag-filter-title">{group.name}</span>
                <div className="tag-chips">
                  {group.tags.map((tag) => {
                    const style = tagStyleFor(tagStyles, tag.name);
                    const active = selectedIds.has(tag.id);
                    return (
                      <button
                        key={tag.id}
                        type="button"
                        className={`tag-chip${active ? " is-filtered" : ""}`}
                        style={{ background: cssColorFor(style.color), color: style.textColor }}
                        onClick={() => void toggleTagFilter(tag.id)}
                      >
                        {active && <CheckIcon size={11} />}
                        {tag.name}
                      </button>
                    );
                  })}
                </div>
              </div>
            ),
        )}
        {selectedIds.size > 0 && (
          <button type="button" className="lg-chip" onClick={() => void clearTagFilters()}>
            <CloseIcon size={11} />
            {t("ClearTagFilters")}
          </button>
        )}
      </div>
    </Popover>
  );
}

function Breadcrumbs() {
  const scope = useLumina((s) => s.scope);
  const currentPath = useLumina((s) => s.currentPath);
  const locations = useLumina((s) => s.locations);
  const selectedLocationId = useLumina((s) => s.selectedLocationId);

  const segments = useMemo<LocationPathSegment[]>(() => {
    if (!scope || !currentPath) return [];
    const name = locations.find((l) => l.id === selectedLocationId)?.name ?? "";
    try {
      return scope.getBreadcrumbs(currentPath, name);
    } catch {
      return [];
    }
  }, [scope, currentPath, locations, selectedLocationId]);

  if (segments.length === 0) return null;
  return (
    <nav className="breadcrumbs" aria-label="breadcrumbs">
      {segments.map((segment, index) => (
        <span key={segment.path} className="breadcrumb-item-wrap">
          {index > 0 && <ChevronRightIcon size={12} className="breadcrumb-sep" />}
          <button
            type="button"
            className={`breadcrumb-item${index === segments.length - 1 ? " is-current" : ""}`}
            onClick={() => void useLumina.getState().openDirectory(segment.path)}
          >
            {segment.name}
          </button>
        </span>
      ))}
    </nav>
  );
}

function FileCard({
  file,
  isCut,
  onRenameComplete,
  onRequestDelete,
}: {
  file: FileItem;
  isCut: boolean;
  onRenameComplete(): void;
  onRequestDelete(): void;
}) {
  const t = useT();
  const selected = useLumina((s) => s.selectedPaths.has(file.path));
  const focused = useLumina((s) => s.focusedPath === file.path);
  const renaming = useLumina((s) => s.renamingPath === file.path);
  const hideExtension = useLumina((s) => s.settings.hideFileExtension);
  const showParent = useLumina((s) => s.settings.showParentFolderInRecursiveSearch);
  const isNativeLocation = useLumina(
    (s) => s.locations.find((l) => l.id === s.selectedLocationId)?.kind === "native",
  );
  const { openMenu } = useOverlay();

  const [thumbRef, thumbUrl] = useLazyThumbnail(file);
  const [dropIndex, setDropIndex] = useState<number | null>(null);
  const chipsRef = useRef<HTMLDivElement | null>(null);

  const displayName =
    !file.isDirectory && hideExtension
      ? getDisplayNameWithoutExtension(file.name)
      : file.displayName;

  const select = (event: { ctrlKey: boolean; metaKey: boolean; shiftKey: boolean }) => {
    const state = useLumina.getState();
    if (event.shiftKey) state.extendSelectionTo(file.path);
    else if (event.ctrlKey || event.metaKey) state.toggleSelect(file.path);
    else state.selectOnly(file.path);
  };

  const cardMenu = () => [
    {
      key: "open",
      label: t("Open"),
      icon: <OpenIcon />,
      onSelect: () => void useLumina.getState().openFile(file),
    },
    ...(isNativeLocation
      ? [
          {
            key: "reveal",
            label: t("ShowInExplorer"),
            icon: <FolderIcon />,
            onSelect: () => void useLumina.getState().revealFile(file),
          },
        ]
      : []),
    {
      key: "rename",
      label: t("Rename"),
      icon: <RenameIcon />,
      onSelect: () => useLumina.getState().beginRename(file.path),
    },
    {
      key: "delete",
      label: t("Delete"),
      icon: <TrashIcon />,
      danger: true,
      separatorAbove: true,
      onSelect: onRequestDelete,
    },
  ];

  const insertionIndexFrom = (event: DragEvent): number => {
    const chips = chipsRef.current;
    if (!chips) return file.tags.length;
    let index = 0;
    for (const child of chips.querySelectorAll<HTMLElement>("[data-chip-index]")) {
      const rect = child.getBoundingClientRect();
      const after =
        event.clientY > rect.bottom ||
        (event.clientY >= rect.top && event.clientX > rect.left + rect.width / 2);
      if (after) index = Number(child.dataset.chipIndex) + 1;
    }
    return index;
  };

  const onDragOver = (event: DragEvent<HTMLDivElement>) => {
    if (file.isDirectory || !hasTagDrag(event.dataTransfer)) return;
    if (getActiveTagDrag()?.sourcePath === file.path) return;
    event.preventDefault();
    event.dataTransfer.dropEffect = "copy";
    setDropIndex(insertionIndexFrom(event));
  };

  const onDrop = (event: DragEvent<HTMLDivElement>) => {
    setDropIndex(null);
    const payload = readTagDrag(event.dataTransfer);
    if (!payload || file.isDirectory) return;
    event.preventDefault();
    const state = useLumina.getState();
    const index = insertionIndexFrom(event);
    void (async () => {
      // Chips dragged off another card move rather than copy.
      if (payload.sourcePath && payload.sourcePath !== file.path) {
        const source = state.files.find((f) => f.path === payload.sourcePath);
        if (source) await state.removeTagFromFile(source, payload.name);
      }
      const current = useLumina
        .getState()
        .files.find((f) => f.path === file.path) ?? file;
      await state.insertTagIntoFile(current, payload.name, index);
    })();
  };

  const chips: (string | { preview: true })[] = [...file.tags];
  if (dropIndex !== null) {
    chips.splice(Math.min(dropIndex, chips.length), 0, { preview: true });
  }

  return (
    <div
      data-path={file.path}
      className={[
        "file-card",
        isCut ? "is-cut" : "",
        selected ? "is-selected" : "",
        focused ? "is-focused" : "",
        dropIndex !== null ? "is-drop-target" : "",
      ]
        .filter(Boolean)
        .join(" ")}
      role="gridcell"
      aria-selected={selected}
      onPointerDown={(e) => {
        if (e.button === 0) select(e);
      }}
      onDoubleClick={() => void useLumina.getState().openFile(file)}
      onContextMenu={(e) => {
        e.preventDefault();
        if (!selected) select(e);
        openMenu(e.clientX, e.clientY, cardMenu());
      }}
      onDragOver={onDragOver}
      onDragLeave={(e) => {
        if (!e.currentTarget.contains(e.relatedTarget as Node)) setDropIndex(null);
      }}
      onDrop={onDrop}
    >
      <div className="file-preview" ref={thumbRef}>
        {thumbUrl ? (
          <img src={thumbUrl} alt="" draggable={false} loading="lazy" />
        ) : (
          <GlyphIcon kind={glyphKindFor(file)} size={40} className="file-glyph" />
        )}
        {(file.tags.length > 0 || dropIndex !== null) && (
          <div className="file-tags" ref={chipsRef}>
            {chips.map((chip, index) =>
              typeof chip === "string" ? (
                <FileTagChip key={`${chip}-${index}`} file={file} name={chip} index={index} />
              ) : (
                <span key="preview" className="tag-chip is-preview">
                  {getActiveTagDrag()?.name ?? t("DropTagHint")}
                </span>
              ),
            )}
          </div>
        )}
      </div>
      <div className="file-info">
        {renaming ? (
          <RenameInput file={file} onComplete={onRenameComplete} />
        ) : (
          <>
            <span className="file-name" title={file.name}>
              {displayName}
            </span>
            <span className="file-meta">
              {showParent && file.relativePath && file.relativePath !== "." && (
                <span className="file-parent" title={file.relativePath}>
                  {file.relativePath}
                </span>
              )}
              {file.isDirectory ? t("Folder") : formatSize(file.size)}
              {file.modified !== null && ` · ${formatModified(file.modified)}`}
            </span>
          </>
        )}
      </div>
    </div>
  );
}

function FileTagChip({ file, name, index }: { file: FileItem; name: string; index: number }) {
  const t = useT();
  const tagStyles = useLumina((s) => s.tagStyles);
  const { openMenu } = useOverlay();
  const style = tagStyleFor(tagStyles, name);
  return (
    <span
      data-chip-index={index}
      className="tag-chip file-tag-chip"
      style={{ background: cssColorFor(style.color), color: style.textColor }}
      draggable
      title={name}
      onPointerDown={(e) => e.stopPropagation()}
      onDragStart={(e) => {
        e.stopPropagation();
        beginTagDrag(e.dataTransfer, { name, sourcePath: file.path });
      }}
      onDragEnd={endTagDrag}
      onContextMenu={(e) => {
        e.preventDefault();
        e.stopPropagation();
        openMenu(e.clientX, e.clientY, [
          {
            key: "remove",
            label: t("RemoveTagFromFile"),
            icon: <CloseIcon />,
            danger: true,
            onSelect: () => void useLumina.getState().removeTagFromFile(file, name),
          },
        ]);
      }}
    >
      {name}
    </span>
  );
}

function RenameInput({ file, onComplete }: { file: FileItem; onComplete(): void }) {
  const inputRef = useRef<HTMLInputElement | null>(null);
  const committed = useRef(false);

  useEffect(() => {
    const input = inputRef.current;
    if (!input) return;
    input.focus();
    // Pre-select the display name without extension, keeping any leading
    // "[tags] " prefix and the extension out of the initial selection.
    const displayStart = file.name.length - file.displayName.length;
    const base = getDisplayNameWithoutExtension(file.name);
    input.setSelectionRange(displayStart, displayStart + base.length);
  }, [file.name, file.displayName]);

  const commit = (value: string) => {
    if (committed.current) return;
    committed.current = true;
    void useLumina.getState().commitRename(file.path, value).finally(onComplete);
  };

  return (
    <input
      ref={inputRef}
      className="lg-input file-rename"
      defaultValue={file.name}
      onKeyDown={(e) => {
        e.stopPropagation();
        if (e.key === "Enter") commit(e.currentTarget.value);
        else if (e.key === "Escape") {
          committed.current = true;
          useLumina.getState().cancelRename();
          onComplete();
        }
      }}
      onBlur={(e) => commit(e.currentTarget.value)}
      onPointerDown={(e) => e.stopPropagation()}
      onDoubleClick={(e) => e.stopPropagation()}
    />
  );
}
