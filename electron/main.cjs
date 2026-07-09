// Lumina Electron main process. The renderer stays sandboxed
// (contextIsolation, no nodeIntegration); every filesystem capability is
// an explicit IPC handler here, and paths are validated against roots the
// user picked (or re-registered from saved locations) before any fs call.
const { app, BrowserWindow, dialog, ipcMain, net, protocol, shell } = require("electron");
const crypto = require("node:crypto");
const fs = require("node:fs/promises");
const path = require("node:path");
const { pathToFileURL } = require("node:url");

const allowedRoots = new Set(); // canonical lower-case absolute paths
const wallpaperExtensions = new Set([".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp", ".avif"]);
const wallpaperProtocol = "lumina-wallpaper";

protocol.registerSchemesAsPrivileged([
  {
    scheme: wallpaperProtocol,
    privileges: { standard: true, secure: true, supportFetchAPI: true, corsEnabled: true },
  },
]);

function canonical(p) {
  return path.resolve(p).replace(/[\\/]+$/, "");
}

function isInsideAllowedRoot(p) {
  const candidate = canonical(p).toLowerCase();
  for (const root of allowedRoots) {
    if (candidate === root) return true;
    if (candidate.startsWith(root + path.sep.toLowerCase()) || candidate.startsWith(root + "\\") || candidate.startsWith(root + "/")) {
      return true;
    }
  }
  return false;
}

function assertAllowed(p) {
  if (typeof p !== "string" || !isInsideAllowedRoot(p)) {
    throw new Error(`Path is outside the registered locations: ${p}`);
  }
  return canonical(p);
}

function wallpaperDir() {
  return path.join(app.getPath("userData"), "wallpapers");
}

function wallpaperUrlFor(fileName) {
  return `${wallpaperProtocol}://image/${encodeURIComponent(fileName)}`;
}

function wallpaperPathFromUrl(rawUrl) {
  const parsed = new URL(rawUrl);
  const fileName = decodeURIComponent(parsed.pathname.replace(/^\/+/, ""));
  if (!fileName || path.basename(fileName) !== fileName) {
    throw new Error("Invalid wallpaper URL.");
  }
  return path.join(wallpaperDir(), fileName);
}

function registerWallpaperProtocol() {
  protocol.handle(wallpaperProtocol, async (request) => {
    const filePath = wallpaperPathFromUrl(request.url);
    return net.fetch(pathToFileURL(filePath).toString());
  });
}

async function copyWallpaper(sourcePath) {
  const ext = path.extname(sourcePath).toLowerCase();
  if (!wallpaperExtensions.has(ext)) {
    throw new Error("Unsupported image format.");
  }
  const stat = await fs.stat(sourcePath);
  if (!stat.isFile()) {
    throw new Error("The selected wallpaper is not a file.");
  }
  const destinationDir = wallpaperDir();
  await fs.mkdir(destinationDir, { recursive: true });
  const fileName = `${Date.now()}-${crypto.randomUUID()}${ext}`;
  const destination = path.join(destinationDir, fileName);
  await fs.copyFile(sourcePath, destination);
  return {
    url: wallpaperUrlFor(fileName),
    name: path.basename(sourcePath),
  };
}

async function entryInfo(dirPath, dirent, relativeParent) {
  const full = path.join(dirPath, dirent.name);
  let size = 0;
  let modified = null;
  if (!dirent.isDirectory()) {
    try {
      const stat = await fs.stat(full);
      size = Number(stat.size);
      modified = stat.mtimeMs;
    } catch {
      // stat can fail on locked/system files; list the entry anyway
    }
  }
  return {
    name: dirent.name,
    path: full,
    relativeParent,
    isDirectory: dirent.isDirectory(),
    size,
    modified,
  };
}

