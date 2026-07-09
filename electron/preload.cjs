// Exposes the filesystem bridge to the sandboxed renderer. Shapes mirror
// the IPC handlers in main.cjs; src/fs/electronApi.d.ts is the typed view.
const { contextBridge, ipcRenderer } = require("electron");

contextBridge.exposeInMainWorld("luminaNative", {
  chooseWallpaper: () => ipcRenderer.invoke("lumina:chooseWallpaper"),
  pickFolder: () => ipcRenderer.invoke("lumina:pickFolder"),
  registerRoot: (rootPath) => ipcRenderer.invoke("lumina:registerRoot", rootPath),
  list: (dirPath) => ipcRenderer.invoke("lumina:list", dirPath),
  listRecursive: (rootPath) => ipcRenderer.invoke("lumina:listRecursive", rootPath),
  mkdir: (dirPath) => ipcRenderer.invoke("lumina:mkdir", dirPath),
  rename: (oldPath, newName) => ipcRenderer.invoke("lumina:rename", oldPath, newName),
  trash: (paths) => ipcRenderer.invoke("lumina:trash", paths),
  readFile: (filePath) => ipcRenderer.invoke("lumina:readFile", filePath),
  openPath: (targetPath) => ipcRenderer.invoke("lumina:openPath", targetPath),
  reveal: (targetPath) => ipcRenderer.invoke("lumina:reveal", targetPath),
});
