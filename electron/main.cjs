// Lumina Electron main process. The renderer stays sandboxed
// (contextIsolation, no nodeIntegration); every filesystem capability is
// an explicit IPC handler here, and paths are validated against roots the
// user picked (or re-registered from saved locations) before any fs call.
const { app, BrowserWindow, dialog, ipcMain, nativeImage, net, protocol, shell } = require("electron");
const crypto = require("node:crypto");
const nodeFs = require("node:fs");
const fs = require("node:fs/promises");
const path = require("node:path");
const { pathToFileURL } = require("node:url");
const { execFile, spawn } = require("node:child_process");
const { promisify } = require("node:util");
const { thumbnailDataUrlForPath } = require("./thumbnail.cjs");
const { createUpdateController } = require("./updater.cjs");

const execFileAsync = promisify(execFile);

const allowedRoots = new Set(); // canonical lower-case absolute paths
const wallpaperExtensions = new Set([".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp", ".avif"]);
const wallpaperProtocol = "lumina-wallpaper";
const directoryWatchers = new Map();
const watchedWebContents = new WeakSet();
const nativeUndoStack = [];
const nativeRedoStack = [];
let clipboardWorker = null;
let clipboardWorkerOutput = "";
const clipboardWorkerPending = [];
let updateController = null;

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

async function runPowerShell(script) {
  const { stdout } = await execFileAsync(
    "powershell.exe",
    ["-NoProfile", "-NonInteractive", "-Sta", "-ExecutionPolicy", "Bypass", "-Command", script],
    { windowsHide: true, maxBuffer: 1024 * 1024 },
  );
  return stdout.trim();
}

function windowsShellHelperPath(fileName) {
  const base = app.isPackaged
    ? path.join(process.resourcesPath, "app.asar.unpacked", "electron", "windows-shell")
    : path.join(__dirname, "windows-shell");
  return path.join(base, fileName);
}

function windowsShellRunnerPath() {
  return windowsShellHelperPath("run-file-operation.ps1");
}

function nativeWindowHandle(event) {
  const window = BrowserWindow.fromWebContents(event.sender);
  if (!window) return 0;
  const buffer = window.getNativeWindowHandle();
  return buffer.length >= 8 ? buffer.readBigUInt64LE().toString() : String(buffer.readUInt32LE());
}

async function runWindowsFileOperation(event, request) {
  const payload = Buffer.from(JSON.stringify({
    action: request.action,
    sources: request.sources ?? [],
    destination: request.destination ?? "",
    newName: request.newName ?? "",
    permanent: Boolean(request.permanent),
    renameOnCollision: Boolean(request.renameOnCollision),
    ownerHandle: nativeWindowHandle(event),
    addUndoRecord: request.addUndoRecord !== false,
    noConfirmation: Boolean(request.noConfirmation),
  }), "utf8").toString("base64");
  const { stdout } = await execFileAsync(
    "powershell.exe",
    [
      "-NoProfile",
      "-NonInteractive",
      "-Sta",
      "-ExecutionPolicy",
      "Bypass",
      "-File",
      windowsShellRunnerPath(),
      "-Payload",
      payload,
    ],
    { windowsHide: true, maxBuffer: 1024 * 1024 },
  );
  const output = stdout.trim();
  return output
    ? JSON.parse(Buffer.from(output, "base64").toString("utf8"))
    : { Aborted: false };
}

function failClipboardWorker(error) {
  const failure = error instanceof Error ? error : new Error(String(error));
  while (clipboardWorkerPending.length > 0) clipboardWorkerPending.shift().reject(failure);
}

