import { create } from "zustand";
import type {
  DisplaySettings,
  FileItem,
  FileSortDirection,
  FileSortField,
  FileSortOptions,
  Location,
  SidebarView,
  Tag,
  TagGroup,
} from "../core/models";
import {
  CARD_WIDTH_ZOOM_LEVELS,
  DEFAULT_FILE_SORT_OPTIONS,
} from "../core/models";
import { LocationPathScope, isSamePath } from "../core/paths";
import { sortFileItems } from "../core/sorting";
import { resolveLanguage, translate } from "../core/localization";
import {
  loadAppState,
  loadDisplaySettings,
  loadLocations,
  loadTagGroups,
  saveAppState,
  saveDisplaySettings,
  saveLocations,
  saveTagGroups,
} from "../core/stores";
import type { FileBrowserService } from "../fs/types";
import { MemoryFileBrowser } from "../fs/memoryFs";
import {
  FsaFileBrowser,
  deleteLocationHandle,
  ensurePermission,
  loadLocationHandle,
} from "../fs/fsaFs";
import { ElectronFileBrowser } from "../fs/electronFs";
import { nativeApi } from "../fs/electronApi";

// Browser adapters are kept outside the store: the demo tree must survive
// re-renders and FSA adapters wrap non-serializable handles.
const browserCache = new Map<string, FileBrowserService>();

async function browserFor(location: Location): Promise<FileBrowserService> {
  const cached = browserCache.get(location.id);
  if (cached) return cached;
  let browser: FileBrowserService;
  if (location.kind === "demo") {
    browser = new MemoryFileBrowser(location.path);
  } else if (location.kind === "native") {
    if (!location.nativePath) {
      throw new Error("The saved folder path is missing. Remove and re-add this location.");
    }
    // Re-arm the main process allowlist after a restart.
    await nativeApi().registerRoot(location.nativePath);
    browser = new ElectronFileBrowser(location.path, location.nativePath);
  } else {
    const handle = await loadLocationHandle(location.id);
    if (!handle) throw new Error("The saved folder handle is missing. Remove and re-add this location.");
    if (!(await ensurePermission(handle))) {
      throw new Error("Folder access was not granted.");
    }
    browser = new FsaFileBrowser(location.path, handle);
  }
  browserCache.set(location.id, browser);
  return browser;
}

export function registerFsaBrowser(location: Location, handle: FileSystemDirectoryHandle): void {
  browserCache.set(location.id, new FsaFileBrowser(location.path, handle));
}

export interface TagStyle {
  readonly color: string;
  readonly textColor: string;
  readonly known: boolean;
}

export const FALLBACK_TAG_STYLE: TagStyle = {
  color: "#14808080",
  textColor: "#c8c8c8",
  known: false,
};

interface LuminaState {
  // settings + i18n
  settings: DisplaySettings;
  language: "en-US" | "zh-Hans";
  updateSettings(patch: Partial<DisplaySettings>): void;
  setSidebarView(view: SidebarView): void;
  toggleSidebar(): void;

  // locations
  locations: Location[];
  selectedLocationId: string | null;
  addLocation(location: Location): void;
  renameLocation(id: string, name: string): void;
  removeLocation(id: string): void;
  clearLocations(): void;
  selectLocation(id: string | null): Promise<void>;

  // tag library
  tagGroups: TagGroup[];
  tagStyles: Map<string, TagStyle>; // keyed by lowercase tag name
  saveTagLibrary(groups: TagGroup[]): void;
  upsertGroup(group: TagGroup): void;
  deleteGroup(groupId: string): void;
  upsertTag(groupId: string, tag: Tag): void;
  deleteTag(groupId: string, tagId: string): void;
  clearTagLibrary(): void;

