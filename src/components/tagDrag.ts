export const TAG_MIME = "application/x-lumina-tag";

/** Payload for dragging a tag (from the library or from a file card). */
export interface TagDragPayload {
  name: string;
  /** Set when the chip was dragged off a file card. */
  sourcePath?: string;
}

export function readTagDrag(dataTransfer: DataTransfer): TagDragPayload | null {
  const raw = dataTransfer.getData(TAG_MIME);
  if (!raw) return null;
  try {
    const parsed = JSON.parse(raw) as TagDragPayload;
    return parsed.name ? parsed : null;
  } catch {
    return null;
  }
}

export function writeTagDrag(dataTransfer: DataTransfer, payload: TagDragPayload): void {
  dataTransfer.setData(TAG_MIME, JSON.stringify(payload));
  dataTransfer.effectAllowed = "copy";
}

export function hasTagDrag(dataTransfer: DataTransfer): boolean {
  return dataTransfer.types.includes(TAG_MIME);
}

// dataTransfer payloads are unreadable during dragover, so the in-app drag
// source also mirrors the payload here for live insertion previews.
let activeDrag: TagDragPayload | null = null;

export function beginTagDrag(dataTransfer: DataTransfer, payload: TagDragPayload): void {
  writeTagDrag(dataTransfer, payload);
  activeDrag = payload;
}

export function endTagDrag(): void {
  activeDrag = null;
}

export function getActiveTagDrag(): TagDragPayload | null {
  return activeDrag;
}