function startClipboardWorker() {
  if (clipboardWorker && clipboardWorker.exitCode === null && !clipboardWorker.killed) {
    return clipboardWorker;
  }

  const worker = spawn(
    "powershell.exe",
    [
      "-NoProfile",
      "-NonInteractive",
      "-Sta",
      "-ExecutionPolicy",
      "Bypass",
      "-File",
      windowsShellHelperPath("clipboard-worker.ps1"),
    ],
    { windowsHide: true, stdio: ["pipe", "pipe", "pipe"] },
  );
  clipboardWorker = worker;
  clipboardWorkerOutput = "";
  worker.clipboardReady = new Promise((resolve) => {
    worker.resolveClipboardReady = resolve;
  });
  worker.stdout.setEncoding("utf8");
  worker.stderr.resume();
  worker.stdout.on("data", (chunk) => {
    clipboardWorkerOutput += chunk;
    for (;;) {
      const newline = clipboardWorkerOutput.indexOf("\n");
      if (newline < 0) break;
      const line = clipboardWorkerOutput.slice(0, newline).trim();
      clipboardWorkerOutput = clipboardWorkerOutput.slice(newline + 1);
      if (line === "LUMINA_CLIPBOARD_READY") {
        worker.resolveClipboardReady(true);
        continue;
      }
      if (!line.startsWith("LUMINA_CLIPBOARD:")) continue;
      const pending = clipboardWorkerPending.shift();
      if (!pending) continue;
      try {
        const response = JSON.parse(
          Buffer.from(line.slice("LUMINA_CLIPBOARD:".length), "base64").toString("utf8"),
        );
        if (response.ok) pending.resolve(response.data);
        else pending.reject(new Error(response.error || "Windows clipboard operation failed."));
      } catch (error) {
        pending.reject(error);
      }
    }
  });
  worker.on("error", (error) => {
    worker.resolveClipboardReady(false);
    failClipboardWorker(error);
  });
  worker.on("exit", (code) => {
    worker.resolveClipboardReady(false);
    if (clipboardWorker === worker) clipboardWorker = null;
    if (clipboardWorkerPending.length > 0) {
      failClipboardWorker(new Error(`Windows clipboard worker exited with code ${code}.`));
    }
  });
  return worker;
}

async function runWindowsClipboard(request) {
  const worker = startClipboardWorker();
  if (!(await worker.clipboardReady) || worker.exitCode !== null) {
    throw new Error("Windows clipboard worker could not be started.");
  }
  const payload = Buffer.from(JSON.stringify(request), "utf8").toString("base64");
  return new Promise((resolve, reject) => {
    clipboardWorkerPending.push({ resolve, reject });
    worker.stdin.write(`${payload}\n`, "utf8", (error) => {
      if (!error) return;
      const index = clipboardWorkerPending.findIndex((entry) => entry.resolve === resolve);
      if (index >= 0) clipboardWorkerPending.splice(index, 1);
      reject(error);
    });
  });
}

function stopClipboardWorker() {
  const worker = clipboardWorker;
  clipboardWorker = null;
  if (!worker) return;
  worker.resolveClipboardReady(false);
  // The app is already quitting; drop outstanding renderer requests instead
  // of surfacing shutdown-only IPC errors in the console.
  clipboardWorkerPending.length = 0;
  worker.stdin.end();
  worker.kill();
}

async function writeWindowsFileClipboard(paths, move) {
  await runWindowsClipboard({ action: "write", paths, move });
}

async function readWindowsFileClipboard() {
  const result = await runWindowsClipboard({ action: "read" });
  const rawPaths = Array.isArray(result.paths) ? result.paths : typeof result.paths === "string" ? [result.paths] : [];
  const paths = rawPaths.filter((entry) => typeof entry === "string");
  return { paths, move: Boolean(result.move) };
}

async function markWindowsClipboardPasteSucceeded(move) {
  await runWindowsClipboard({ action: "complete", move });
}

async function directoryNames(directory) {
  return fs.readdir(directory).catch(() => []);
}

function recordNativePasteHistory(entry) {
  nativeUndoStack.push(entry);
  if (nativeUndoStack.length > 100) nativeUndoStack.shift();
  nativeRedoStack.length = 0;
}

async function buildNativePasteHistory(sources, destination, move, namesBefore) {
  const before = new Set(namesBefore.map((name) => name.toLowerCase()));
  const namesAfter = await directoryNames(destination);
  const after = new Map(namesAfter.map((name) => [name.toLowerCase(), name]));
  if (move) {
    const targets = [];
    for (const source of sources) {
      const name = path.basename(source);
      if (before.has(name.toLowerCase()) || !after.has(name.toLowerCase())) return null;
      try {
        await fs.access(source);
        return null;
      } catch (error) {
        if (!error || error.code !== "ENOENT") return null;
      }
      targets.push(path.join(destination, after.get(name.toLowerCase())));
    }
    return { kind: "move", sources, targets, destination };
  }

  const regularTargets = [];
  const sameDirectorySources = [];
  for (const source of sources) {
    const name = path.basename(source);
    if (path.dirname(source).toLowerCase() === destination.toLowerCase()) {
      sameDirectorySources.push(source);
      continue;
    }
    if (before.has(name.toLowerCase()) || !after.has(name.toLowerCase())) return null;
    regularTargets.push(path.join(destination, after.get(name.toLowerCase())));
  }
  const newNames = namesAfter.filter((name) => !before.has(name.toLowerCase()));
  const regularNames = new Set(regularTargets.map((target) => path.basename(target).toLowerCase()));
  const collisionTargets = newNames
    .filter((name) => !regularNames.has(name.toLowerCase()))
    .map((name) => path.join(destination, name));
  if (collisionTargets.length !== sameDirectorySources.length) return null;
  return {
    kind: "copy",
    sources,
    targets: [...regularTargets, ...collisionTargets],
    destination,
  };
}