function registerIpc() {
  ipcMain.handle("lumina:chooseWallpaper", async (event) => {
    const win = BrowserWindow.fromWebContents(event.sender);
    const result = await dialog.showOpenDialog(win, {
      properties: ["openFile"],
      filters: [
        {
          name: "Images",
          extensions: ["jpg", "jpeg", "png", "webp", "gif", "bmp", "avif"],
        },
      ],
    });
    if (result.canceled || result.filePaths.length === 0) return null;
    return copyWallpaper(result.filePaths[0]);
  });

  ipcMain.handle("lumina:pickFolder", async (event) => {
    const win = BrowserWindow.fromWebContents(event.sender);
    const result = await dialog.showOpenDialog(win, {
      properties: ["openDirectory"],
    });
    if (result.canceled || result.filePaths.length === 0) return null;
    const chosen = canonical(result.filePaths[0]);
    allowedRoots.add(chosen.toLowerCase());
    return { path: chosen, name: path.basename(chosen) || chosen };
  });

  // Saved locations re-register their roots on startup. This is comfort
  // scoping (mirrors LocationPathScope), not a security boundary — the
  // renderer only ever holds paths the user picked in this app.
  ipcMain.handle("lumina:registerRoot", (_event, rootPath) => {
    if (typeof rootPath !== "string" || rootPath.length === 0) return false;
    allowedRoots.add(canonical(rootPath).toLowerCase());
    return true;
  });

  ipcMain.handle("lumina:list", async (_event, dirPath) => {
    const dir = assertAllowed(dirPath);
    const dirents = await fs.readdir(dir, { withFileTypes: true });
    return Promise.all(dirents.map((d) => entryInfo(dir, d, "")));
  });

  ipcMain.handle("lumina:listRecursive", async (_event, rootPath) => {
    const root = assertAllowed(rootPath);
    const results = [];
    const walk = async (dir, relative) => {
      let dirents;
      try {
        dirents = await fs.readdir(dir, { withFileTypes: true });
      } catch {
        return; // mirror IgnoreInaccessible
      }
      for (const dirent of dirents) {
        if (dirent.isSymbolicLink()) continue; // no cycles
        results.push(await entryInfo(dir, dirent, relative));
        if (dirent.isDirectory()) {
          await walk(
            path.join(dir, dirent.name),
            relative === "." ? dirent.name : `${relative}/${dirent.name}`,
          );
        }
      }
    };
    await walk(root, ".");
    return results;
  });

  ipcMain.handle("lumina:mkdir", async (_event, dirPath) => {
    const dir = assertAllowed(dirPath);
    await fs.mkdir(dir); // no recursive: parent must exist, target must not
    return dir;
  });

  ipcMain.handle("lumina:rename", async (_event, oldPath, newName) => {
    const from = assertAllowed(oldPath);
    if (typeof newName !== "string" || /[\\/:*?"<>|]/.test(newName) || !newName.trim()) {
      throw new Error("The name must be a valid file or folder name.");
    }
    const to = path.join(path.dirname(from), newName);
    const sameSlot = to.toLowerCase() === from.toLowerCase();
    if (!sameSlot) {
      try {
        await fs.access(to);
        throw new Error(`Destination already exists: ${to}`);
      } catch (error) {
        if (error.code !== "ENOENT") throw error;
      }
    }
    await fs.rename(from, to);
    return to;
  });

  ipcMain.handle("lumina:trash", async (_event, paths) => {
    if (!Array.isArray(paths)) throw new Error("Expected a path array");
    for (const p of paths) {
      await shell.trashItem(assertAllowed(p));
    }
  });

  ipcMain.handle("lumina:readFile", async (_event, filePath) => {
    const file = assertAllowed(filePath);
    const stat = await fs.stat(file);
    if (stat.size > 64 * 1024 * 1024) {
      throw new Error("File is too large to preview.");
    }
    const buffer = await fs.readFile(file);
    // Return an ArrayBuffer slice so structured clone hands the renderer
    // exactly the file bytes.
    return buffer.buffer.slice(buffer.byteOffset, buffer.byteOffset + buffer.byteLength);
  });

  ipcMain.handle("lumina:openPath", async (_event, targetPath) => {
    const target = assertAllowed(targetPath);
    const error = await shell.openPath(target);
    if (error) throw new Error(error);
    return true;
  });

  ipcMain.handle("lumina:reveal", (_event, targetPath) => {
    shell.showItemInFolder(assertAllowed(targetPath));
    return true;
  });
}

function createWindow() {
  const smoke = process.env.LUMINA_SMOKE === "1";
  const win = new BrowserWindow({
    width: 1280,
    height: 800,
    minWidth: 720,
    minHeight: 480,
    show: !smoke,
    backgroundColor: "#0b1020",
    autoHideMenuBar: true,
    webPreferences: {
      preload: path.join(__dirname, "preload.cjs"),
      contextIsolation: true,
      nodeIntegration: false,
    },
  });

  const devUrl = process.env.VITE_DEV_SERVER_URL;
  if (devUrl) {
    win.loadURL(devUrl);
  } else {
    win.loadFile(path.join(__dirname, "..", "dist", "index.html"));
  }

  // Anything that tries to open a new window (e.g. blob: fallbacks) goes to
  // the system browser instead of a second Electron window.
  win.webContents.setWindowOpenHandler(({ url }) => {
    if (url.startsWith("http:") || url.startsWith("https:")) {
      shell.openExternal(url);
    }
    return { action: "deny" };
  });

  if (smoke) {
    win.webContents.once("did-finish-load", async () => {
      try {
        const probe = await win.webContents.executeJavaScript(
          "JSON.stringify({ title: document.title, bridge: typeof window.luminaNative, root: !!document.getElementById('root')?.childElementCount })",
        );
        console.log(`LUMINA_SMOKE_OK ${probe}`);
        process.exitCode = 0;
      } catch (error) {
        console.error(`LUMINA_SMOKE_FAIL ${error}`);
        process.exitCode = 1;
      } finally {
        app.quit();
      }
    });
  }

  return win;
}

app.whenReady().then(() => {
  registerWallpaperProtocol();
  registerIpc();
  createWindow();
  app.on("activate", () => {
    if (BrowserWindow.getAllWindows().length === 0) createWindow();
  });
});

app.on("window-all-closed", () => {
  if (process.platform !== "darwin") app.quit();
});
