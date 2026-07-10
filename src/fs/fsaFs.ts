import type { FileItem } from "../core/models";
import { previewKindFor } from "../core/models";
import { getDisplayName, parseTagsFromFilename } from "../core/tagParser";
import { baseNameOf, joinPath, parentPathOf } from "../core/paths";
import {
  copyName,
  uniqueName,
  validateEntryName,
  type FileBrowserService,
  type TransferConflictResolutions,
} from "./types";

// Minimal typings for File System Access API members not yet in lib.dom.
interface DirectoryHandle extends FileSystemDirectoryHandle {
  queryPermission?(desc: { mode: string }): Promise<PermissionState>;
  requestPermission?(desc: { mode: string }): Promise<PermissionState>;
}

interface MovableHandle extends FileSystemHandle {
  move?(newName: string): Promise<void>;
}

declare global {
  interface Window {
    showDirectoryPicker?(options?: { mode?: string }): Promise<FileSystemDirectoryHandle>;
  }
}

export function isFileSystemAccessSupported(): boolean {
  return typeof window.showDirectoryPicker === "function";
}

// --- IndexedDB persistence for directory handles ---------------------------

const DB_NAME = "lumina-handles";
const STORE = "handles";

function openDb(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open(DB_NAME, 1);
    request.onupgradeneeded = () => request.result.createObjectStore(STORE);
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}

async function withStore<T>(
  mode: IDBTransactionMode,
  run: (store: IDBObjectStore) => IDBRequest<T>,
): Promise<T> {
  const db = await openDb();
  try {
    return await new Promise<T>((resolve, reject) => {
      const request = run(db.transaction(STORE, mode).objectStore(STORE));
      request.onsuccess = () => resolve(request.result);
      request.onerror = () => reject(request.error);
    });
  } finally {
    db.close();
  }
}

export async function saveLocationHandle(
  locationId: string,
  handle: FileSystemDirectoryHandle,
): Promise<void> {
  await withStore("readwrite", (store) => store.put(handle, locationId));
}

export async function loadLocationHandle(
  locationId: string,
): Promise<FileSystemDirectoryHandle | null> {
  const handle = await withStore<FileSystemDirectoryHandle | undefined>(
    "readonly",
    (store) => store.get(locationId) as IDBRequest<FileSystemDirectoryHandle | undefined>,
  );
  return handle ?? null;
}

export async function deleteLocationHandle(locationId: string): Promise<void> {
  await withStore("readwrite", (store) => store.delete(locationId));
}

export async function ensurePermission(
  handle: FileSystemDirectoryHandle,
): Promise<boolean> {
  const dh = handle as DirectoryHandle;
  const desc = { mode: "readwrite" };
  if ((await dh.queryPermission?.(desc)) === "granted") return true;
  return (await dh.requestPermission?.(desc)) === "granted";
}

// --- Adapter ----------------------------------------------------------------

async function fileItemFor(
  handle: FileSystemHandle,
  path: string,
  relativePath: string,
): Promise<FileItem> {
  const isDirectory = handle.kind === "directory";
  let size = 0;
  let modified: number | null = null;
  if (!isDirectory) {
    const file = await (handle as FileSystemFileHandle).getFile();
    size = file.size;
    modified = file.lastModified;
  }
  const displayName = getDisplayName(handle.name);
  return {
    name: handle.name,
    displayName: displayName.trim() ? displayName : handle.name,
    path,
    relativePath,
    isDirectory,
    previewKind: previewKindFor(handle.name, isDirectory),
    size,
    modified,
    tags: parseTagsFromFilename(handle.name),
  };
}

export class FsaFileBrowser implements FileBrowserService {
  private readonly rootPath: string;
  private readonly rootHandle: FileSystemDirectoryHandle;

  constructor(rootPath: string, rootHandle: FileSystemDirectoryHandle) {
    this.rootPath = rootPath;
    this.rootHandle = rootHandle;
  }