async function replayNativePaste(event, entry) {
  const sameDirectoryCopies = entry.kind === "copy"
    ? entry.sources.filter((source) => path.dirname(source).toLowerCase() === entry.destination.toLowerCase())
    : [];
  const regularSources = entry.sources.filter((source) => !sameDirectoryCopies.includes(source));
  if (sameDirectoryCopies.length > 0) {
    await runWindowsFileOperation(event, {
      action: "copy",
      sources: sameDirectoryCopies,
      destination: entry.destination,
      renameOnCollision: true,
      addUndoRecord: false,
      noConfirmation: true,
    });
  }
  if (regularSources.length > 0) {
    await runWindowsFileOperation(event, {
      action: entry.kind,
      sources: regularSources,
      destination: entry.destination,
      addUndoRecord: false,
      noConfirmation: true,
    });
  }
}

async function undoNativePaste(event, entry) {
  if (entry.kind === "copy") {
    await runWindowsFileOperation(event, {
      action: "delete",
      sources: entry.targets,
      permanent: true,
      addUndoRecord: false,
      noConfirmation: true,
    });
    return;
  }
  for (let index = 0; index < entry.targets.length; index += 1) {
    await runWindowsFileOperation(event, {
      action: "move",
      sources: [entry.targets[index]],
      destination: path.dirname(entry.sources[index]),
      addUndoRecord: false,
      noConfirmation: true,
    });
  }
}

