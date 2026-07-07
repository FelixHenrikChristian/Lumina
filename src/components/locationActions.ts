import { newId, type Location } from "../core/models";
import { useLumina } from "../state/store";
import { registerFsaBrowser } from "../state/store";
import {
  isFileSystemAccessSupported,
  saveLocationHandle,
} from "../fs/fsaFs";
import { isElectron, nativeApi } from "../fs/electronApi";
import { translate } from "../core/localization";

/** Adds the in-memory demo library location. */
export function addDemoLocation(): void {
  const state = useLumina.getState();
  const id = newId();
  const location: Location = {
    id,
    name: translate(state.language, "DemoLocation"),
    path: `loc:${id}`,
    kind: "demo",
  };
  state.addLocation(location);
}

/** True when this build can add real folders (desktop app or FSA browser). */
export function canAddRealFolder(): boolean {
  return isElectron() || isFileSystemAccessSupported();
}

/**
 * Opens the platform folder picker (native dialog in the desktop app,
 * File System Access picker in the browser) and registers the chosen
 * folder as a managed location. Returns false when dismissed.
 */
export async function addRealLocation(): Promise<boolean> {
  if (isElectron()) {
    const picked = await nativeApi().pickFolder();
    if (!picked) return false;
    const id = newId();
    useLumina.getState().addLocation({
      id,
      name: picked.name,
      path: `loc:${id}`,
      kind: "native",
      nativePath: picked.path,
    });
    return true;
  }

  if (!isFileSystemAccessSupported()) return false;
  let handle: FileSystemDirectoryHandle;
  try {
    handle = await window.showDirectoryPicker!({ mode: "readwrite" });
  } catch {
    return false; // user dismissed the picker
  }
  const id = newId();
  const location: Location = {
    id,
    name: handle.name,
    path: `loc:${id}`,
    kind: "fsa",
  };
  await saveLocationHandle(id, handle);
  registerFsaBrowser(location, handle);
  useLumina.getState().addLocation(location);
  return true;
}
