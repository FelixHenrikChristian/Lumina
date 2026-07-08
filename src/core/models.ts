// Port of Lumina.Core models. C# records become readonly interfaces;
// numeric enums become string unions.
export type FilePreviewKind = "none" | "image" | "video";
export type FileSortField = "name" | "modified" | "type" | "size" | "created";
export type FileSortDirection = "ascending" | "descending";

export interface FileSortOptions {
  readonly field: FileSortField;
  readonly direction: FileSortDirection;
}

export const DEFAULT_FILE_SORT_OPTIONS: FileSortOptions = {
  field: "name",
  direction: "ascending",
};

export function newId(): string {
  return crypto.randomUUID().replace(/-/g, "");
}

export interface Location {
  readonly id: string;
  readonly name: string;
  /** Virtual root path, `loc:{id}` — real directory handles live in the FS adapter. */
  readonly path: string;
  readonly kind: "demo" | "fsa" | "native";
  /** Absolute OS path for kind "native" (Electron). */
  readonly nativePath?: string;
}

export interface Tag {
  readonly id: string;
  readonly name: string;
  readonly color: string;
  readonly textColor: string | null;
  readonly groupId: string | null;
}

export interface TagGroup {
  readonly id: string;
  readonly name: string;
  readonly defaultColor: string;
  readonly defaultTextColor: string | null;
  readonly description: string | null;
  readonly tags: readonly Tag[];
}

export const DEFAULT_TAG_COLOR = "#2196f3";

export interface AppState {
  readonly selectedLocationId: string | null;
}

export type SidebarView = "locations" | "tags";

export type GlassMode = "standard" | "polar" | "prominent" | "shader";

/** liquid-glass-react surface tuning, user-editable in Settings. */
export interface GlassConfig {
  readonly mode: GlassMode;
  readonly displacementScale: number;
  readonly blurAmount: number;
  readonly saturation: number;
  readonly aberrationIntensity: number;
  readonly elasticity: number;
  readonly cornerRadius: number;
  readonly overLight: boolean;
}

export const DEFAULT_GLASS_CONFIG: GlassConfig = {
  mode: "standard",
  displacementScale: 64,
  blurAmount: 0.12,
  saturation: 140,
  aberrationIntensity: 2,
  elasticity: 0.15,
  cornerRadius: 24,
  overLight: false,
};

export const GLASS_MODES: readonly GlassMode[] = [
  "standard",
  "polar",
  "prominent",
  "shader",
];

/**
 * Persisted glass configs may be partial (older versions) or hand-edited;
 * out-of-range numbers just look odd, but an unknown mode makes
 * liquid-glass-react throw. Clamp everything to the ranges the settings
 * UI offers.
 */
export function normalizeGlassConfig(value: Partial<GlassConfig> | undefined): GlassConfig {
  const d = DEFAULT_GLASS_CONFIG;
  const raw = value ?? {};
  const num = (v: unknown, min: number, max: number, fallback: number) =>
    typeof v === "number" && Number.isFinite(v)
      ? Math.min(Math.max(v, min), max)
      : fallback;
  return {
    mode: GLASS_MODES.includes(raw.mode as GlassMode) ? (raw.mode as GlassMode) : d.mode,
    displacementScale: num(raw.displacementScale, 0, 200, d.displacementScale),
    blurAmount: num(raw.blurAmount, 0, 1, d.blurAmount),
    saturation: num(raw.saturation, 100, 300, d.saturation),
    aberrationIntensity: num(raw.aberrationIntensity, 0, 20, d.aberrationIntensity),
    elasticity: num(raw.elasticity, 0, 1, d.elasticity),
    cornerRadius: num(raw.cornerRadius, 0, 100, d.cornerRadius),
    overLight: typeof raw.overLight === "boolean" ? raw.overLight : d.overLight,
  };
}

export interface DisplaySettings {
  readonly language: string; // 'system' | 'en-US' | 'zh-Hans'
  readonly hideFileExtension: boolean;
  readonly showParentFolderInRecursiveSearch: boolean;
  readonly gridSize: number; // zoom level index 0..5
  readonly sidebarView: SidebarView;
  readonly glass: GlassConfig;
}

