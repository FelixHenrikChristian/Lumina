import type { FileItem, FileSortOptions } from "./models";
import { extensionOf } from "./models";

const collator = new Intl.Collator(undefined, { sensitivity: "accent" });

function compareStrings(a: string, b: string): number {
  return collator.compare(a, b);
}

function typeKeyOf(item: FileItem): string {
  if (item.isDirectory) return "folder";
  const ext = extensionOf(item.name);
  return ext.startsWith(".") ? ext.slice(1) : ext;
}

/**
 * Port of FileSystemBrowserService.SortFileItems: directories always first;
 * Name sorts by display name then raw name; other fields tie-break by
 * display name then raw name ascending; recursive result sets additionally
 * tie-break by full path.
 */
export function sortFileItems(
  items: readonly FileItem[],
  sort: FileSortOptions,
  recursive: boolean,
): FileItem[] {
  const sign = sort.direction === "descending" ? -1 : 1;

  const fieldCompare = (a: FileItem, b: FileItem): number => {
    switch (sort.field) {
      case "name":
        return (
          compareStrings(a.displayName, b.displayName) ||
          compareStrings(a.name, b.name)
        );
      case "modified":
      case "created":
        return (a.modified ?? 0) - (b.modified ?? 0);
      case "size":
        return a.size - b.size;
      case "type":
        return compareStrings(typeKeyOf(a), typeKeyOf(b));
    }
  };

  return [...items].sort((a, b) => {
    if (a.isDirectory !== b.isDirectory) {
      return a.isDirectory ? -1 : 1;
    }
    const primary = fieldCompare(a, b) * sign;
    if (primary !== 0) return primary;
    if (sort.field !== "name") {
      const tie =
        compareStrings(a.displayName, b.displayName) ||
        compareStrings(a.name, b.name);
      if (tie !== 0) return tie;
    }
    return recursive ? compareStrings(a.path, b.path) : 0;
  });
}
