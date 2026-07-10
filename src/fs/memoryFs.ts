import type { FileItem } from "../core/models";
import { previewKindFor } from "../core/models";
import { getDisplayName, parseTagsFromFilename } from "../core/tagParser";
import { baseNameOf, joinPath, parentPathOf } from "../core/paths";
import {
  copyName,
  uniqueName,
  validateEntryName,
  type FileBrowserService,
  type TransferConflictResolutions,
} from "./types";

interface MemoryNode {
  name: string;
  isDirectory: boolean;
  size: number;
  modified: number;
  children: Map<string, MemoryNode>; // keyed by lowercase name
}

function makeItem(node: MemoryNode, path: string, relativePath: string): FileItem {
  const displayName = getDisplayName(node.name);
  return {
    name: node.name,
    displayName: displayName.trim() ? displayName : node.name,
    path,
    relativePath,
    isDirectory: node.isDirectory,
    previewKind: previewKindFor(node.name, node.isDirectory),
    size: node.size,
    modified: node.modified,
    tags: parseTagsFromFilename(node.name),
  };
}

function dir(name: string, children: MemoryNode[] = []): MemoryNode {
  const map = new Map<string, MemoryNode>();
  for (const child of children) map.set(child.name.toLowerCase(), child);
  return { name, isDirectory: true, size: 0, modified: Date.now(), children: map };
}

function cloneNode(node: MemoryNode): MemoryNode {
  return {
    ...node,
    children: new Map([...node.children].map(([key, child]) => [key, cloneNode(child)])),
  };
}

let seedCounter = 0;

function file(name: string, size: number): MemoryNode {
  // Deterministic-ish spread of recent timestamps for a lively demo listing.
  const modified = Date.now() - (++seedCounter % 40) * 36e5 * 7;
  return { name, isDirectory: false, size, modified, children: new Map() };
}

function buildDemoTree(): MemoryNode {
  return dir("root", [
    dir("Photos", [
      dir("2025 Japan", [
        file("[travel japan] shibuya crossing.jpg", 4_213_009),
        file("[travel japan food] ramen night.jpg", 3_118_442),
        file("[travel japan] mount fuji dawn.png", 8_450_113),
        file("kyoto notes.txt", 2_310),
      ]),
      file("[family] birthday cake.jpg", 2_871_554),
      file("[travel sunset] beach walk.jpg", 3_902_118),
      file("[wallpaper] aurora night.png", 11_204_776),
      file("untagged snapshot.jpg", 1_223_090),
    ]),
    dir("Videos", [
      file("[travel japan] street timelapse.mp4", 182_400_512),
      file("[family] holiday recap.mov", 96_212_000),
      file("screen recording.webm", 24_118_223),
    ]),
    dir("Documents", [
      file("[work draft] quarterly report.docx", 182_223),
      file("[work] project brief.pdf", 421_881),
      file("[personal] recipes.md", 12_408),
      file("resume.pdf", 88_112),
      dir("Archive", [
        file("[work] 2024 summary.pdf", 302_119),
        file("old backups.zip", 48_112_004),
      ]),
    ]),
    dir("Music", [
      file("[chill] late night lofi.mp3", 8_204_552),
      file("[focus] deep work mix.flac", 41_009_212),
    ]),
    file("[important] read me first.txt", 1_204),
  ]);
}

/** Deterministic gradient placeholder rendered for demo image files. */
function renderDemoImage(name: string): Promise<Blob | null> {
  let hash = 0;
  for (const ch of name) hash = (hash * 31 + ch.charCodeAt(0)) >>> 0;
  const hue = hash % 360;
  const canvas = document.createElement("canvas");
  canvas.width = 320;
  canvas.height = 180;
  const ctx = canvas.getContext("2d");
  if (!ctx) return Promise.resolve(null);
  const gradient = ctx.createLinearGradient(0, 0, 320, 180);
  gradient.addColorStop(0, `hsl(${hue} 70% 55%)`);
  gradient.addColorStop(1, `hsl(${(hue + 70) % 360} 65% 35%)`);
  ctx.fillStyle = gradient;
  ctx.fillRect(0, 0, 320, 180);
  ctx.fillStyle = "rgba(255,255,255,0.85)";
  ctx.beginPath();
  ctx.arc(60 + (hash % 200), 40 + (hash % 100), 18 + (hash % 22), 0, Math.PI * 2);
  ctx.fill();
  return new Promise((resolve) => canvas.toBlob(resolve, "image/png"));
}

export class MemoryFileBrowser implements FileBrowserService {
  private readonly root: MemoryNode;
  private readonly rootPath: string;

  constructor(rootPath: string) {
    this.root = buildDemoTree();
    this.rootPath = rootPath;
  }

