import assert from "node:assert/strict";
import test from "node:test";
import { thumbnailDataUrlForPath } from "../electron/thumbnail.cjs";

test("thumbnailDataUrlForPath returns a PNG data URL for a non-empty shell thumbnail", async () => {
  const result = await thumbnailDataUrlForPath("C:\\Videos\\clip.mp4", {
    createThumbnailFromPath: async (path, size) => {
      assert.equal(path, "C:\\Videos\\clip.mp4");
      assert.deepEqual(size, { width: 640, height: 360 });
      return { isEmpty: () => false, toDataURL: () => "data:image/png;base64,thumbnail" };
    },
  });

  assert.equal(result, "data:image/png;base64,thumbnail");
});

test("thumbnailDataUrlForPath returns null for an empty or failed shell thumbnail", async () => {
  const empty = await thumbnailDataUrlForPath("C:\\Videos\\empty.mp4", {
    createThumbnailFromPath: async () => ({ isEmpty: () => true, toDataURL: () => "unused" }),
  });
  const failed = await thumbnailDataUrlForPath("C:\\Videos\\broken.mp4", {
    createThumbnailFromPath: async () => {
      throw new Error("unsupported");
    },
  });

  assert.equal(empty, null);
  assert.equal(failed, null);
});
