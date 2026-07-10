import { useEffect, useRef, useState } from "react";
import type { FileItem } from "../core/models";
import { useLumina } from "../state/store";

/**
 * Lazy image thumbnail: returns [ref, url]. Put the ref on the card's
 * preview container — the blob is fetched once it scrolls near the
 * viewport, and the object URL is revoked on unmount or file change.
 */
export function useLazyThumbnail(
  file: FileItem,
): [React.RefObject<HTMLDivElement | null>, string | null] {
  const getBlob = useLumina((s) => s.getBlob);
  const getThumbnail = useLumina((s) => s.getThumbnail);
  const ref = useRef<HTMLDivElement | null>(null);
  const [url, setUrl] = useState<string | null>(null);

  useEffect(() => {
    setUrl(null);
    if (file.previewKind === "none") return;
    const el = ref.current;
    if (!el) return;

    let cancelled = false;
    let objectUrl: string | null = null;

    const observer = new IntersectionObserver(
      (entries) => {
        if (entries.some((e) => e.isIntersecting)) {
          observer.disconnect();
          if (file.previewKind === "image") {
            void getBlob(file.path).then((blob) => {
              if (cancelled || !blob) return;
              objectUrl = URL.createObjectURL(blob);
              setUrl(objectUrl);
            });
          } else if (file.previewKind === "video") {
            void getThumbnail(file.path).then((thumbnail) => {
              if (!cancelled && thumbnail) setUrl(thumbnail);
            });
          }
        }
      },
      { rootMargin: "300px" },
    );
    observer.observe(el);

    return () => {
      cancelled = true;
      observer.disconnect();
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [file.path, file.name, file.previewKind, getBlob, getThumbnail]);

  return [ref, url];
}