export const DEFAULT_DISPLAY_SETTINGS: DisplaySettings = {
  language: "system",
  hideFileExtension: false,
  showParentFolderInRecursiveSearch: true,
  gridSize: 2,
  sidebarView: "locations",
  glass: DEFAULT_GLASS_CONFIG,
};

export interface FileItem {
  readonly name: string; // raw name incl. tag block
  readonly displayName: string; // tags stripped, falls back to name
  readonly path: string; // virtual absolute path
  readonly relativePath: string; // parent dir relative to search root ('' for plain loads)
  readonly isDirectory: boolean;
  readonly previewKind: FilePreviewKind;
  readonly size: number;
  readonly modified: number | null; // epoch ms
  readonly tags: readonly string[];
}

export interface LocationPathSegment {
  readonly name: string;
  readonly path: string;
}

export const CARD_WIDTH_ZOOM_LEVELS = [176, 208, 240, 280, 320, 368] as const;
export const INFO_PANEL_HEIGHT = 48;

export function cardHeightForWidth(width: number): number {
  return Math.round((width * 9) / 16 + INFO_PANEL_HEIGHT);
}

const IMAGE_EXTENSIONS = new Set([
  ".avif", ".bmp", ".dib", ".gif", ".heic", ".heif", ".ico", ".jfif",
  ".jpe", ".jpeg", ".jpg", ".png", ".svg", ".tif", ".tiff", ".webp",
]);

const VIDEO_EXTENSIONS = new Set([
  ".3g2", ".3gp", ".avi", ".m2ts", ".m4v", ".mkv", ".mov", ".mp4",
  ".mpeg", ".mpg", ".mts", ".webm", ".wmv",
]);

const AUDIO_EXTENSIONS = new Set([".flac", ".m4a", ".mp3", ".wav", ".wma"]);
const DOCUMENT_EXTENSIONS = new Set([".doc", ".docx", ".md", ".pdf", ".rtf", ".txt"]);
const ARCHIVE_EXTENSIONS = new Set([".7z", ".rar", ".zip"]);

export function extensionOf(name: string): string {
  const dot = name.lastIndexOf(".");
  return dot <= 0 || dot === name.length - 1 ? "" : name.slice(dot).toLowerCase();
}

export function previewKindFor(name: string, isDirectory: boolean): FilePreviewKind {
  if (isDirectory) return "none";
  const ext = extensionOf(name);
  if (IMAGE_EXTENSIONS.has(ext)) return "image";
  if (VIDEO_EXTENSIONS.has(ext)) return "video";
  return "none";
}

export type FileGlyphKind =
  | "folder" | "image" | "video" | "audio" | "document" | "archive" | "generic";

export function glyphKindFor(item: FileItem): FileGlyphKind {
  if (item.isDirectory) return "folder";
  if (item.previewKind === "image") return "image";
  if (item.previewKind === "video") return "video";
  const ext = extensionOf(item.name);
  if (AUDIO_EXTENSIONS.has(ext)) return "audio";
  if (DOCUMENT_EXTENSIONS.has(ext)) return "document";
  if (ARCHIVE_EXTENSIONS.has(ext)) return "archive";
  return "generic";
}

export function isValidHexColor(value: string | null | undefined): value is string {
  if (!value || !value.startsWith("#")) return false;
  if (value.length !== 7 && value.length !== 9) return false;
  return /^[0-9a-fA-F]+$/.test(value.slice(1));
}

/**
 * Stored colors follow the WinUI #AARRGGBB convention when 9 chars long;
 * CSS expects #RRGGBBAA, so alpha moves to the tail.
 */
export function cssColorFor(hex: string): string {
  if (!isValidHexColor(hex)) return hex;
  if (hex.length === 9) return `#${hex.slice(3)}${hex.slice(1, 3)}`;
  return hex;
}

/** Contrast color per the original brightness formula. Invalid input => white. */
export function contrastColorFor(hex: string): string {
  if (!isValidHexColor(hex)) return "#ffffff";
  const offset = hex.length === 9 ? 3 : 1;
  const r = parseInt(hex.slice(offset, offset + 2), 16);
  const g = parseInt(hex.slice(offset + 2, offset + 4), 16);
  const b = parseInt(hex.slice(offset + 4, offset + 6), 16);
  const brightness = (r * 299 + g * 587 + b * 114) / 1000;
  return brightness > 128 ? "#000000" : "#ffffff";
}
