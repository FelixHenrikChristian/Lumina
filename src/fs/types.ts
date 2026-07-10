import type { FileItem } from "../core/models";

export type TransferConflictAction = "replace" | "skip" | "keepBoth";
export type TransferConflictResolutions = Readonly<Record<string, TransferConflictAction>>;
export interface SystemClipboardPasteResult {
  readonly pasted: boolean;
  readonly supported?: boolean;
  readonly undoRecorded?: boolean;
}
export interface FileClipboardState {
  readonly paths: string[];
  readonly move: boolean;
  readonly supported?: boolean;
}

/**
 * Web replacement for IFileBrowserService. Paths are virtual
 * ("loc:{locationId}/sub/file.ext"); each adapter resolves them against its
 * own root. Listings are returned unsorted — the store applies
 * sortFileItems. Errors are thrown with user-presentable messages.
 */
export interface FileBrowserService {
  /** Non-recursive listing; relativePath is ''. */
  loadDirectory(path: string): Promise<FileItem[]>;
  /**
   * Recursive listing rooted at path; relativePath is the item's parent
   * directory relative to the root ('.' when the parent is the root).
   * Symlink-cycle concerns don't apply to either adapter.
   */
  listRecursive(path: string): Promise<FileItem[]>;
  /** Creates "{preferredName}", "{preferredName} (2)", ... Returns the new path. */
  createDirectory(parentPath: string, preferredName: string): Promise<string>;
  /** Renames within the same parent. Returns the resulting path. */
  rename(path: string, newName: string): Promise<string>;
  deleteMany(paths: string[], permanently?: boolean): Promise<boolean>;
  /** Copies or moves entries into a directory, resolving each named conflict explicitly. */
  transferMany(
    paths: string[],
    destinationPath: string,
    move: boolean,
    resolutions?: TransferConflictResolutions,
  ): Promise<boolean>;
  /** Native desktop only: exchange file lists with the operating system clipboard. */
  writeFileClipboard?(paths: string[], move: boolean): Promise<void>;
  pasteFileClipboard?(destinationPath: string): Promise<SystemClipboardPasteResult>;
  readFileClipboard?(): Promise<FileClipboardState>;
  undoNativePaste?(): Promise<boolean>;
  redoNativePaste?(): Promise<boolean>;
  /** Restores items deleted through the Windows Recycle Bin. Desktop adapter only. */
  restoreDeleted?(paths: string[]): Promise<void>;
  watchDirectory?(path: string, onChanged: () => void): Promise<() => void>;
  pathExists?(path: string): Promise<boolean>;
  /** Blob for previews/open; null when unavailable. */
  getFileBlob(path: string): Promise<Blob | null>;
  /** Opens a file with the platform default app. Desktop adapter only. */
  openExternally?(path: string): Promise<boolean>;
  /** Shows the item in the platform file manager. Desktop adapter only. */
  revealInShell?(path: string): Promise<boolean>;
}

const INVALID_NAME = /[\\/:*?"<>|]/;

export function validateEntryName(name: string): string {
  const trimmed = name.trim();
  if (!trimmed || trimmed === "." || trimmed === ".." || INVALID_NAME.test(trimmed)) {
    throw new Error("The name must be a valid file or folder name.");
  }
  return trimmed;
}

export function uniqueName(existing: Set<string>, preferred: string): string {
  const lower = new Set([...existing].map((n) => n.toLowerCase()));
  if (!lower.has(preferred.toLowerCase())) return preferred;
  for (let i = 2; i < 1000; i++) {
    const candidate = `${preferred} (${i})`;
    if (!lower.has(candidate.toLowerCase())) return candidate;
  }
  throw new Error(`Could not resolve a unique folder name for: ${preferred}`);
}

/** Windows Explorer-style "Copy" suffix, keeping a file extension at the end. */
export function copyName(existing: Set<string>, name: string): string {
  const dot = name.lastIndexOf(".");
  const stem = dot > 0 ? name.slice(0, dot) : name;
  const extension = dot > 0 ? name.slice(dot) : "";
  for (let index = 1; index < 1000; index += 1) {
    const suffix = index === 1 ? " - Copy" : ` - Copy (${index})`;
    const candidate = `${stem}${suffix}${extension}`;
    if (![...existing].some((entry) => entry.toLowerCase() === candidate.toLowerCase())) {
      return candidate;
    }
  }
  throw new Error(`Could not resolve a copy name for: ${name}`);
}
