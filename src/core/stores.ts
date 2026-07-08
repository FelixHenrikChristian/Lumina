import type { AppState, DisplaySettings, Location, TagGroup } from "./models";
import { DEFAULT_DISPLAY_SETTINGS, normalizeGlassConfig } from "./models";
import { normalizeLanguage } from "./localization";

// Web replacement for the %LocalAppData%\Lumina JSON stores. Same camelCase
// shapes, persisted to localStorage under lumina.* keys.
const KEYS = {
  appState: "lumina.app-state",
  settings: "lumina.settings",
  locations: "lumina.locations",
  tagGroups: "lumina.tag-groups",
} as const;

function load<T>(key: string, fallback: T): T {
  try {
    const raw = localStorage.getItem(key);
    if (raw === null) return fallback;
    const parsed = JSON.parse(raw) as T | null;
    return parsed ?? fallback;
  } catch {
    return fallback;
  }
}

function save(key: string, value: unknown): void {
  localStorage.setItem(key, JSON.stringify(value));
}

export function loadAppState(): AppState {
  return load<AppState>(KEYS.appState, { selectedLocationId: null });
}

export function saveAppState(state: AppState): void {
  save(KEYS.appState, state);
}

export function loadDisplaySettings(): DisplaySettings {
  const settings = { ...DEFAULT_DISPLAY_SETTINGS, ...load(KEYS.settings, {}) };
  return {
    ...settings,
    language: normalizeLanguage(settings.language),
    // Settings moved from a sidebar tab to its own dialog; older persisted
    // state may still say "settings".
    sidebarView: settings.sidebarView === "tags" ? "tags" : "locations",
    // Clamp persisted glass configs (partial or hand-edited) to valid ranges.
    glass: normalizeGlassConfig(settings.glass),
  };
}

export function saveDisplaySettings(settings: DisplaySettings): void {
  save(KEYS.settings, { ...settings, language: normalizeLanguage(settings.language) });
}

export function loadLocations(): Location[] {
  return load<Location[]>(KEYS.locations, []);
}

export function saveLocations(locations: readonly Location[]): void {
  save(KEYS.locations, locations);
}

export function loadTagGroups(): TagGroup[] {
  return load<TagGroup[]>(KEYS.tagGroups, []);
}

export function saveTagGroups(groups: readonly TagGroup[]): void {
  save(KEYS.tagGroups, groups);
}
