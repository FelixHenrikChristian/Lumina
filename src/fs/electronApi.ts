// Typed view of the API exposed by electron/preload.cjs. Present only when
// the app runs inside Electron.
import type { CustomWallpaper } from "../core/models";
import type { FileClipboardState, SystemClipboardPasteResult } from "./types";

export interface NativeEntry {
  readonly name: string;
  readonly path: string; // absolute Windows path
  readonly relativeParent: string; // '' for plain lists, '.'/'sub/dir' recursive
  readonly isDirectory: boolean;
  readonly size: number;
  readonly modified: number | null;
}

export interface LuminaNativeApi {
  chooseWallpaper(): Promise<CustomWallpaper | null>;
  pickFolder(): Promise<{ path: string; name: string } | null>;
  registerRoot(rootPath: string): Promise<boolean>;
  watchDirectory(directoryPath: string): Promise<string>;
  unwatchDirectory(token: string): Promise<void>;
  onDirectoryChanged(callback: () => void): () => void;
  list(dirPath: string): Promise<NativeEntry[]>;
  pathExists(targetPath: string): Promise<boolean>;
  listRecursive(rootPath: string): Promise<NativeEntry[]>;
  mkdir(dirPath: string): Promise<string>;
  rename(oldPath: string, newName: string): Promise<string>;
  trash(paths: string[]): Promise<{ aborted: boolean }>;
  deletePermanently(paths: string[]): Promise<{ aborted: boolean }>;
  transfer(paths: string[], destinationPath: string, move: boolean): Promise<{ aborted: boolean }>;
  writeFileClipboard(paths: string[], move: boolean): Promise<void>;
  pasteFileClipboard(destinationPath: string): Promise<SystemClipboardPasteResult>;
  readFileClipboard(): Promise<FileClipboardState>;
  undoNativePaste(): Promise<{ handled: boolean }>;
  redoNativePaste(): Promise<{ handled: boolean }>;
  restoreDeleted(paths: string[]): Promise<void>;
  readFile(filePath: string): Promise<ArrayBuffer>;
  thumbnail(filePath: string): Promise<string | null>;
  openPath(targetPath: string): Promise<boolean>;
  reveal(targetPath: string): Promise<boolean>;
}

declare global {
  interface Window {
    luminaNative?: LuminaNativeApi;
  }
}

export function isElectron(): boolean {
  return typeof window !== "undefined" && window.luminaNative !== undefined;
}

export function nativeApi(): LuminaNativeApi {
  if (!window.luminaNative) {
    throw new Error("The native bridge is only available in the desktop app.");
  }
  return window.luminaNative;
}
