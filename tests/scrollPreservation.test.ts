import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const store = readFileSync(new URL("../src/state/store.ts", import.meta.url), "utf8");
const explorer = readFileSync(
  new URL("../src/components/FileExplorer.tsx", import.meta.url),
  "utf8",
);

test("same-directory reloads keep the file list mounted so scroll survives", () => {
  // loadInto only clears the grid when the caller does not opt into keeping
  // the current entries; collapsing the grid clamps scrollTop back to 0.
  assert.match(store, /loadInto = async \(path: string, options\?: \{ keepFiles\?: boolean \}\)/);
  assert.match(store, /if \(options\?\.keepFiles\) set\(\{ isBusy: true, errorMessage: null \}\);/);
  assert.match(store, /else set\(\{ isBusy: true, errorMessage: null, files: \[\] \}\);/);
});

test("refresh and post-operation reloads opt into keeping files", () => {
  const keepers = store.match(/loadInto\(get\(\)\.currentPath, \{ keepFiles: true \}\)/g) ?? [];
  // reloadAndSelect (rename/tag drop/transfer/paste/import reveal), refresh
  // (F5 + watcher), deleteSelected, importExternalPaths (Explorer drop onto
  // a folder card), undo, redo.
  assert.equal(keepers.length, 6);
});

test("navigation still clears the grid so stale entries never flash", () => {
  // openDirectory/back/forward load the target path without keepFiles.
  assert.match(store, /await loadInto\(target\);/);
  assert.match(store, /await loadInto\(candidate\);/);
});

test("the explorer reveals the focused card after reloads", () => {
  assert.match(explorer, /const focusedPath = useLumina\(\(s\) => s\.focusedPath\);/);
  assert.match(
    explorer,
    /\[data-path="\$\{CSS\.escape\(focusedPath\)\}"\]/,
  );
  assert.match(explorer, /scrollIntoView\(\{ block: "nearest" \}\);\s*\}, \[focusedPath\]\);/);
});

test("the loading row only renders into an empty grid", () => {
  // With kept files, an appended 200px loading row would shift the layout.
  assert.match(explorer, /\{isBusy && files\.length === 0 && \(/);
});
