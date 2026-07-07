import { useRef, useState } from "react";
import { tagStyleFor, useLumina, useT } from "../state/store";
import type { Tag, TagGroup } from "../core/models";
import {
  DEFAULT_TAG_COLOR,
  contrastColorFor,
  cssColorFor,
  isValidHexColor,
  newId,
} from "../core/models";
import { exportTagLibrary, importTagLibrary } from "../core/tagTransfer";
import { beginTagDrag, endTagDrag } from "./tagDrag";
import { ConfirmDialog, GlassDialog, useOverlay } from "./overlays";
import {
  ChevronDownIcon,
  ChevronRightIcon,
  EditIcon,
  ExportIcon,
  ImportIcon,
  MoreIcon,
  PlusIcon,
  TrashIcon,
} from "./icons";

/** Coerces stored colors (6- or 9-digit hex) into the #rrggbb form <input type=color> needs. */
function colorInputValue(hex: string | null | undefined): string {
  if (!isValidHexColor(hex)) return DEFAULT_TAG_COLOR;
  return hex.length === 9 ? `#${hex.slice(3)}` : hex;
}

interface TagDraft {
  groupId: string;
  tag: Tag | null; // null => creating
  defaultColor: string;
}

export function TagSidebar() {
  const t = useT();
  const tagGroups = useLumina((s) => s.tagGroups);
  const tagStyles = useLumina((s) => s.tagStyles);
  const selectedFilterIds = useLumina((s) => s.selectedTagFilterIds);
  const toggleTagFilter = useLumina((s) => s.toggleTagFilter);
  const saveTagLibrary = useLumina((s) => s.saveTagLibrary);
  const upsertGroup = useLumina((s) => s.upsertGroup);
  const deleteGroup = useLumina((s) => s.deleteGroup);
  const upsertTag = useLumina((s) => s.upsertTag);
  const deleteTag = useLumina((s) => s.deleteTag);
  const clearTagLibrary = useLumina((s) => s.clearTagLibrary);
  const { openMenu } = useOverlay();

  const [collapsed, setCollapsed] = useState<Set<string>>(new Set());
  const [groupDraft, setGroupDraft] = useState<TagGroup | "new" | null>(null);
  const [tagDraft, setTagDraft] = useState<TagDraft | null>(null);
  const [confirmClear, setConfirmClear] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  const importInputRef = useRef<HTMLInputElement | null>(null);
  const noticeTimer = useRef<number | undefined>(undefined);

  const showNotice = (text: string) => {
    setNotice(text);
    window.clearTimeout(noticeTimer.current);
    noticeTimer.current = window.setTimeout(() => setNotice(null), 6000);
  };

  const toggleCollapsed = (groupId: string) => {
    const next = new Set(collapsed);
    if (next.has(groupId)) next.delete(groupId);
    else next.add(groupId);
    setCollapsed(next);
  };

  const groupMenu = (group: TagGroup) => [
    {
      key: "add-tag",
      label: t("AddTag"),
      icon: <PlusIcon />,
      onSelect: () =>
        setTagDraft({ groupId: group.id, tag: null, defaultColor: group.defaultColor }),
    },
    {
      key: "edit",
      label: t("EditGroup"),
      icon: <EditIcon />,
      onSelect: () => setGroupDraft(group),
    },
    {
      key: "delete",
      label: t("DeleteGroup"),
      icon: <TrashIcon />,
      danger: true,
      separatorAbove: true,
      onSelect: () => deleteGroup(group.id),
    },
  ];

  const tagMenu = (group: TagGroup, tag: Tag) => [
    {
      key: "edit",
      label: t("EditTag"),
      icon: <EditIcon />,
      onSelect: () =>
        setTagDraft({ groupId: group.id, tag, defaultColor: group.defaultColor }),
    },
    {
      key: "delete",
      label: t("DeleteTag"),
      icon: <TrashIcon />,
      danger: true,
      onSelect: () => deleteTag(group.id, tag.id),
    },
  ];

  const exportLibrary = () => {
    const blob = new Blob([exportTagLibrary(tagGroups)], { type: "application/json" });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = "lumina-tags.json";
    anchor.click();
    URL.revokeObjectURL(url);
  };

  const importLibrary = async (fileList: FileList | null) => {
    const file = fileList?.[0];
    if (!file) return;
    try {
      const result = importTagLibrary(await file.text());
      saveTagLibrary(result.groups);
      showNotice(t("ImportResult", result.tagGroupCount, result.tagCount, result.sourceFormat));
    } catch (error) {
      showNotice(t("ImportFailed", error instanceof Error ? error.message : String(error)));
    }
  };

  return (
    <div className="sidebar-pane">
      <header className="sidebar-header">
        <h2>{t("Tags")}</h2>
        <div className="sidebar-actions">
          <button
            type="button"
            className="lg-button icon-only"
            title={t("AddTagGroup")}
            onClick={() => setGroupDraft("new")}
          >
            <PlusIcon />
          </button>
        </div>
      </header>

      <div className="sidebar-scroll">
        {tagGroups.length === 0 && (
          <div className="sidebar-empty">
            <p>{t("TagsEmpty")}</p>
            <p className="sidebar-empty-hint">{t("TagsEmptyHint")}</p>
          </div>
        )}
        {tagGroups.map((group) => {
          const isCollapsed = collapsed.has(group.id);
          return (
            <section key={group.id} className="tag-group">
              <div
                className="tag-group-header"
                role="button"
                tabIndex={0}
                onClick={() => toggleCollapsed(group.id)}
                onKeyDown={(e) => {
                  if (e.key === "Enter" || e.key === " ") toggleCollapsed(group.id);
                }}
                onContextMenu={(e) => {
                  e.preventDefault();
                  openMenu(e.clientX, e.clientY, groupMenu(group));
                }}
              >
                {isCollapsed ? <ChevronRightIcon size={14} /> : <ChevronDownIcon size={14} />}
                <span
                  className="tag-group-swatch"
                  style={{ background: cssColorFor(group.defaultColor) }}
                />
                <span className="tag-group-name" title={group.description ?? group.name}>
                  {group.name}
                </span>
                <span className="tag-group-count">{group.tags.length}</span>
                <button
                  type="button"
                  className="row-more"
                  title={t("EditGroup")}
                  onClick={(e) => {
                    e.stopPropagation();
                    const rect = e.currentTarget.getBoundingClientRect();
                    openMenu(rect.left, rect.bottom + 4, groupMenu(group));
                  }}
                >
                  <MoreIcon />
                </button>
              </div>
              {!isCollapsed && (
                <div className="tag-chips">
                  {group.tags.length === 0 && (
                    <span className="tag-group-empty">{t("GroupEmpty")}</span>
                  )}
                  {group.tags.map((tag) => {
                    const style = tagStyleFor(tagStyles, tag.name);
                    const filtered = selectedFilterIds.has(tag.id);
                    return (
                      <button
                        key={tag.id}
                        type="button"
                        className={`tag-chip${filtered ? " is-filtered" : ""}`}
                        style={{
                          background: cssColorFor(style.color),
                          color: style.textColor,
                        }}
                        draggable
                        title={tag.name}
                        onClick={() => void toggleTagFilter(tag.id)}
                        onContextMenu={(e) => {
                          e.preventDefault();
                          openMenu(e.clientX, e.clientY, tagMenu(group, tag));
                        }}
                        onDragStart={(e) => beginTagDrag(e.dataTransfer, { name: tag.name })}
                        onDragEnd={endTagDrag}
                      >
                        {tag.name}
                      </button>
                    );
                  })}
                  <button
                    type="button"
                    className="tag-chip tag-chip-add"
                    title={t("AddTag")}
                    onClick={() =>
                      setTagDraft({ groupId: group.id, tag: null, defaultColor: group.defaultColor })
                    }
                  >
                    <PlusIcon size={11} />
                  </button>
                </div>
              )}
            </section>
          );
        })}
      </div>

      <footer className="sidebar-footer">
        <button type="button" className="lg-chip" onClick={exportLibrary} disabled={tagGroups.length === 0}>
          <ExportIcon size={12} />
          {t("ExportTags")}
        </button>
        <button type="button" className="lg-chip" onClick={() => importInputRef.current?.click()}>
          <ImportIcon size={12} />
          {t("ImportTags")}
        </button>
        {tagGroups.length > 0 && (
          <button type="button" className="lg-chip" onClick={() => setConfirmClear(true)}>
            <TrashIcon size={12} />
            {t("ClearTags")}
          </button>
        )}
        <input
          ref={importInputRef}
          type="file"
          accept="application/json,.json"
          hidden
          onChange={(e) => {
            void importLibrary(e.currentTarget.files);
            e.currentTarget.value = "";
          }}
        />
      </footer>
      {notice && <p className="sidebar-notice">{notice}</p>}

      {groupDraft && (
        <GroupEditDialog
          group={groupDraft === "new" ? null : groupDraft}
          onSave={(group) => {
            upsertGroup(group);
            setGroupDraft(null);
          }}
          onCancel={() => setGroupDraft(null)}
        />
      )}
      {tagDraft && (
        <TagEditDialog
          draft={tagDraft}
          onSave={(tag) => {
            upsertTag(tagDraft.groupId, tag);
            setTagDraft(null);
          }}
          onCancel={() => setTagDraft(null)}
        />
      )}
      {confirmClear && (
        <ConfirmDialog
          title={t("ClearTags")}
          message={t("ClearTagsConfirm")}
          confirmLabel={t("ClearTags")}
          cancelLabel={t("Cancel")}
          danger
          onConfirm={() => {
            clearTagLibrary();
            setConfirmClear(false);
          }}
          onCancel={() => setConfirmClear(false)}
        />
      )}
    </div>
  );
}

