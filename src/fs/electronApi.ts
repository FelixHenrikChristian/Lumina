// Typed view of the API exposed by electron/preload.cjs. Present only when
// the app runs inside Electron.
export interface NativeEntry {
  readonly name: string;
  readonly path: string; // absolute Windows path
  readonly relativeParent: string; // '' for plain lists, '.'/'sub/dir' recursive
  readonly isDirectory: boolean;
  readonly size: number;
  readonly modified: number | null;
}

export interface LuminaNativeApi {
  pickFolder(): Promise<{ path: string; name: string } | null>;
  registerRoot(rootPath: string): Promise<boolean>;
  list(dirPath: string): Promise<NativeEntry[]>;
  listRecursive(rootPath: string): Promise<NativeEntry[]>;
  mkdir(dirPath: string): Promise<string>;
  rename(oldPath: string, newName: string): Promise<string>;
  trash(paths: string[]): Promise<void>;
  readFile(filePath: string): Promise<ArrayBuffer>;
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
