import type { FileItem } from "../core/models";
import { parentPathOf } from "../core/paths.ts";

export function searchResultParentPath(
  file: Pick<FileItem, "path" | "relativePath">,
  showParent: boolean,
): string | null {
  if (!showParent || !file.relativePath || file.relativePath === ".") return null;
  return parentPathOf(file.path);
}