async function restoreWindowsRecycleBin(paths) {
  const payload = Buffer.from(JSON.stringify({ paths }), "utf8").toString("base64");
  const script = [
    `$payload = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('${payload}'))`,
    "$input = ConvertFrom-Json -InputObject $payload",
    "$shell = New-Object -ComObject Shell.Application",
    "$recycleBin = $shell.NameSpace(0xA)",
    "if ($null -eq $recycleBin) { throw 'Windows Recycle Bin is unavailable.' }",
    "$items = @($recycleBin.Items())",
    "foreach ($path in $input.paths) {",
    "  if (Test-Path -LiteralPath $path) { continue }",
    "  $parent = [IO.Path]::GetDirectoryName($path)",
    "  $name = [IO.Path]::GetFileName($path)",
    "  $item = @($items | Where-Object { $_.Name -eq $name -and $_.ExtendedProperty('System.Recycle.DeletedFrom') -eq $parent }) | Select-Object -First 1",
    "  if ($null -eq $item) { throw ('Deleted item was not found in the Recycle Bin: ' + $path) }",
    "  $verb = @($item.Verbs() | Where-Object { $_.Name.Trim() -ne '' }) | Select-Object -First 1",
    "  if ($null -eq $verb) { throw ('Recycle Bin item has no restore verb: ' + $path) }",
    "  $verb.DoIt()",
    "  for ($i = 0; $i -lt 30 -and -not (Test-Path -LiteralPath $path); $i++) { Start-Sleep -Milliseconds 100 }",
    "  if (-not (Test-Path -LiteralPath $path)) { throw ('Recycle Bin restore did not restore the original path: ' + $path) }",
    "}",
  ].join("; ");
  await runPowerShell(script);
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

  ipcMain.handle("lumina:watchDirectory", (event, directoryPath) => {
    const directory = assertAllowed(directoryPath);
    const id = event.sender.id;
    const previous = directoryWatchers.get(id);
    if (previous) {
      clearTimeout(previous.timer);
      previous.watcher.close();
    }
    const entry = { token: crypto.randomUUID(), watcher: null, timer: undefined };
    entry.watcher = nodeFs.watch(directory, { persistent: false, recursive: true }, () => {
      clearTimeout(entry.timer);
      entry.timer = setTimeout(() => {
        if (!event.sender.isDestroyed()) event.sender.send("lumina:directoryChanged");
      }, 120);
    });
    entry.watcher.on("error", () => {
      entry.watcher.close();
      if (directoryWatchers.get(id) === entry) directoryWatchers.delete(id);
      if (!event.sender.isDestroyed()) event.sender.send("lumina:directoryChanged");
    });
    directoryWatchers.set(id, entry);
    if (!watchedWebContents.has(event.sender)) {
      watchedWebContents.add(event.sender);
      event.sender.once("destroyed", () => {
        const current = directoryWatchers.get(id);
        if (!current) return;
        clearTimeout(current.timer);
        current.watcher.close();
        directoryWatchers.delete(id);
      });
    }
    return entry.token;
  });

  ipcMain.handle("lumina:unwatchDirectory", (event, token) => {
    const entry = directoryWatchers.get(event.sender.id);
    if (!entry || entry.token !== token) return;
    clearTimeout(entry.timer);
    entry.watcher.close();
    directoryWatchers.delete(event.sender.id);
  });

  ipcMain.handle("lumina:list", async (_event, dirPath) => {
    const dir = assertAllowed(dirPath);
    const dirents = await fs.readdir(dir, { withFileTypes: true });
    return Promise.all(dirents.map((d) => entryInfo(dir, d, "")));
  });

  ipcMain.handle("lumina:pathExists", async (_event, targetPath) => {
    const target = assertAllowed(targetPath);
    try {
      await fs.access(target);
      return true;
    } catch (error) {
      if (error && error.code === "ENOENT") return false;
      throw error;
    }
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

  ipcMain.handle("lumina:mkdir", async (event, dirPath) => {
    const dir = assertAllowed(dirPath);
    const result = await runWindowsFileOperation(event, {
      action: "newFolder",
      destination: path.dirname(dir),
      newName: path.basename(dir),
    });
    if (result.Aborted) throw new Error("Folder creation was cancelled.");
    return dir;
  });

  ipcMain.handle("lumina:rename", async (event, oldPath, newName) => {
    const from = assertAllowed(oldPath);
    if (typeof newName !== "string" || /[\\/:*?"<>|]/.test(newName) || !newName.trim()) {
      throw new Error("The name must be a valid file or folder name.");
    }
    const to = path.join(path.dirname(from), newName);
    if (to.toLowerCase() === from.toLowerCase()) return to;
    const result = await runWindowsFileOperation(event, {
      action: "rename",
      sources: [from],
      newName,
    });
    if (result.Aborted) throw new Error("Rename was cancelled.");
    return to;
  });

  ipcMain.handle("lumina:trash", async (event, paths) => {
    if (!Array.isArray(paths)) throw new Error("Expected a path array");
    const result = await runWindowsFileOperation(event, {
      action: "delete",
      sources: paths.map(assertAllowed),
    });
    return { aborted: Boolean(result.Aborted) };
  });

  ipcMain.handle("lumina:deletePermanently", async (event, paths) => {
    if (!Array.isArray(paths)) throw new Error("Expected a path array");
    const result = await runWindowsFileOperation(event, {
      action: "delete",
      sources: paths.map(assertAllowed),
      permanent: true,
    });
    return { aborted: Boolean(result.Aborted) };
  });

  ipcMain.handle("lumina:transfer", async (event, paths, destinationPath, move) => {
    if (!Array.isArray(paths) || paths.length === 0 || typeof move !== "boolean") {
      throw new Error("Expected file paths and a transfer mode");
    }
    const destination = assertAllowed(destinationPath);
    const sources = paths.map(assertAllowed);
    const sameDirectoryCopies = move
      ? []
      : sources.filter((source) => path.dirname(source).toLowerCase() === destination.toLowerCase());
    const regularSources = sources.filter((source) => !sameDirectoryCopies.includes(source));
    let aborted = false;
    if (sameDirectoryCopies.length > 0) {
      const result = await runWindowsFileOperation(event, {
        action: "copy",
        sources: sameDirectoryCopies,
        destination,
        renameOnCollision: true,
      });
      aborted ||= Boolean(result.Aborted);
    }
    if (regularSources.length > 0) {
      const result = await runWindowsFileOperation(event, {
        action: move ? "move" : "copy",
        sources: regularSources,
        destination,
      });
      aborted ||= Boolean(result.Aborted);
    }
    return { aborted };
  });

  ipcMain.handle("lumina:writeFileClipboard", async (_event, paths, move) => {
    if (!Array.isArray(paths) || typeof move !== "boolean") {
      throw new Error("Expected file paths and a clipboard mode");
    }
    await writeWindowsFileClipboard(paths.map(assertAllowed), move);
  });

  ipcMain.handle("lumina:readFileClipboard", async () => readWindowsFileClipboard());

  ipcMain.handle("lumina:pasteFileClipboard", async (event, destinationPath) => {
    const destination = assertAllowed(destinationPath);
    const { paths, move } = await readWindowsFileClipboard();
    if (paths.length === 0) return { pasted: false };
    const sources = paths.map(canonical);
    const namesBefore = await directoryNames(destination);
    const sameDirectoryCopies = move
      ? []
      : sources.filter((source) => path.dirname(source).toLowerCase() === destination.toLowerCase());
    const regularSources = sources.filter((source) => !sameDirectoryCopies.includes(source));
    let aborted = false;
    if (sameDirectoryCopies.length > 0) {
      const result = await runWindowsFileOperation(event, {
        action: "copy",
        sources: sameDirectoryCopies,
        destination,
        renameOnCollision: true,
      });
      aborted ||= Boolean(result.Aborted);
    }
    if (regularSources.length > 0) {
      const result = await runWindowsFileOperation(event, {
        action: move ? "move" : "copy",
        sources: regularSources,
        destination,
      });
      aborted ||= Boolean(result.Aborted);
    }
    let undoRecorded = false;
    if (!aborted) {
      try {
        await markWindowsClipboardPasteSucceeded(move);
      } catch (error) {
        console.warn("Could not publish the completed paste effect to the Windows clipboard:", error);
      }
      const history = await buildNativePasteHistory(sources, destination, move, namesBefore);
      if (history) {
        recordNativePasteHistory(history);
        undoRecorded = true;
      }
    }
    return { pasted: !aborted, undoRecorded };
  });

  ipcMain.handle("lumina:undoNativePaste", async (event) => {
    const entry = nativeUndoStack.pop();
    if (!entry) return { handled: false };
    try {
      const targetsMissing = (await Promise.all(
        entry.targets.map((target) => fs.access(target).then(() => false).catch(() => true)),
      )).every(Boolean);
      const sourcesPresent = (await Promise.all(
        entry.sources.map((source) => fs.access(source).then(() => true).catch(() => false)),
      )).every(Boolean);
      const alreadyUndone = entry.kind === "copy" ? targetsMissing : targetsMissing && sourcesPresent;
      if (!alreadyUndone) await undoNativePaste(event, entry);
      nativeRedoStack.push(entry);
      return { handled: true };
    } catch (error) {
      nativeUndoStack.push(entry);
      throw error;
    }
  });

  ipcMain.handle("lumina:redoNativePaste", async (event) => {
    const entry = nativeRedoStack.pop();
    if (!entry) return { handled: false };
    try {
      const targetsPresent = (await Promise.all(
        entry.targets.map((target) => fs.access(target).then(() => true).catch(() => false)),
      )).every(Boolean);
      const sourcesMissing = (await Promise.all(
        entry.sources.map((source) => fs.access(source).then(() => false).catch(() => true)),
      )).every(Boolean);
      const alreadyRedone = entry.kind === "copy" ? targetsPresent : targetsPresent && sourcesMissing;
      if (!alreadyRedone) await replayNativePaste(event, entry);
      nativeUndoStack.push(entry);
      return { handled: true };
    } catch (error) {
      nativeRedoStack.push(entry);
      throw error;
    }
  });

  ipcMain.handle("lumina:restoreDeleted", async (_event, paths) => {
    if (!Array.isArray(paths)) throw new Error("Expected a path array");
    await restoreWindowsRecycleBin(paths.map(assertAllowed));
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

  ipcMain.handle("lumina:thumbnail", async (_event, filePath) =>
    thumbnailDataUrlForPath(assertAllowed(filePath), nativeImage),
  );

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
    show: false,
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

  if (!smoke) {
    win.once("ready-to-show", () => {
      const worker = startClipboardWorker();
      void Promise.race([
        worker.clipboardReady,
        new Promise((resolve) => setTimeout(() => resolve(false), 5000)),
      ]).finally(() => {
        if (!win.isDestroyed()) win.show();
      });
    });
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
        const worker = startClipboardWorker();
        const clipboardReady = await Promise.race([
          worker.clipboardReady,
          new Promise((resolve) => setTimeout(() => resolve(false), 8000)),
        ]);
        if (!clipboardReady) throw new Error("Windows clipboard worker did not become ready.");
        const probe = await win.webContents.executeJavaScript(
          "JSON.stringify({ title: document.title, bridge: typeof window.luminaNative, updates: typeof window.luminaNative?.getUpdateState === 'function', root: !!document.getElementById('root')?.childElementCount })",
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
  updateController = createUpdateController();
  updateController.registerIpc();
  registerWallpaperProtocol();
  registerIpc();
  startClipboardWorker();
  createWindow();
  updateController.start();
  app.on("activate", () => {
    if (BrowserWindow.getAllWindows().length === 0) createWindow();
  });
});

app.on("before-quit", () => {
  updateController?.stop();
  stopClipboardWorker();
});

app.on("window-all-closed", () => {
  if (process.platform !== "darwin") app.quit();
});
