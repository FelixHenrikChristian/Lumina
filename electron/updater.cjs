const RELEASES_URL = "https://github.com/FelixHenrikChristian/Lumina/releases/latest";
const STARTUP_CHECK_DELAY_MS = 30 * 1000;
const CHECK_INTERVAL_MS = 6 * 60 * 60 * 1000;

function runtimeDependencies() {
  const { app, BrowserWindow, ipcMain, shell } = require("electron");
  const { autoUpdater } = require("electron-updater");
  return { app, BrowserWindow, ipcMain, shell, autoUpdater, env: process.env };
}

function portableExecutable(env) {
  return typeof env.PORTABLE_EXECUTABLE_FILE === "string"
    && env.PORTABLE_EXECUTABLE_FILE.length > 0;
}

function updateMode(app, env) {
  if (!app.isPackaged) return "development";
  return portableExecutable(env) ? "portable" : "installed";
}

function copyState(state) {
  return {
    status: state.status,
    mode: state.mode,
    currentVersion: state.currentVersion,
    availableVersion: state.availableVersion,
    progressPercent: state.progressPercent,
    error: state.error,
  };
}

function errorMessage(error) {
  const message = error instanceof Error ? error.message : String(error);
  return message.replace(/[\r\n]+/g, " ").slice(0, 500);
}

function progressPercent(progress) {
  const percent = Number(progress?.percent);
  if (!Number.isFinite(percent)) return null;
  return Math.max(0, Math.min(100, percent));
}

function createUpdateController(dependencies = runtimeDependencies()) {
  const { app, BrowserWindow, ipcMain, shell, autoUpdater } = dependencies;
  const mode = updateMode(app, dependencies.env ?? process.env);
  let state = {
    status: mode === "development" ? "disabled" : "idle",
    mode,
    currentVersion: app.getVersion(),
    availableVersion: null,
    progressPercent: null,
    error: null,
  };
  let startupTimer = null;
  let intervalTimer = null;
  let checkPromise = null;
  let downloadPromise = null;

  autoUpdater.autoDownload = false;
  autoUpdater.autoInstallOnAppQuit = false;
  autoUpdater.autoRunAppAfterInstall = true;
  autoUpdater.allowDowngrade = false;
  autoUpdater.allowPrerelease = false;
  // NSIS web installers do not provide the same signature guarantees as the
  // full installer. Lumina publishes only the full NSIS target.
  autoUpdater.disableWebInstaller = true;

  function broadcast() {
    const snapshot = copyState(state);
    for (const window of BrowserWindow.getAllWindows()) {
      if (!window.isDestroyed() && !window.webContents.isDestroyed()) {
        window.webContents.send("lumina:updateState", snapshot);
      }
    }
  }

  function setState(patch) {
    state = { ...state, ...patch };
    broadcast();
    return copyState(state);
  }

  function setError(error) {
    return setState({
      status: "error",
      progressPercent: null,
      error: errorMessage(error),
    });
  }

  autoUpdater.on("checking-for-update", () => {
    setState({
      status: "checking",
      availableVersion: null,
      progressPercent: null,
      error: null,
    });
  });

  autoUpdater.on("update-available", (info) => {
    setState({
      status: "available",
      availableVersion: info.version,
      progressPercent: null,
      error: null,
    });
  });

  autoUpdater.on("update-not-available", () => {
    setState({
      status: "up-to-date",
      availableVersion: null,
      progressPercent: null,
      error: null,
    });
  });

  autoUpdater.on("download-progress", (progress) => {
    setState({
      status: "downloading",
      progressPercent: progressPercent(progress),
      error: null,
    });
  });

  autoUpdater.on("update-downloaded", (info) => {
    setState({
      status: "downloaded",
      availableVersion: info.version,
      progressPercent: 100,
      error: null,
    });
  });

  autoUpdater.on("error", (error) => {
    setError(error);
  });

  async function checkForUpdates(force = false) {
    if (mode === "development") return copyState(state);
    if (checkPromise) return checkPromise;
    if (!force && ["available", "downloading", "downloaded", "installing"].includes(state.status)) {
      return copyState(state);
    }
    if (["downloading", "downloaded", "installing"].includes(state.status)) {
      return copyState(state);
    }

    checkPromise = (async () => {
      try {
        await autoUpdater.checkForUpdates();
      } catch (error) {
        setError(error);
      } finally {
        checkPromise = null;
      }
      return copyState(state);
    })();
    return checkPromise;
  }

  async function downloadUpdate() {
    if (mode !== "installed") {
      await shell.openExternal(RELEASES_URL);
      return copyState(state);
    }
    if (downloadPromise) return downloadPromise;
    if (state.status !== "available") {
      await checkForUpdates(true);
    }
    if (state.status !== "available") return copyState(state);

    setState({ status: "downloading", progressPercent: 0, error: null });
    downloadPromise = (async () => {
      try {
        await autoUpdater.downloadUpdate();
      } catch (error) {
        setError(error);
      } finally {
        downloadPromise = null;
      }
      return copyState(state);
    })();
    return downloadPromise;
  }

  function installUpdate() {
    if (mode !== "installed" || state.status !== "downloaded") return false;
    setState({ status: "installing", error: null });
    setImmediate(() => autoUpdater.quitAndInstall(false, true));
    return true;
  }

  function registerIpc() {
    ipcMain.handle("lumina:getUpdateState", () => copyState(state));
    ipcMain.handle("lumina:checkForUpdates", () => checkForUpdates(true));
    ipcMain.handle("lumina:downloadUpdate", () => downloadUpdate());
    ipcMain.handle("lumina:installUpdate", () => installUpdate());
    ipcMain.handle("lumina:openUpdatePage", async () => {
      await shell.openExternal(RELEASES_URL);
      return true;
    });
  }

  function start() {
    if (mode === "development" || startupTimer || intervalTimer) return;
    startupTimer = setTimeout(() => {
      startupTimer = null;
      void checkForUpdates(false);
    }, STARTUP_CHECK_DELAY_MS);
    startupTimer.unref?.();
    intervalTimer = setInterval(() => void checkForUpdates(false), CHECK_INTERVAL_MS);
    intervalTimer.unref?.();
  }

  function stop() {
    if (startupTimer) clearTimeout(startupTimer);
    if (intervalTimer) clearInterval(intervalTimer);
    startupTimer = null;
    intervalTimer = null;
  }

  return { registerIpc, start, stop };
}

module.exports = {
  CHECK_INTERVAL_MS,
  RELEASES_URL,
  STARTUP_CHECK_DELAY_MS,
  createUpdateController,
  errorMessage,
  portableExecutable,
  progressPercent,
  updateMode,
};