  private resolve(path: string): MemoryNode {
    const trimmed = path.trim().replace(/\/+$/, "");
    if (!trimmed.toLowerCase().startsWith(this.rootPath.toLowerCase())) {
      throw new Error(`Path is outside the current location root: ${path}`);
    }
    const rest = trimmed.slice(this.rootPath.length).replace(/^\//, "");
    let node = this.root;
    if (!rest) return node;
    for (const segment of rest.split("/")) {
      const next = node.children.get(segment.toLowerCase());
      if (!next) throw new Error(`Directory not found: ${path}`);
      node = next;
    }
    return node;
  }

  async loadDirectory(path: string): Promise<FileItem[]> {
    const node = this.resolve(path);
    if (!node.isDirectory) throw new Error(`Directory not found: ${path}`);
    return [...node.children.values()].map((child) =>
      makeItem(child, joinPath(path, child.name), ""),
    );
  }

  async listRecursive(path: string): Promise<FileItem[]> {
    const results: FileItem[] = [];
    const walk = (node: MemoryNode, nodePath: string, relative: string) => {
      for (const child of node.children.values()) {
        const childPath = joinPath(nodePath, child.name);
        results.push(makeItem(child, childPath, relative));
        if (child.isDirectory) {
          walk(child, childPath, relative === "." ? child.name : `${relative}/${child.name}`);
        }
      }
    };
    const start = this.resolve(path);
    if (!start.isDirectory) throw new Error(`Directory not found: ${path}`);
    walk(start, path.trim().replace(/\/+$/, ""), ".");
    return results;
  }

  async createDirectory(parentPath: string, preferredName: string): Promise<string> {
    const parent = this.resolve(parentPath);
    const name = uniqueName(
      new Set([...parent.children.values()].map((c) => c.name)),
      validateEntryName(preferredName),
    );
    parent.children.set(name.toLowerCase(), dir(name));
    return joinPath(parentPath, name);
  }

  async rename(path: string, newName: string): Promise<string> {
    const name = validateEntryName(newName);
    const parentPath = parentPathOf(path.trim().replace(/\/+$/, ""));
    if (parentPath === null) throw new Error(`Cannot rename root directory: ${path}`);
    const parent = this.resolve(parentPath);
    const oldName = baseNameOf(path.trim().replace(/\/+$/, ""));
    const node = parent.children.get(oldName.toLowerCase());
    if (!node) throw new Error(`File or directory not found: ${path}`);
    if (name === node.name) return path;
    const sameSlot = name.toLowerCase() === oldName.toLowerCase();
    if (!sameSlot && parent.children.has(name.toLowerCase())) {
      throw new Error(`Destination already exists: ${joinPath(parentPath, name)}`);
    }
    parent.children.delete(oldName.toLowerCase());
    node.name = name;
    node.modified = Date.now();
    parent.children.set(name.toLowerCase(), node);
    return joinPath(parentPath, name);
  }

  async deleteMany(paths: string[], _permanently = false): Promise<boolean> {
    for (const path of paths) {
      const normalized = path.trim().replace(/\/+$/, "");
      const parentPath = parentPathOf(normalized);
      if (parentPath === null) continue;
      const parent = this.resolve(parentPath);
      parent.children.delete(baseNameOf(normalized).toLowerCase());
    }
    return true;
  }

  async transferMany(
    paths: string[],
    destinationPath: string,
    move: boolean,
    resolutions: TransferConflictResolutions = {},
  ): Promise<boolean> {
    const destination = this.resolve(destinationPath);
    if (!destination.isDirectory) throw new Error(`Directory not found: ${destinationPath}`);
    const normalizedDestination = destinationPath.trim().replace(/\/+$/, "").toLowerCase();
    const entries = paths.map((path) => {
      const normalized = path.trim().replace(/\/+$/, "");
      const parentPath = parentPathOf(normalized);
      if (parentPath === null) throw new Error(`Cannot transfer root directory: ${path}`);
      const parent = this.resolve(parentPath);
      const name = baseNameOf(normalized);
      const node = parent.children.get(name.toLowerCase());
      if (!node) throw new Error(`File or directory not found: ${path}`);
      if (node.isDirectory && normalizedDestination.startsWith(`${normalized.toLowerCase()}/`)) {
        throw new Error("Cannot transfer a folder into itself.");
      }
      const existing = destination.children.get(name.toLowerCase());
      const action = resolutions[normalized.toLowerCase()];
      if (parent === destination) {
        if (move) {
          if (action === "skip") return null;
          throw new Error("The source and destination folders are the same.");
        }
        return { parent, name, targetName: copyName(new Set(destination.children.keys()), name), node };
      }
      if (existing) {
        if (action === "skip") return null;
        if (action === "keepBoth") {
          return { parent, name, targetName: copyName(new Set(destination.children.keys()), name), node };
        }
        if (action !== "replace") throw new Error(`Destination already exists: ${joinPath(destinationPath, name)}`);
      }
      return { parent, name, targetName: name, node, replace: Boolean(existing) };
    });
    for (const entry of entries) {
      if (!entry) continue;
      const { parent, name, targetName, node, replace } = entry;
      if (replace) destination.children.delete(targetName.toLowerCase());
      const copy = move ? node : cloneNode(node);
      if (move) parent.children.delete(name.toLowerCase());
      copy.name = targetName;
      destination.children.set(targetName.toLowerCase(), copy);
    }
    return true;
  }

  async getFileBlob(path: string): Promise<Blob | null> {
    const node = this.resolve(path);
    if (node.isDirectory) return null;
    if (previewKindFor(node.name, false) === "image") {
      return renderDemoImage(node.name);
    }
    return null;
  }
}
