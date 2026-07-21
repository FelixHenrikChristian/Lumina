// Exposes the filesystem bridge to the sandboxed renderer. Shapes mirror
// the IPC handlers in main.cjs; src/fs/electronApi.d.ts is the typed view.
const { contextBridge, ipcRenderer, webUtils } = require("electron");

contextBridge.exposeInMainWorld("luminaNative", {
  chooseWallpaper: () => ipcRenderer.invoke("lumina:chooseWallpaper"),
  pickFolder: () => ipcRenderer.invoke("lumina:pickFolder"),
  registerRoot: (rootPath) => ipcRenderer.invoke("lumina:registerRoot", rootPath),
  watchDirectory: (directoryPath) => ipcRenderer.invoke("lumina:watchDirectory", directoryPath),
  unwatchDirectory: (token) => ipcRenderer.invoke("lumina:unwatchDirectory", token),
  onDirectoryChanged: (callback) => {
    const listener = () => callback();
    ipcRenderer.on("lumina:directoryChanged", listener);
    return () => ipcRenderer.removeListener("lumina:directoryChanged", listener);
  },
  list: (dirPath) => ipcRenderer.invoke("lumina:list", dirPath),
  pathExists: (targetPath) => ipcRenderer.invoke("lumina:pathExists", targetPath),
  listRecursive: (rootPath) => ipcRenderer.invoke("lumina:listRecursive", rootPath),
  mkdir: (dirPath) => ipcRenderer.invoke("lumina:mkdir", dirPath),
  rename: (oldPath, newName) => ipcRenderer.invoke("lumina:rename", oldPath, newName),
  trash: (paths) => ipcRenderer.invoke("lumina:trash", paths),
  deletePermanently: (paths) => ipcRenderer.invoke("lumina:deletePermanently", paths),
  transfer: (paths, destinationPath, move, resolutions) =>
    ipcRenderer.invoke("lumina:transfer", paths, destinationPath, move, resolutions),
  cancelFileOperation: (operationId) => ipcRenderer.invoke("lumina:cancelFileOperation", operationId),
  onFileOperationProgress: (callback) => {
    const listener = (_event, state) => callback(state);
    ipcRenderer.on("lumina:fileOperationProgress", listener);
    return () => ipcRenderer.removeListener("lumina:fileOperationProgress", listener);
  },
  writeFileClipboard: (paths, move) => ipcRenderer.invoke("lumina:writeFileClipboard", paths, move),
  inspectPasteFileClipboard: (destinationPath) =>
    ipcRenderer.invoke("lumina:inspectPasteFileClipboard", destinationPath),
  pasteFileClipboard: (destinationPath, resolutions) =>
    ipcRenderer.invoke("lumina:pasteFileClipboard", destinationPath, resolutions),
  readFileClipboard: () => ipcRenderer.invoke("lumina:readFileClipboard"),
  // Drag-drop from Explorer: dropped File objects only reveal their OS path
  // through webUtils, and the import runs outside the clipboard pipeline.
  pathForFile: (file) => webUtils.getPathForFile(file),
  inspectExternalImport: (sourcePaths, destinationPath) =>
    ipcRenderer.invoke("lumina:inspectExternalImport", sourcePaths, destinationPath),
  importExternalPaths: (sourcePaths, destinationPath, move, resolutions) =>
    ipcRenderer.invoke("lumina:importExternalPaths", sourcePaths, destinationPath, move, resolutions),
  undoNativePaste: () => ipcRenderer.invoke("lumina:undoNativePaste"),
  redoNativePaste: () => ipcRenderer.invoke("lumina:redoNativePaste"),
  restoreDeleted: (paths) => ipcRenderer.invoke("lumina:restoreDeleted", paths),
  readFile: (filePath) => ipcRenderer.invoke("lumina:readFile", filePath),
  thumbnail: (filePath) => ipcRenderer.invoke("lumina:thumbnail", filePath),
  openPath: (targetPath) => ipcRenderer.invoke("lumina:openPath", targetPath),
  reveal: (targetPath) => ipcRenderer.invoke("lumina:reveal", targetPath),
  getUpdateState: () => ipcRenderer.invoke("lumina:getUpdateState"),
  checkForUpdates: () => ipcRenderer.invoke("lumina:checkForUpdates"),
  downloadUpdate: () => ipcRenderer.invoke("lumina:downloadUpdate"),
  installUpdate: () => ipcRenderer.invoke("lumina:installUpdate"),
  openUpdatePage: () => ipcRenderer.invoke("lumina:openUpdatePage"),
  onUpdateState: (callback) => {
    const listener = (_event, state) => callback(state);
    ipcRenderer.on("lumina:updateState", listener);
    return () => ipcRenderer.removeListener("lumina:updateState", listener);
  },
});