  private segmentsOf(path: string): string[] {
    const trimmed = path.trim().replace(/\/+$/, "");
    if (!trimmed.toLowerCase().startsWith(this.rootPath.toLowerCase())) {
      throw new Error(`Path is outside the current location root: ${path}`);
    }
    const rest = trimmed.slice(this.rootPath.length).replace(/^\//, "");
    return rest ? rest.split("/") : [];
  }

  private async resolveDirectory(path: string): Promise<DirectoryHandle> {
    let dir = this.rootHandle as DirectoryHandle;
    for (const segment of this.segmentsOf(path)) {
      dir = (await dir.getDirectoryHandle(segment)) as DirectoryHandle;
    }
    return dir;
  }

  private async resolveEntry(
    path: string,
  ): Promise<{ parent: DirectoryHandle; handle: FileSystemHandle; name: string }> {
    const segments = this.segmentsOf(path);
    if (segments.length === 0) {
      throw new Error(`Cannot rename root directory: ${path}`);
    }
    const name = segments[segments.length - 1];
    let parent = this.rootHandle as DirectoryHandle;
    for (const segment of segments.slice(0, -1)) {
      parent = (await parent.getDirectoryHandle(segment)) as DirectoryHandle;
    }
    try {
      return { parent, handle: await parent.getDirectoryHandle(name), name };
    } catch {
      try {
        return { parent, handle: await parent.getFileHandle(name), name };
      } catch {
        throw new Error(`File or directory not found: ${path}`);
      }
    }
  }

  async loadDirectory(path: string): Promise<FileItem[]> {
    const dir = await this.resolveDirectory(path);
    const items: FileItem[] = [];
    for await (const [name, handle] of dir.entries()) {
      items.push(await fileItemFor(handle, joinPath(path, name), ""));
    }
    return items;
  }

  async listRecursive(path: string): Promise<FileItem[]> {
    const results: FileItem[] = [];
    const walk = async (dir: DirectoryHandle, dirPath: string, relative: string) => {
      for await (const [name, handle] of dir.entries()) {
        const childPath = joinPath(dirPath, name);
        try {
          results.push(await fileItemFor(handle, childPath, relative));
        } catch {
          continue; // mirror IgnoreInaccessible
        }
        if (handle.kind === "directory") {
          await walk(
            handle as DirectoryHandle,
            childPath,
            relative === "." ? name : `${relative}/${name}`,
          );
        }
      }
    };
    await walk(await this.resolveDirectory(path), path.trim().replace(/\/+$/, ""), ".");
    return results;
  }

  async createDirectory(parentPath: string, preferredName: string): Promise<string> {
    const parent = await this.resolveDirectory(parentPath);
    const existing = new Set<string>();
    for await (const [name] of parent.entries()) existing.add(name);
    const name = uniqueName(existing, validateEntryName(preferredName));
    await parent.getDirectoryHandle(name, { create: true });
    return joinPath(parentPath, name);
  }

  async rename(path: string, newName: string): Promise<string> {
    const name = validateEntryName(newName);
    const normalized = path.trim().replace(/\/+$/, "");
    const parentPath = parentPathOf(normalized);
    if (parentPath === null) throw new Error(`Cannot rename root directory: ${path}`);
    const { parent, handle } = await this.resolveEntry(normalized);
    const oldName = baseNameOf(normalized);
    if (name === oldName) return normalized;

    const sameSlot = name.toLowerCase() === oldName.toLowerCase();
    if (!sameSlot) {
      const taken = await parent
        .getFileHandle(name)
        .catch(() => parent.getDirectoryHandle(name).catch(() => null));
      if (taken) throw new Error(`Destination already exists: ${joinPath(parentPath, name)}`);
    }

    const movable = handle as MovableHandle;
    if (typeof movable.move === "function") {
      await movable.move(name);
      return joinPath(parentPath, name);
    }
    if (handle.kind === "file") {
      // Fallback for engines without FileSystemHandle.move: copy + delete.
      const blob = await (handle as FileSystemFileHandle).getFile();
      const target = await parent.getFileHandle(name, { create: true });
      const writable = await target.createWritable();
      await blob.stream().pipeTo(writable);
      await parent.removeEntry(oldName);
      return joinPath(parentPath, name);
    }
    throw new Error("Renaming folders is not supported by this browser.");
  }

  async deleteMany(paths: string[], _permanently = false): Promise<boolean> {
    for (const path of paths) {
      const normalized = path.trim().replace(/\/+$/, "");
      const { parent, name } = await this.resolveEntry(normalized);
      await parent.removeEntry(name, { recursive: true });
    }
    return true;
  }

  async transferMany(
    paths: string[],
    destinationPath: string,
    move: boolean,
    resolutions: TransferConflictResolutions = {},
  ): Promise<boolean> {
    const destination = await this.resolveDirectory(destinationPath);
    const normalizedDestination = destinationPath.trim().replace(/\/+$/, "").toLowerCase();
    const entries = await Promise.all(
      paths.map(async (path) => {
        const normalized = path.trim().replace(/\/+$/, "");
        const entry = await this.resolveEntry(normalized);
        if (entry.handle.kind === "directory" && normalizedDestination.startsWith(`${normalized.toLowerCase()}/`)) {
          throw new Error("Cannot transfer a folder into itself.");
        }
        const existing = await destination
          .getFileHandle(entry.name)
          .catch(() => destination.getDirectoryHandle(entry.name).catch(() => null));
        const action = resolutions[normalized.toLowerCase()];
        if (entry.parent === destination) {
          if (move) {
            if (action === "skip") return null;
            throw new Error("The source and destination folders are the same.");
          }
          return { ...entry, targetName: await this.copyNameIn(destination, entry.name) };
        }
        if (existing) {
          if (action === "skip") return null;
          if (action === "keepBoth") {
            return { ...entry, targetName: await this.copyNameIn(destination, entry.name) };
          }
          if (action !== "replace") throw new Error(`Destination already exists: ${joinPath(destinationPath, entry.name)}`);
        }
        return { ...entry, targetName: entry.name, replace: Boolean(existing) };
      }),
    );

    const copyEntry = async (parent: DirectoryHandle, handle: FileSystemHandle, name = handle.name): Promise<void> => {
      if (handle.kind === "file") {
        const source = await (handle as FileSystemFileHandle).getFile();
        const target = await parent.getFileHandle(name, { create: true });
        const writable = await target.createWritable();
        await source.stream().pipeTo(writable);
        return;
      }
      const target = (await parent.getDirectoryHandle(name, { create: true })) as DirectoryHandle;
      for await (const [, child] of (handle as DirectoryHandle).entries()) {
        await copyEntry(target, child);
      }
    };

    for (const entry of entries) {
      if (!entry) continue;
      const { parent, handle, name, targetName } = entry;
      const replace = "replace" in entry && entry.replace;
      if (replace) await destination.removeEntry(targetName, { recursive: true });
      await copyEntry(destination, handle, targetName);
      if (move) await parent.removeEntry(name, { recursive: handle.kind === "directory" });
    }
    return true;
  }

  private async copyNameIn(parent: DirectoryHandle, name: string): Promise<string> {
    const existing = new Set<string>();
    for await (const [entryName] of parent.entries()) existing.add(entryName);
    return copyName(existing, name);
  }

  async getFileBlob(path: string): Promise<Blob | null> {
    const { handle } = await this.resolveEntry(path);
    if (handle.kind !== "file") return null;
    return (handle as FileSystemFileHandle).getFile();
  }
}
