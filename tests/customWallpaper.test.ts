import assert from "node:assert/strict";
import test from "node:test";

import { normalizeCustomWallpaper } from "../src/core/models.ts";

test("normalizeCustomWallpaper preserves a valid wallpaper reference", () => {
  assert.deepEqual(
    normalizeCustomWallpaper({
      url: "lumina-wallpaper://image/bg.png",
      name: "bg.png",
    }),
    {
      url: "lumina-wallpaper://image/bg.png",
      name: "bg.png",
    },
  );
});

test("normalizeCustomWallpaper rejects missing or malformed wallpaper data", () => {
  assert.equal(normalizeCustomWallpaper(undefined), null);
  assert.equal(normalizeCustomWallpaper({}), null);
  assert.equal(normalizeCustomWallpaper({ url: "", name: "bg.png" }), null);
  assert.equal(normalizeCustomWallpaper({ url: "lumina-wallpaper://image/bg.png", name: "" }), null);
});
