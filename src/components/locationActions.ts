import { newId, type Location } from "../core/models";
import { useLumina } from "../state/store";
import { registerFsaBrowser } from "../state/store";
import {
  isFileSystemAccessSupported,
  saveLocationHandle,
} from "../fs/fsaFs";
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

/**
 * Opens the browser directory picker and registers the chosen folder as a
 * managed location. Returns false when the picker was dismissed.
 */
export async function addFsaLocation(): Promise<boolean> {
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
