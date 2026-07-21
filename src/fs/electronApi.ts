// Typed view of the API exposed by electron/preload.cjs. Present only when
// the app runs inside Electron.
import type { CustomWallpaper } from "../core/models";
import type { FileClipboardState, SystemClipboardPasteResult, SystemImportResult } from "./types";

export interface NativeEntry {
  readonly name: string;
  readonly path: string; // absolute Windows path
  readonly relativeParent: string; // '' for plain lists, '.'/'sub/dir' recursive
  readonly isDirectory: boolean;
  readonly size: number;
  readonly modified: number | null;
}

export type AppUpdateStatus =
  | "disabled"
  | "idle"
  | "checking"
  | "available"
  | "up-to-date"
  | "downloading"
  | "downloaded"
  | "installing"
  | "error";

export interface AppUpdateState {
  readonly status: AppUpdateStatus;
  readonly mode: "development" | "installed" | "portable";
  readonly currentVersion: string;
  readonly availableVersion: string | null;
  readonly progressPercent: number | null;
  readonly error: string | null;
}

export interface NativeFileOperationProgress {
  readonly id: string;
  readonly action: "copy" | "move" | "delete";
  readonly itemCount: number;
  readonly phase: "started" | "progress" | "cancelling" | "completed" | "failed";
  readonly pointsCurrent?: number;
  readonly pointsTotal?: number;
  readonly sizeCurrent?: number;
  readonly sizeTotal?: number;
  readonly itemsCurrent?: number;
  readonly itemsTotal?: number;
  readonly aborted?: boolean;
}

export interface NativePasteEntryInfo {
  readonly name: string;
  readonly isDirectory: boolean;
  readonly size: number;
  readonly modified: number | null;
}

export interface NativePasteItem extends NativePasteEntryInfo {
  readonly path: string;
  readonly conflict: (NativePasteEntryInfo & { readonly path: string }) | null;
}

export interface NativeImportInspection {
  readonly hasFiles: boolean;
  readonly items: NativePasteItem[];
}

export interface NativePasteInspection extends NativeImportInspection {
  readonly move: boolean;
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
  transfer(
    paths: string[],
    destinationPath: string,
    move: boolean,
    resolutions?: Readonly<Record<string, string>>,
  ): Promise<{ aborted: boolean }>;
  cancelFileOperation(operationId: string): Promise<boolean>;
  onFileOperationProgress(callback: (state: NativeFileOperationProgress) => void): () => void;
  writeFileClipboard(paths: string[], move: boolean): Promise<void>;
  inspectPasteFileClipboard(destinationPath: string): Promise<NativePasteInspection>;
  pasteFileClipboard(
    destinationPath: string,
    resolutions?: Readonly<Record<string, string>>,
  ): Promise<SystemClipboardPasteResult>;
  readFileClipboard(): Promise<FileClipboardState>;
  /** Resolves the OS path of a File dropped in from another app ("" if none). */
  pathForFile(file: File): string;
  inspectExternalImport(
    sourcePaths: string[],
    destinationPath: string,
  ): Promise<NativeImportInspection>;
  importExternalPaths(
    sourcePaths: string[],
    destinationPath: string,
    move: boolean,
    resolutions?: Readonly<Record<string, string>>,
  ): Promise<SystemImportResult>;
  undoNativePaste(): Promise<{ handled: boolean }>;
  redoNativePaste(): Promise<{ handled: boolean }>;
  restoreDeleted(paths: string[]): Promise<void>;
  readFile(filePath: string): Promise<ArrayBuffer>;
  thumbnail(filePath: string): Promise<string | null>;
  openPath(targetPath: string): Promise<boolean>;
  reveal(targetPath: string): Promise<boolean>;
  getUpdateState(): Promise<AppUpdateState>;
  checkForUpdates(): Promise<AppUpdateState>;
  downloadUpdate(): Promise<AppUpdateState>;
  installUpdate(): Promise<boolean>;
  openUpdatePage(): Promise<boolean>;
  onUpdateState(callback: (state: AppUpdateState) => void): () => void;
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
