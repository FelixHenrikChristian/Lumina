import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { createRequire } from "node:module";
import { dirname, join } from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const read = (relativePath: string) => readFileSync(join(root, relativePath), "utf8");
const require = createRequire(import.meta.url);
const { normalizeResolutions, planTransferBatches } = require(
  join(root, "electron", "fileOperationPlanning.cjs"),
);

test("normalizeResolutions keeps only valid actions and lower-cases keys", () => {
  assert.deepEqual(
    normalizeResolutions({
      "C:\\A\\File.txt": "replace",
      "c:\\a\\other.txt": "skip",
      "c:\\a\\third.txt": "keepBoth",
      "c:\\a\\bad.txt": "explode",
      42: "replace",
    }),
    {
      "c:\\a\\file.txt": "replace",
      "c:\\a\\other.txt": "skip",
      "c:\\a\\third.txt": "keepBoth",
      "42": "replace",
    },
  );
  assert.deepEqual(normalizeResolutions(null), {});
  assert.deepEqual(normalizeResolutions("nope"), {});
});

test("planTransferBatches drops skipped items and batches keep-both separately", () => {
  const batches = planTransferBatches(
    ["C:\\src\\a.txt", "C:\\src\\b.txt", "C:\\src\\c.txt"],
    "C:\\dst",
    false,
    { "c:\\src\\a.txt": "skip", "c:\\src\\b.txt": "keepBoth", "c:\\src\\c.txt": "replace" },
  );
  assert.deepEqual(batches, [
    { action: "copy", sources: ["C:\\src\\b.txt"], renameOnCollision: true },
    { action: "copy", sources: ["C:\\src\\c.txt"], renameOnCollision: false },
  ]);
});

test("planTransferBatches auto-renames copies pasted into their own folder", () => {
  const batches = planTransferBatches(
    ["C:\\dst\\same.txt", "C:\\other\\normal.txt"],
    "C:\\dst",
    false,
    {},
  );
  assert.deepEqual(batches, [
    { action: "copy", sources: ["C:\\dst\\same.txt"], renameOnCollision: true },
    { action: "copy", sources: ["C:\\other\\normal.txt"], renameOnCollision: false },
  ]);
});

test("planTransferBatches never auto-renames moves within the same folder", () => {
  const batches = planTransferBatches(["C:\\dst\\same.txt"], "C:\\dst", true, {});
  assert.deepEqual(batches, [
    { action: "move", sources: ["C:\\dst\\same.txt"], renameOnCollision: false },
  ]);
});

test("Shell-owned dialogs are suppressed on every native operation path", () => {
  const helper = read("electron/windows-shell/WindowsFileOperation.cs");
  const main = read("electron/main.cjs");

  // Error popups are the Shell's unless NoErrorUi is always set.
  assert.match(helper, /NoErrorUi = 0x0400/);
  assert.match(helper, /FileOperationFlags\.NoErrorUi/);
  // The progress bridge replaces the Shell progress window on every run.
  assert.match(helper, /SetProgressDialog\(new LuminaProgressBridge/);
  assert.match(main, /reportProgress: true/);
  // Deletes confirm in Lumina's dialog, so the Shell must not ask again.
  assert.match(main, /"lumina:trash"[\s\S]*?noConfirmation: true/);
  assert.match(main, /"lumina:deletePermanently"[\s\S]*?permanent: true,\s*noConfirmation: true/);
  // Transfers/pastes resolve conflicts in Lumina's dialog first.
  assert.match(main, /"lumina:transfer"[\s\S]*?noConfirmation: true/);
  assert.match(main, /"lumina:pasteFileClipboard"[\s\S]*?noConfirmation: true/);
});

test("progress and cancellation are bridged into the themed dialog", () => {
  const helper = read("electron/windows-shell/WindowsFileOperation.cs");
  const runner = read("electron/windows-shell/run-file-operation.ps1");
  const main = read("electron/main.cjs");
  const preload = read("electron/preload.cjs");
  const explorer = read("src/components/FileExplorer.tsx");

  assert.match(helper, /IOperationsProgressDialog/);
  assert.match(helper, /StatusCancelled/);
  assert.match(helper, /LUMINA_FILE_PROGRESS/);
  assert.match(runner, /LUMINA_FILE_\$\{Kind\}/);
  assert.match(main, /lumina:fileOperationProgress/);
  assert.match(main, /lumina:cancelFileOperation/);
  assert.match(preload, /onFileOperationProgress/);
  assert.match(preload, /cancelFileOperation/);
  assert.match(explorer, /FileOperationProgressDialog/);
  assert.match(explorer, /cancelFileOperation/);
});

test("cancellation HRESULTs surface as aborted results, not IPC errors", () => {
  const helper = read("electron/windows-shell/WindowsFileOperation.cs");
  assert.match(helper, /ErrorCancelled = unchecked\(\(int\)0x800704C7\)/);
  assert.match(helper, /return new FileOperationResult \{ Aborted = true \}/);
});

test("native pastes inspect conflicts before running", () => {
  const main = read("electron/main.cjs");
  const explorer = read("src/components/FileExplorer.tsx");
  assert.match(main, /lumina:inspectPasteFileClipboard/);
  assert.match(explorer, /inspectSystemClipboardPaste/);
  assert.match(explorer, /kind: "systemPaste"/);
});

test("conflict and progress strings are localized in both languages", () => {
  const source = read("src/core/localization.ts");
  for (const key of [
    "FileOperationCopyTitle",
    "FileOperationCancelling",
    "FileConflictTitle",
    "FileConflictMessage",
    "ConflictSkip",
    "ConflictKeepBoth",
    "ConflictReplace",
    "PasteSameFolderMessage",
    "UnknownDate",
  ]) {
    const occurrences = source.split(`${key}:`).length - 1;
    assert.equal(occurrences, 2, `${key} must exist in both en and zh dictionaries`);
  }
  // The conflict dialog itself must not carry hardcoded UI strings.
  const explorer = read("src/components/FileExplorer.tsx");
  assert.doesNotMatch(explorer, /文件冲突|保留两个|粘贴已中断/);
});