function GroupEditDialog({
  group,
  onSave,
  onCancel,
}: {
  group: TagGroup | null;
  onSave(group: TagGroup): void;
  onCancel(): void;
}) {
  const t = useT();
  const [name, setName] = useState(group?.name ?? "");
  const [color, setColor] = useState(colorInputValue(group?.defaultColor));
  const [description, setDescription] = useState(group?.description ?? "");

  const commit = () => {
    onSave({
      id: group?.id ?? newId(),
      name: name.trim() || t("UntitledGroup"),
      defaultColor: color,
      defaultTextColor: contrastColorFor(color),
      description: description.trim() || null,
      tags: group?.tags ?? [],
    });
  };

  return (
    <GlassDialog title={group ? t("EditGroup") : t("AddTagGroup")} onDismiss={onCancel}>
      <div className="lg-form" onKeyDown={(e) => e.key === "Enter" && commit()}>
        <label className="lg-field">
          <span>{t("Name")}</span>
          <input
            className="lg-input"
            value={name}
            autoFocus
            onChange={(e) => setName(e.currentTarget.value)}
          />
        </label>
        <label className="lg-field">
          <span>{t("Color")}</span>
          <input
            className="lg-color"
            type="color"
            value={color}
            onChange={(e) => setColor(e.currentTarget.value)}
          />
        </label>
        <label className="lg-field">
          <span>{t("Description")}</span>
          <input
            className="lg-input"
            value={description}
            onChange={(e) => setDescription(e.currentTarget.value)}
          />
        </label>
      </div>
      <div className="lg-dialog-actions">
        <button type="button" className="lg-button" onClick={onCancel}>
          {t("Cancel")}
        </button>
        <button type="button" className="lg-button is-primary" onClick={commit}>
          {t("Save")}
        </button>
      </div>
    </GlassDialog>
  );
}