  // explorer
  scope: LocationPathScope | null;
  currentPath: string;
  files: FileItem[];
  isBusy: boolean;
  errorMessage: string | null;
  searchQuery: string;
  sort: FileSortOptions;
  selectedTagFilterIds: Set<string>;
  backStack: string[];
  forwardStack: string[];
  selectedPaths: Set<string>;
  focusedPath: string | null;
  anchorPath: string | null;
  renamingPath: string | null;
  zoomLevelIndex: number;

  loadCurrentDirectory(): Promise<void>;
  openDirectory(path: string): Promise<void>;
  navigateBack(): Promise<void>;
  navigateForward(): Promise<void>;
  navigateToParent(): Promise<void>;
  refresh(): Promise<void>;
  setSearchQuery(query: string): void;
  runSearch(): Promise<void>;
  setSort(field?: FileSortField, direction?: FileSortDirection): Promise<void>;
  toggleTagFilter(tagId: string): Promise<void>;
  clearTagFilters(): Promise<void>;
  zoomByWheelDelta(delta: number): void;

  selectOnly(path: string): void;
  toggleSelect(path: string): void;
  extendSelectionTo(path: string): void;
  selectAll(): void;
  clearSelection(): void;
  focusPath(path: string | null): void;
  beginRename(path: string): void;
  cancelRename(): void;

  createFolder(): Promise<void>;
  commitRename(path: string, newFileSystemName: string): Promise<void>;
  deleteSelected(): Promise<void>;
  insertTagIntoFile(file: FileItem, tagName: string, insertionIndex: number): Promise<void>;
  removeTagFromFile(file: FileItem, tagName: string): Promise<void>;
  openFile(file: FileItem): Promise<void>;
  revealFile(file: FileItem): Promise<void>;
  getBlob(path: string): Promise<Blob | null>;
}

function buildTagStyles(groups: readonly TagGroup[]): Map<string, TagStyle> {
  const styles = new Map<string, TagStyle>();
  for (const group of groups) {
    for (const tag of group.tags) {
      const key = tag.name.trim().toLowerCase();
      if (key && !styles.has(key)) {
        styles.set(key, {
          color: tag.color,
          textColor: tag.textColor ?? "#ffffff",
          known: true,
        });
      }
    }
  }
  return styles;
}

function activeTagFilterNames(state: Pick<LuminaState, "tagGroups" | "selectedTagFilterIds">): string[] {
  const names: string[] = [];
  const seen = new Set<string>();
  for (const group of state.tagGroups) {
    for (const tag of group.tags) {
      if (state.selectedTagFilterIds.has(tag.id)) {
        const key = tag.name.trim().toLowerCase();
        if (key && !seen.has(key)) {
          seen.add(key);
          names.push(tag.name.trim());
        }
      }
    }
  }
  return names;
}

function matchesQuery(item: FileItem, query: string): boolean {
  const q = query.toLocaleLowerCase();
  return (
    item.name.toLocaleLowerCase().includes(q) ||
    item.displayName.toLocaleLowerCase().includes(q) ||
    item.tags.some((tag) => tag.toLocaleLowerCase().includes(q))
  );
}

let loadGeneration = 0;

const initialSettings = loadDisplaySettings();
const initialLocations = loadLocations();
const initialTagGroups = loadTagGroups();
const initialAppState = loadAppState();

