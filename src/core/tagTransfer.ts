import type { Tag, TagGroup } from "./models";
import {
  DEFAULT_TAG_COLOR,
  contrastColorFor,
  isValidHexColor,
  newId,
} from "./models";

// Port of JsonTagGroupTransferService: TagSpaces-compatible portable JSON
// plus the native TagGroup[] array format. Import is a full replace.
// Property names 'textcolor', 'created_date', 'modified_date' are
// load-bearing for TagSpaces interop.
const APP_NAME = "Lumina";
const APP_VERSION = "1.0.0";
const SETTINGS_VERSION = 1;

export interface TagGroupImportResult {
  readonly sourceFormat: string;
  readonly tagGroupCount: number;
  readonly tagCount: number;
  readonly groups: TagGroup[];
}

interface PortableTag {
  id?: string;
  title?: string;
  color?: string;
  textcolor?: string;
}

interface PortableGroup {
  title?: string;
  uuid?: string;
  children?: PortableTag[];
  color?: string;
  textcolor?: string;
  description?: string | null;
}

export function exportTagLibrary(groups: readonly TagGroup[]): string {
  const exportedAt = new Date();
  const unixMs = exportedAt.getTime();
  return JSON.stringify(
    {
      appName: APP_NAME,
      appVersion: APP_VERSION,
      settingsVersion: SETTINGS_VERSION,
      exportedAt: exportedAt.toISOString(),
      tagGroups: groups.map((group) => ({
        title: group.name,
        uuid: group.id,
        children: group.tags.map((tag) => ({
          id: tag.id,
          title: tag.name,
          color: tag.color,
          textcolor: tag.textColor ?? contrastColorFor(tag.color),
        })),
        created_date: unixMs,
        color: group.defaultColor,
        textcolor: group.defaultTextColor ?? contrastColorFor(group.defaultColor),
        modified_date: unixMs,
        expanded: true,
        description: group.description,
      })),
    },
    null,
    2,
  );
}

function normalizeTag(raw: Partial<Tag>, groupId: string): Tag {
  return {
    id: raw.id?.trim() || newId(),
    name: raw.name?.trim() || "Untitled tag",
    color: isValidHexColor(raw.color) ? raw.color : DEFAULT_TAG_COLOR,
    textColor: isValidHexColor(raw.textColor) ? raw.textColor : "#ffffff",
    groupId,
  };
}

function normalizeGroup(raw: Partial<TagGroup>): TagGroup {
  const id = raw.id?.trim() || newId();
  return {
    id,
    name: raw.name?.trim() || "Untitled group",
    defaultColor: isValidHexColor(raw.defaultColor) ? raw.defaultColor : DEFAULT_TAG_COLOR,
    defaultTextColor: isValidHexColor(raw.defaultTextColor) ? raw.defaultTextColor : "#ffffff",
    description: raw.description?.trim() || null,
    tags: (raw.tags ?? []).map((tag) => normalizeTag(tag, id)),
  };
}

function fromPortableGroup(raw: PortableGroup): TagGroup {
  const id = raw.uuid?.trim() || newId();
  return normalizeGroup({
    id,
    name: raw.title,
    defaultColor: raw.color,
    defaultTextColor: raw.textcolor,
    description: raw.description ?? null,
    tags: (raw.children ?? []).map((child) => ({
      id: child.id,
      name: child.title,
      color: child.color,
      textColor: child.textcolor,
      groupId: id,
    })) as Tag[],
  });
}

export function importTagLibrary(json: string): TagGroupImportResult {
  const root: unknown = JSON.parse(json);

  if (Array.isArray(root)) {
    const groups = root.map((g) => normalizeGroup(g as Partial<TagGroup>));
    return summarize("Lumina tag groups", groups);
  }

  if (typeof root !== "object" || root === null) {
    throw new Error("The tag library file must be a JSON object or tag group array.");
  }

  const record = root as { appName?: string; tagGroups?: unknown };
  if (!Array.isArray(record.tagGroups)) {
    throw new Error("The tag library file does not contain a tagGroups array.");
  }

  const firstObject = record.tagGroups.find(
    (entry) => typeof entry === "object" && entry !== null,
  ) as Record<string, unknown> | undefined;
  const isPortable =
    firstObject === undefined ||
    "title" in firstObject ||
    "uuid" in firstObject ||
    "children" in firstObject;

  const groups = isPortable
    ? record.tagGroups.map((g) => fromPortableGroup(g as PortableGroup))
    : record.tagGroups.map((g) => normalizeGroup(g as Partial<TagGroup>));

  const sourceFormat = record.appName?.trim() || "TagSpaces";
  return summarize(sourceFormat, groups);
}

function summarize(sourceFormat: string, groups: TagGroup[]): TagGroupImportResult {
  return {
    sourceFormat,
    tagGroupCount: groups.length,
    tagCount: groups.reduce((sum, group) => sum + group.tags.length, 0),
    groups,
  };
}