function TagEditDialog({
  draft,
  onSave,
  onCancel,
}: {
  draft: TagDraft;
  onSave(tag: Tag): void;
  onCancel(): void;
}) {
  const t = useT();
  const [name, setName] = useState(draft.tag?.name ?? "");
  const [color, setColor] = useState(colorInputValue(draft.tag?.color ?? draft.defaultColor));

  const commit = () => {
    // Tag names live inside the filename bracket group: spaces separate
    // tags and "]" closes the group, so both are stripped.
    const cleaned = name.trim().replace(/[\s\]]+/g, "-");
    onSave({
      id: draft.tag?.id ?? newId(),
      name: cleaned || t("UntitledTag"),
      color,
      textColor: contrastColorFor(color),
      groupId: draft.groupId,
    });
  };

  return (
    <GlassDialog title={draft.tag ? t("EditTag") : t("AddTag")} onDismiss={onCancel} width={360}>
      <div className="lg-form" onKeyDown={(e) => e.key === "Enter" && commit()}>
        <label className="lg-field">
          <span>{t("Name")}</span>
          <input
            className="lg-input"
            value={name}
            autoFocus
            onChange={(e) => setName(e.currentTarget.value)}
          />
        </label>
        <label className="lg-field">
          <span>{t("Color")}</span>
          <input
            className="lg-color"
            type="color"
            value={color}
            onChange={(e) => setColor(e.currentTarget.value)}
          />
        </label>
      </div>
      <div className="lg-dialog-actions">
        <button type="button" className="lg-button" onClick={onCancel}>
          {t("Cancel")}
        </button>
        <button type="button" className="lg-button is-primary" onClick={commit}>
          {t("Save")}
        </button>
      </div>
    </GlassDialog>
  );
}