export const useLumina = create<LuminaState>((set, get) => {
  const persistLocations = () => {
    const { locations, selectedLocationId } = get();
    saveLocations(locations);
    saveAppState({ selectedLocationId });
  };

  const persistTags = (groups: TagGroup[]) => {
    saveTagGroups(groups);
    set({ tagGroups: groups, tagStyles: buildTagStyles(groups) });
    // Prune filter ids that no longer exist, then reload if filters changed.
    const state = get();
    const validIds = new Set(groups.flatMap((g) => g.tags.map((t) => t.id)));
    const pruned = new Set([...state.selectedTagFilterIds].filter((id) => validIds.has(id)));
    if (pruned.size !== state.selectedTagFilterIds.size) {
      set({ selectedTagFilterIds: pruned });
      void state.loadCurrentDirectory();
    }
  };

  const currentBrowser = async (): Promise<FileBrowserService | null> => {
    const { locations, selectedLocationId } = get();
    const location = locations.find((l) => l.id === selectedLocationId);
    return location ? browserFor(location) : null;
  };

  const clearNavigationState = () => {
    set({
      searchQuery: "",
      selectedTagFilterIds: new Set(),
      selectedPaths: new Set(),
      focusedPath: null,
      anchorPath: null,
      renamingPath: null,
    });
  };

  const loadInto = async (path: string) => {
    const generation = ++loadGeneration;
    const state = get();
    const browser = await currentBrowser().catch((error: unknown) => {
      set({ errorMessage: message(error), isBusy: false });
      return null;
    });
    if (!browser || !state.scope) return;
    set({ isBusy: true, errorMessage: null, files: [] });
    try {
      const filterNames = activeTagFilterNames(state);
      const query = state.searchQuery.trim();
      let items: FileItem[];
      let recursive = false;
      if (filterNames.length > 0) {
        recursive = true;
        const required = filterNames.map((n) => n.toLowerCase());
        items = (await browser.listRecursive(path)).filter((item) => {
          if (item.isDirectory) return false;
          const tags = new Set(item.tags.map((t) => t.toLowerCase()));
          if (!required.every((t) => tags.has(t))) return false;
          return query === "" || matchesQuery(item, query);
        });
      } else if (query === "") {
        items = await browser.loadDirectory(path);
      } else {
        recursive = true;
        items = (await browser.listRecursive(path)).filter((item) =>
          matchesQuery(item, query),
        );
      }
      if (generation !== loadGeneration) return;
      set({ files: sortFileItems(items, get().sort, recursive), isBusy: false });
    } catch (error) {
      if (generation !== loadGeneration) return;
      set({
        files: [],
        isBusy: false,
        errorMessage: translate(get().language, "FailedLoadFolder", message(error)),
      });
    }
  };

  const reloadAndSelect = async (paths: string[]) => {
    await loadInto(get().currentPath);
    const wanted = new Set(paths.map((p) => p.toLowerCase()));
    const matching = get().files.filter((f) => wanted.has(f.path.toLowerCase()));
    if (matching.length > 0) {
      set({
        selectedPaths: new Set(matching.map((f) => f.path)),
        anchorPath: matching[0].path,
        focusedPath: matching[matching.length - 1].path,
      });
    }
  };

  return {
    settings: initialSettings,
    language: resolveLanguage(initialSettings.language),
    updateSettings(patch) {
      const settings = { ...get().settings, ...patch };
      saveDisplaySettings(settings);
      set({ settings, language: resolveLanguage(settings.language) });
    },
    setSidebarView(view) {
      get().updateSettings({ sidebarView: view });
    },
    toggleSidebar() {
      get().updateSettings({ sidebarCollapsed: !get().settings.sidebarCollapsed });
    },

    locations: initialLocations,
    selectedLocationId: initialAppState.selectedLocationId,
    addLocation(location) {
      set({ locations: [...get().locations, location] });
      persistLocations();
      void get().selectLocation(location.id);
    },
    renameLocation(id, name) {
      const trimmed = name.trim();
      if (!trimmed) return;
      set({
        locations: get().locations.map((l) => (l.id === id ? { ...l, name: trimmed } : l)),
      });
      persistLocations();
      if (get().selectedLocationId === id) {
        // Breadcrumb root label derives from the location name.
        void get().selectLocation(id);
      }
    },
    removeLocation(id) {
      browserCache.delete(id);
      void deleteLocationHandle(id).catch(() => undefined);
      const wasSelected = get().selectedLocationId === id;
      set({ locations: get().locations.filter((l) => l.id !== id) });
      if (wasSelected) void get().selectLocation(null);
      persistLocations();
    },
    clearLocations() {
      for (const location of get().locations) {
        browserCache.delete(location.id);
        void deleteLocationHandle(location.id).catch(() => undefined);
      }
      set({ locations: [] });
      void get().selectLocation(null);
      persistLocations();
    },
    async selectLocation(id) {
      loadGeneration++;
      const location = get().locations.find((l) => l.id === id) ?? null;
      set({ selectedLocationId: location?.id ?? null });
      persistLocations();
      clearNavigationState();
      set({ backStack: [], forwardStack: [], files: [], errorMessage: null });
      if (!location) {
        set({ scope: null, currentPath: "" });
        return;
      }
      try {
        const scope = new LocationPathScope(location.path);
        set({ scope, currentPath: scope.rootPath });
        await loadInto(scope.rootPath);
      } catch (error) {
        set({
          scope: null,
          currentPath: "",
          errorMessage: translate(get().language, "FailedOpenLocation", message(error)),
        });
      }
    },

    tagGroups: initialTagGroups,
    tagStyles: buildTagStyles(initialTagGroups),
    saveTagLibrary(groups) {
      persistTags(groups);
    },
    upsertGroup(group) {
      const groups = get().tagGroups.some((g) => g.id === group.id)
        ? get().tagGroups.map((g) => (g.id === group.id ? group : g))
        : [...get().tagGroups, group];
      persistTags(groups);
    },
    deleteGroup(groupId) {
      persistTags(get().tagGroups.filter((g) => g.id !== groupId));
    },
    upsertTag(groupId, tag) {
      const groups = get().tagGroups.map((group) => {
        if (group.id !== groupId) {
          // Moving a tag between groups: drop it from its old group.
          if (group.tags.some((t) => t.id === tag.id)) {
            return { ...group, tags: group.tags.filter((t) => t.id !== tag.id) };
          }
          return group;
        }
        const exists = group.tags.some((t) => t.id === tag.id);
        return {
          ...group,
          tags: exists
            ? group.tags.map((t) => (t.id === tag.id ? { ...tag, groupId } : t))
            : [...group.tags, { ...tag, groupId }],
        };
      });
      persistTags(groups);
    },
    deleteTag(groupId, tagId) {
      persistTags(
        get().tagGroups.map((group) =>
          group.id === groupId
            ? { ...group, tags: group.tags.filter((t) => t.id !== tagId) }
            : group,
        ),
      );
    },
    clearTagLibrary() {
      persistTags([]);
    },

    scope: null,
    currentPath: "",
    files: [],
    isBusy: false,
    errorMessage: null,
    searchQuery: "",
    sort: DEFAULT_FILE_SORT_OPTIONS,
    selectedTagFilterIds: new Set<string>(),
    backStack: [],
    forwardStack: [],
    selectedPaths: new Set<string>(),
    focusedPath: null,
    anchorPath: null,
    renamingPath: null,
    zoomLevelIndex: Math.min(
      Math.max(initialSettings.gridSize, 0),
      CARD_WIDTH_ZOOM_LEVELS.length - 1,
    ),

    async loadCurrentDirectory() {
      const { currentPath } = get();
      if (currentPath) await loadInto(currentPath);
    },
    async openDirectory(path) {
      const { scope, currentPath } = get();
      if (!scope) return;
      const target = scope.normalizeContainedPath(path);
      if (isSamePath(target, currentPath)) {
        clearNavigationState();
        await loadInto(currentPath);
        return;
      }
      set({
        backStack: [...get().backStack, currentPath],
        forwardStack: [],
        currentPath: target,
      });
      clearNavigationState();
      await loadInto(target);
    },
    async navigateBack() {
      const { scope } = get();
      if (!scope) return;
      const back = [...get().backStack];
      while (back.length > 0) {
        const candidate = back.pop()!;
        if (scope.containsPath(candidate)) {
          set({
            backStack: back,
            forwardStack: [...get().forwardStack, get().currentPath],
            currentPath: candidate,
          });
          clearNavigationState();
          await loadInto(candidate);
          return;
        }
      }
      set({ backStack: back });
    },
    async navigateForward() {
      const { scope } = get();
      if (!scope) return;
      const forward = [...get().forwardStack];
      while (forward.length > 0) {
        const candidate = forward.pop()!;
        if (scope.containsPath(candidate)) {
          set({
            forwardStack: forward,
            backStack: [...get().backStack, get().currentPath],
            currentPath: candidate,
          });
          clearNavigationState();
          await loadInto(candidate);
          return;
        }
      }
      set({ forwardStack: forward });
    },
    async navigateToParent() {
      const { scope, currentPath } = get();
      const parent = scope?.tryGetParentPath(currentPath);
      if (parent) await get().openDirectory(parent);
    },
    async refresh() {
      await loadInto(get().currentPath);
    },
    setSearchQuery(query) {
      set({ searchQuery: query });
    },
    async runSearch() {
      set({ selectedPaths: new Set(), focusedPath: null, anchorPath: null });
      await loadInto(get().currentPath);
    },
    async setSort(field, direction) {
      const sort = {
        field: field ?? get().sort.field,
        direction: direction ?? get().sort.direction,
      };
      if (sort.field === get().sort.field && sort.direction === get().sort.direction) return;
      const selected = [...get().selectedPaths];
      set({ sort });
      await reloadAndSelect(selected);
    },
    async toggleTagFilter(tagId) {
      const ids = new Set(get().selectedTagFilterIds);
      if (ids.has(tagId)) ids.delete(tagId);
      else ids.add(tagId);
      set({ selectedTagFilterIds: ids, selectedPaths: new Set(), focusedPath: null });
      await loadInto(get().currentPath);
    },
    async clearTagFilters() {
      if (get().selectedTagFilterIds.size === 0) return;
      set({ selectedTagFilterIds: new Set(), selectedPaths: new Set(), focusedPath: null });
      await loadInto(get().currentPath);
    },
    zoomByWheelDelta(delta) {
      const direction = Math.sign(delta);
      if (direction === 0) return;
      const index = Math.min(
        Math.max(get().zoomLevelIndex + direction, 0),
        CARD_WIDTH_ZOOM_LEVELS.length - 1,
      );
      if (index !== get().zoomLevelIndex) {
        set({ zoomLevelIndex: index });
        get().updateSettings({ gridSize: index });
      }
    },

    selectOnly(path) {
      set({
        selectedPaths: new Set([path]),
        focusedPath: path,
        anchorPath: path,
      });
    },
    toggleSelect(path) {
      const selected = new Set(get().selectedPaths);
      if (selected.has(path)) selected.delete(path);
      else selected.add(path);
      set({ selectedPaths: selected, focusedPath: path, anchorPath: path });
    },
    extendSelectionTo(path) {
      const { files, anchorPath, focusedPath } = get();
      const anchor = anchorPath ?? focusedPath ?? path;
      const anchorIndex = files.findIndex((f) => f.path === anchor);
      const targetIndex = files.findIndex((f) => f.path === path);
      if (anchorIndex < 0 || targetIndex < 0) {
        get().selectOnly(path);
        return;
      }
      const [from, to] = [Math.min(anchorIndex, targetIndex), Math.max(anchorIndex, targetIndex)];
      set({
        selectedPaths: new Set(files.slice(from, to + 1).map((f) => f.path)),
        focusedPath: path,
        anchorPath: anchor,
      });
    },
    selectAll() {
      const { files } = get();
      if (files.length === 0) return;
      set({
        selectedPaths: new Set(files.map((f) => f.path)),
        anchorPath: get().anchorPath ?? files[0].path,
        focusedPath: get().focusedPath ?? files[0].path,
      });
    },
    clearSelection() {
      set({ selectedPaths: new Set(), focusedPath: null, anchorPath: null });
    },
    focusPath(path) {
      set({ focusedPath: path });
    },
    beginRename(path) {
      set({ renamingPath: path });
    },
    cancelRename() {
      set({ renamingPath: null });
    },

    async createFolder() {
      const browser = await currentBrowser();
      const { currentPath, language } = get();
      if (!browser || !currentPath) return;
      set({ searchQuery: "" });
      try {
        const newPath = await browser.createDirectory(
          currentPath,
          translate(language, "NewFolder"),
        );
        await reloadAndSelect([newPath]);
        set({ renamingPath: newPath });
      } catch (error) {
        set({ errorMessage: message(error) });
      }
    },
    async commitRename(path, newFileSystemName) {
      const browser = await currentBrowser();
      if (!browser) return;
      set({ renamingPath: null });
      const trimmed = newFileSystemName.trim();
      if (!trimmed) return;
      try {
        const newPath = await browser.rename(path, trimmed);
        await reloadAndSelect([newPath]);
      } catch (error) {
        set({ errorMessage: message(error) });
      }
    },
    async deleteSelected() {
      const browser = await currentBrowser();
      const { files, selectedPaths } = get();
      if (!browser || selectedPaths.size === 0) return;
      const indexes = files
        .map((f, i) => (selectedPaths.has(f.path) ? i : -1))
        .filter((i) => i >= 0);
      const minIndex = Math.min(...indexes);
      try {
        await browser.deleteMany([...selectedPaths]);
        await loadInto(get().currentPath);
        const remaining = get().files;
        if (remaining.length > 0) {
          const next = remaining[Math.min(minIndex, remaining.length - 1)];
          get().selectOnly(next.path);
        }
      } catch (error) {
        set({ errorMessage: message(error) });
      }
    },
    async insertTagIntoFile(file, tagName, insertionIndex) {
      if (file.isDirectory) return;
      const { insertTagIntoFilename } = await import("../core/tagParser");
      const newName = insertTagIntoFilename(file.name, tagName, insertionIndex);
      if (newName === file.name) return;
      await get().commitRename(file.path, newName);
    },
    async removeTagFromFile(file, tagName) {
      const { removeTagFromFilename } = await import("../core/tagParser");
      const newName = removeTagFromFilename(file.name, tagName);
      if (newName === file.name) return;
      await get().commitRename(file.path, newName);
    },
    async openFile(file) {
      if (file.isDirectory) {
        await get().openDirectory(file.path);
        return;
      }
      const browser = await currentBrowser();
      if (!browser) return;
      // Desktop adapter: hand the file to the platform default app.
      if (browser.openExternally) {
        try {
          await browser.openExternally(file.path);
        } catch (error) {
          set({ errorMessage: message(error) });
        }
        return;
      }
      const blob = await browser.getFileBlob(file.path);
      if (!blob) return;
      const url = URL.createObjectURL(blob);
      window.open(url, "_blank");
      setTimeout(() => URL.revokeObjectURL(url), 60_000);
    },
    async revealFile(file) {
      const browser = await currentBrowser();
      if (!browser?.revealInShell) return;
      try {
        await browser.revealInShell(file.path);
      } catch (error) {
        set({ errorMessage: message(error) });
      }
    },
    async getBlob(path) {
      const browser = await currentBrowser().catch(() => null);
      if (!browser) return null;
      return browser.getFileBlob(path).catch(() => null);
    },
  };
});

function message(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}

/** Translation hook bound to the current language. */
export function useT(): (key: string, ...args: (string | number)[]) => string {
  const language = useLumina((s) => s.language);
  return (key, ...args) => translate(language, key, ...args);
}

export function tagStyleFor(styles: Map<string, TagStyle>, tagName: string): TagStyle {
  return styles.get(tagName.trim().toLowerCase()) ?? FALLBACK_TAG_STYLE;
}
