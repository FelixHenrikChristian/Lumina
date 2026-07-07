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
  const ref = useRef<HTMLDivElement | null>(null);
  const [url, setUrl] = useState<string | null>(null);

  useEffect(() => {
    setUrl(null);
    if (file.previewKind !== "image") return;
    const el = ref.current;
    if (!el) return;

    let cancelled = false;
    let objectUrl: string | null = null;

    const observer = new IntersectionObserver(
      (entries) => {
        if (entries.some((e) => e.isIntersecting)) {
          observer.disconnect();
          void getBlob(file.path).then((blob) => {
            if (cancelled || !blob) return;
            objectUrl = URL.createObjectURL(blob);
            setUrl(objectUrl);
          });
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
  }, [file.path, file.name, file.previewKind, getBlob]);

  return [ref, url];
}
