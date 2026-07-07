import type { FileItem } from "../core/models";
import { previewKindFor } from "../core/models";
import { getDisplayName, parseTagsFromFilename } from "../core/tagParser";
import { joinPath, parentPathOf } from "../core/paths";
import { uniqueName, validateEntryName, type FileBrowserService } from "./types";
import { nativeApi, type NativeEntry } from "./electronApi";

function makeItem(entry: NativeEntry, virtualPath: string, relativePath: string): FileItem {
  const displayName = getDisplayName(entry.name);
  return {
    name: entry.name,
    displayName: displayName.trim() ? displayName : entry.name,
    path: virtualPath,
    relativePath,
    isDirectory: entry.isDirectory,
    previewKind: previewKindFor(entry.name, entry.isDirectory),
    size: entry.size,
    modified: entry.modified,
    tags: parseTagsFromFilename(entry.name),
  };
}

/**
 * FileBrowserService over the Electron IPC bridge. Virtual paths
 * ("loc:{id}/sub/file.ext") map onto an absolute Windows root; deletes go
 * to the Recycle Bin and files open with their default app.
 */
export class ElectronFileBrowser implements FileBrowserService {
  private readonly rootPath: string; // virtual root, loc:{id}
  private readonly nativeRoot: string; // absolute Windows path

  constructor(rootPath: string, nativeRoot: string) {
    this.rootPath = rootPath;
    this.nativeRoot = nativeRoot.replace(/[\\/]+$/, "");
  }

  /** loc:{id}/a/b -> {nativeRoot}\a\b */
  private toNative(virtualPath: string): string {
    const trimmed = virtualPath.trim().replace(/\/+$/, "");
    if (!trimmed.toLowerCase().startsWith(this.rootPath.toLowerCase())) {
      throw new Error(`Path is outside the current location root: ${virtualPath}`);
    }
    const rest = trimmed.slice(this.rootPath.length).replace(/^\//, "");
    if (!rest) return this.nativeRoot;
    return `${this.nativeRoot}\\${rest.replaceAll("/", "\\")}`;
  }

  async loadDirectory(path: string): Promise<FileItem[]> {
    const entries = await nativeApi().list(this.toNative(path));
    return entries.map((entry) => makeItem(entry, joinPath(path, entry.name), ""));
  }

  async listRecursive(path: string): Promise<FileItem[]> {
    const virtualRoot = path.trim().replace(/\/+$/, "");
    const entries = await nativeApi().listRecursive(this.toNative(path));
    return entries.map((entry) => {
      const virtualPath =
        entry.relativeParent === "."
          ? joinPath(virtualRoot, entry.name)
          : joinPath(virtualRoot, `${entry.relativeParent}/${entry.name}`);
      return makeItem(entry, virtualPath, entry.relativeParent);
    });
  }

  async createDirectory(parentPath: string, preferredName: string): Promise<string> {
    const siblings = await nativeApi().list(this.toNative(parentPath));
    const name = uniqueName(
      new Set(siblings.map((s) => s.name)),
      validateEntryName(preferredName),
    );
    await nativeApi().mkdir(`${this.toNative(parentPath)}\\${name}`);
    return joinPath(parentPath, name);
  }

  async rename(path: string, newName: string): Promise<string> {
    const name = validateEntryName(newName);
    const normalized = path.trim().replace(/\/+$/, "");
    const parent = parentPathOf(normalized);
    if (parent === null || normalized.toLowerCase() === this.rootPath.toLowerCase()) {
      throw new Error(`Cannot rename root directory: ${path}`);
    }
    await nativeApi().rename(this.toNative(normalized), name);
    return joinPath(parent, name);
  }

  async deleteMany(paths: string[]): Promise<void> {
    await nativeApi().trash(paths.map((p) => this.toNative(p)));
  }

  async getFileBlob(path: string): Promise<Blob | null> {
    try {
      const buffer = await nativeApi().readFile(this.toNative(path));
      return new Blob([buffer]);
    } catch {
      return null;
    }
  }

  /** Opens a file with its Windows default app (explorer double-click). */
  async openExternally(path: string): Promise<boolean> {
    return nativeApi().openPath(this.toNative(path));
  }

  /** Shows the item selected in File Explorer. */
  async revealInShell(path: string): Promise<boolean> {
    return nativeApi().reveal(this.toNative(path));
  }
}
