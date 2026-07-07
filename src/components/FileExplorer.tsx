import {
  useEffect,
  useMemo,
  useRef,
  useState,
  type DragEvent,
  type KeyboardEvent,
  type PointerEvent,
} from "react";
import { tagStyleFor, useLumina, useT } from "../state/store";
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
import { ConfirmDialog, Popover, useOverlay } from "./overlays";
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

export function FileExplorer() {
  const t = useT();
  const selectedLocationId = useLumina((s) => s.selectedLocationId);
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
  const gridRef = useRef<HTMLDivElement | null>(null);
  const searchRef = useRef<HTMLInputElement | null>(null);

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

  // App-level shortcuts (skipped while typing in a field).
  useEffect(() => {
    const onKeyDown = (event: globalThis.KeyboardEvent) => {
      const target = event.target as HTMLElement;
      const typing = target.tagName === "INPUT" || target.tagName === "TEXTAREA";
      const state = useLumina.getState();
      if ((event.ctrlKey && (event.key === "f" || event.key === "e")) ) {
        event.preventDefault();
        searchRef.current?.focus();
        searchRef.current?.select();
        return;
      }
      if (typing) return;
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

  const moveFocus = (delta: number, extend: boolean) => {
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
    else state.selectOnly(path);
    gridRef.current
      ?.querySelector(`[data-path="${CSS.escape(path)}"]`)
      ?.scrollIntoView({ block: "nearest" });
  };

  const onGridKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    if ((event.target as HTMLElement).tagName === "INPUT") return;
    const state = useLumina.getState();
    switch (event.key) {
      case "ArrowLeft":
        event.preventDefault();
        moveFocus(-1, event.shiftKey);
        break;
      case "ArrowRight":
        event.preventDefault();
        moveFocus(1, event.shiftKey);
        break;
      case "ArrowUp":
        if (event.altKey) break;
        event.preventDefault();
        moveFocus(-columnsOf(), event.shiftKey);
        break;
      case "ArrowDown":
        event.preventDefault();
        moveFocus(columnsOf(), event.shiftKey);
        break;
      case "Home":
        event.preventDefault();
        moveFocus(-state.files.length, event.shiftKey);
        break;
      case "End":
        event.preventDefault();
        moveFocus(state.files.length, event.shiftKey);
        break;
      case "Enter": {
        const focused = state.files.find((f) => f.path === state.focusedPath);
        if (focused) void state.openFile(focused);
        break;
      }
      case "F2":
        if (state.focusedPath) state.beginRename(state.focusedPath);
        break;
      case "Delete":
        if (state.selectedPaths.size > 0) setConfirmDelete(true);
        break;
      case "Escape":
        state.clearSelection();
        break;
      case "a":
        if (event.ctrlKey) {
          event.preventDefault();
          state.selectAll();
        }
        break;
    }
  };

  const onGridPointerDown = (event: PointerEvent<HTMLDivElement>) => {
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
      <ExplorerToolbar searchRef={searchRef} onRequestDelete={() => setConfirmDelete(true)} />
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
        style={{
          // Fixed tracks like the WinUI grid: intrinsic row sizing of
          // column-flex cards is unreliable in Chromium.
          gridTemplateColumns: `repeat(auto-fill, min(${cardWidth}px, 100%))`,
          gridAutoRows: `${cardHeightForWidth(cardWidth)}px`,
        }}
        tabIndex={0}
        role="grid"
        onKeyDown={onGridKeyDown}
        onPointerDown={onGridPointerDown}
      >
        {files.map((file) => (
          <FileCard key={file.path} file={file} onRequestDelete={() => setConfirmDelete(true)} />
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
          message={t(trashDelete ? "DeleteConfirmTrash" : "DeleteConfirm", selectedCount)}
          confirmLabel={t("Delete")}
          cancelLabel={t("Cancel")}
          danger
          onConfirm={() => {
            setConfirmDelete(false);
            void useLumina.getState().deleteSelected();
          }}
          onCancel={() => setConfirmDelete(false)}
        />
      )}
    </div>
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
          placeholder={locationName ? t("SearchInFolder", locationName) : t("Search")}
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
  onRequestDelete,
}: {
  file: FileItem;
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
          <RenameInput file={file} />
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

function RenameInput({ file }: { file: FileItem }) {
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
    void useLumina.getState().commitRename(file.path, value);
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
        }
      }}
      onBlur={(e) => commit(e.currentTarget.value)}
      onPointerDown={(e) => e.stopPropagation()}
      onDoubleClick={(e) => e.stopPropagation()}
    />
  );
}
