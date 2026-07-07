// Port of Lumina.Core TagParserService. Filenames carry tags as a
// leading bracket group: "[tag1 tag2] name.ext". Tags are space-separated
// and matched case-insensitively; the bracket group cannot contain "]".
const LEADING_TAGS_RE = /^\[([^\]]+)\]\s*/;

export function parseTagsFromFilename(filename: string): string[] {
  const match = LEADING_TAGS_RE.exec(filename);
  if (!match) {
    return [];
  }
  return match[1]
    .split(" ")
    .map((tag) => tag.trim())
    .filter((tag) => tag.length > 0);
}

export function getDisplayName(filename: string): string {
  return filename.replace(LEADING_TAGS_RE, "").replace(/^\s+/, "");
}

export function getExtension(filename: string): string {
  const displayName = getDisplayName(filename);
  const dot = displayName.lastIndexOf(".");
  // Matches .NET Path.GetExtension: no extension for names starting
  // with the only dot ("." itself yields empty too).
  if (dot < 0 || dot === displayName.length - 1) {
    return "";
  }
  return displayName.slice(dot);
}

export function getDisplayNameWithoutExtension(filename: string): string {
  const displayName = getDisplayName(filename);
  const extension = getExtension(filename);
  return extension.length === 0
    ? displayName
    : displayName.slice(0, displayName.length - extension.length);
}

function sameTag(a: string, b: string): boolean {
  return a.toLowerCase() === b.toLowerCase();
}

export function insertTagIntoFilename(
  filename: string,
  tag: string,
  insertionIndex: number,
): string {
  const normalizedTag = tag.trim();
  if (normalizedTag.length === 0) {
    throw new Error("Tag must not be empty");
  }
  const tags = parseTagsFromFilename(filename).filter(
    (existing) => !sameTag(existing, normalizedTag),
  );
  const targetIndex = Math.min(Math.max(insertionIndex, 0), tags.length);
  tags.splice(targetIndex, 0, normalizedTag);
  return `[${tags.join(" ")}] ${getDisplayName(filename)}`;
}

export function removeTagFromFilename(filename: string, tag: string): string {
  const normalizedTag = tag.trim();
  if (normalizedTag.length === 0) {
    throw new Error("Tag must not be empty");
  }
  const tags = parseTagsFromFilename(filename);
  if (!tags.some((existing) => sameTag(existing, normalizedTag))) {
    return filename;
  }
  const remaining = tags.filter((existing) => !sameTag(existing, normalizedTag));
  const displayName = getDisplayName(filename);
  return remaining.length === 0
    ? displayName
    : `[${remaining.join(" ")}] ${displayName}`;
}
