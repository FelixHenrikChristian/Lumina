import assert from "node:assert/strict";
import { EventEmitter } from "node:events";
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

import updaterModule from "../electron/updater.cjs";

const {
  CHECK_INTERVAL_MS,
  RELEASES_URL,
  STARTUP_CHECK_DELAY_MS,
  createUpdateController,
  errorMessage,
  progressPercent,
} = updaterModule;
const root = dirname(dirname(fileURLToPath(import.meta.url)));
const read = (relativePath: string) => readFileSync(join(root, relativePath), "utf8");

class FakeAutoUpdater extends EventEmitter {
  autoDownload = true;
  autoInstallOnAppQuit = true;
  autoRunAppAfterInstall = false;
  allowDowngrade = true;
  allowPrerelease = true;
  disableWebInstaller = false;
  checkCalls = 0;
  downloadCalls = 0;
  quitArguments: [boolean, boolean] | null = null;

  async checkForUpdates() {
    this.checkCalls += 1;
    this.emit("checking-for-update");
    this.emit("update-available", { version: "1.1.0" });
  }

  async downloadUpdate() {
    this.downloadCalls += 1;
    this.emit("download-progress", { percent: 42.5 });
    this.emit("update-downloaded", { version: "1.1.0" });
  }

  quitAndInstall(isSilent: boolean, isForceRunAfter: boolean) {
    this.quitArguments = [isSilent, isForceRunAfter];
  }
}

function harness(mode: "development" | "installed" | "portable") {
  const handlers = new Map<string, (...args: never[]) => unknown>();
  const openedUrls: string[] = [];
  const autoUpdater = new FakeAutoUpdater();
  const controller = createUpdateController({
    app: {
      isPackaged: mode !== "development",
      getVersion: () => "1.0.0",
    },
    BrowserWindow: { getAllWindows: () => [] },
    ipcMain: {
      handle: (channel: string, handler: (...args: never[]) => unknown) => {
        handlers.set(channel, handler);
      },
    },
    shell: {
      openExternal: async (url: string) => {
        openedUrls.push(url);
      },
    },
    autoUpdater,
    env: mode === "portable" ? { PORTABLE_EXECUTABLE_FILE: "Lumina-Portable.exe" } : {},
  });
  controller.registerIpc();
  const invoke = async (channel: string) => {
    const handler = handlers.get(channel);
    assert.ok(handler, `missing IPC handler ${channel}`);
    return handler();
  };
  return { autoUpdater, controller, invoke, openedUrls };
}

test("installed builds check, download, and explicitly restart into the NSIS update", async () => {
  const { autoUpdater, invoke, openedUrls } = harness("installed");

  assert.deepEqual(await invoke("lumina:getUpdateState"), {
    status: "idle",
    mode: "installed",
    currentVersion: "1.0.0",
    availableVersion: null,
    progressPercent: null,
    error: null,
  });
  assert.equal(autoUpdater.autoDownload, false);
  assert.equal(autoUpdater.autoInstallOnAppQuit, false);
  assert.equal(autoUpdater.allowDowngrade, false);
  assert.equal(autoUpdater.allowPrerelease, false);
  assert.equal(autoUpdater.disableWebInstaller, true);

  const available = await invoke("lumina:checkForUpdates");
  assert.equal(available.status, "available");
  assert.equal(available.availableVersion, "1.1.0");

  const downloaded = await invoke("lumina:downloadUpdate");
  assert.equal(downloaded.status, "downloaded");
  assert.equal(downloaded.progressPercent, 100);
  assert.equal(autoUpdater.downloadCalls, 1);

  assert.equal(await invoke("lumina:installUpdate"), true);
  await new Promise((resolve) => setImmediate(resolve));
  assert.deepEqual(autoUpdater.quitArguments, [false, true]);
  assert.deepEqual(openedUrls, []);
});

test("portable builds check for a version but always hand downloads to the release page", async () => {
  const { autoUpdater, invoke, openedUrls } = harness("portable");

  const initial = await invoke("lumina:getUpdateState");
  assert.equal(initial.mode, "portable");
  assert.equal((await invoke("lumina:checkForUpdates")).status, "available");
  await invoke("lumina:downloadUpdate");

  assert.equal(autoUpdater.downloadCalls, 0);
  assert.deepEqual(openedUrls, [RELEASES_URL]);
  assert.equal(await invoke("lumina:installUpdate"), false);
});

test("development builds never contact the update provider", async () => {
  const { autoUpdater, invoke } = harness("development");

  assert.equal((await invoke("lumina:getUpdateState")).status, "disabled");
  assert.equal((await invoke("lumina:checkForUpdates")).status, "disabled");
  assert.equal(autoUpdater.checkCalls, 0);
});

test("packaged builds use a delayed startup check and a bounded periodic cadence", () => {
  assert.equal(STARTUP_CHECK_DELAY_MS, 30_000);
  assert.equal(CHECK_INTERVAL_MS, 6 * 60 * 60 * 1000);
});

test("update progress and errors are safe renderer values", () => {
  assert.equal(progressPercent({ percent: -4 }), 0);
  assert.equal(progressPercent({ percent: 42.25 }), 42.25);
  assert.equal(progressPercent({ percent: 105 }), 100);
  assert.equal(progressPercent({ percent: Number.NaN }), null);
  assert.equal(errorMessage(new Error("first\r\nsecond")), "first second");
  assert.equal(errorMessage("x".repeat(700)).length, 500);
});

test("the sandbox bridge exposes update actions without exposing a configurable feed URL", () => {
  const updater = read("electron/updater.cjs");
  const preload = read("electron/preload.cjs");
  const api = read("src/fs/electronApi.ts");
  const settings = read("src/components/SettingsDialog.tsx");

  for (const action of [
    "getUpdateState",
    "checkForUpdates",
    "downloadUpdate",
    "installUpdate",
    "openUpdatePage",
    "onUpdateState",
  ]) {
    assert.match(preload, new RegExp(action));
    assert.match(api, new RegExp(action));
  }
  assert.doesNotMatch(preload, /setFeedURL|GH_TOKEN|GITHUB_TOKEN/);
  assert.doesNotMatch(updater, /setFeedURL|GH_TOKEN|GITHUB_TOKEN/);
  assert.match(settings, /SettingsAbout/);
  assert.match(settings, /RestartAndInstall/);
});
