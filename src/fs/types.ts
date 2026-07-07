import type { FileItem } from "../core/models";

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
  deleteMany(paths: string[]): Promise<void>;
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
