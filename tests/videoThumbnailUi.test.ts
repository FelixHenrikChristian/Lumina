import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const hook = readFileSync(new URL("../src/components/useThumbnail.ts", import.meta.url), "utf8");
const main = readFileSync(new URL("../electron/main.cjs", import.meta.url), "utf8");

test("video thumbnails use the native Shell bridge while images keep blob previews", () => {
  assert.match(hook, /file\.previewKind[^\n]*"image"/);
  assert.match(hook, /file\.previewKind === "video"/);
  assert.match(hook, /getThumbnail\(file\.path\)/);
  assert.match(main, /ipcMain\.handle\("lumina:thumbnail"/);
  assert.match(main, /assertAllowed\(filePath\)/);
});
