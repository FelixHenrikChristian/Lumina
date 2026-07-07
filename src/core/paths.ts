import type { LocationPathSegment } from "./models";

// Virtual paths: `loc:{locationId}` is a location root; children are
// appended with '/' ("loc:abc/sub/file.txt"). Comparisons are
// case-insensitive to match the original Windows semantics.
export function joinPath(parent: string, name: string): string {
  return `${parent.replace(/\/+$/, "")}/${name}`;
}

export function parentPathOf(path: string): string | null {
  const slash = path.lastIndexOf("/");
  return slash < 0 ? null : path.slice(0, slash);
}

export function baseNameOf(path: string): string {
  const slash = path.lastIndexOf("/");
  return slash < 0 ? path : path.slice(slash + 1);
}

function normalize(path: string): string {
  return path.trim().replace(/\/+$/, "");
}

function fold(path: string): string {
  return normalize(path).toLowerCase();
}

export function isSamePath(a: string, b: string): boolean {
  return fold(a) === fold(b);
}

/**
 * Port of LocationPathScope: sandboxes navigation inside a location root.
 * Containment is segment-boundary aware ("loc:a/RootEvil" is not inside
 * "loc:a/Root").
 */
export class LocationPathScope {
  readonly rootPath: string;

  constructor(rootPath: string) {
    const trimmed = normalize(rootPath);
    if (!trimmed) {
      throw new Error("Root path must not be empty");
    }
    this.rootPath = trimmed;
  }

  containsPath(path: string): boolean {
    if (!path.trim()) return false;
    const candidate = fold(path);
    const root = fold(this.rootPath);
    return candidate === root || candidate.startsWith(`${root}/`);
  }

  normalizeContainedPath(path: string): string {
    if (!this.containsPath(path)) {
      throw new Error(`Path is outside the current location root: ${path}`);
    }
    return normalize(path);
  }

  tryGetParentPath(path: string): string | null {
    if (!this.containsPath(path) || isSamePath(path, this.rootPath)) {
      return null;
    }
    const parent = parentPathOf(normalize(path));
    return parent !== null && this.containsPath(parent) ? parent : null;
  }

  getRelativePath(path: string): string {
    const normalized = this.normalizeContainedPath(path);
    if (isSamePath(normalized, this.rootPath)) return ".";
    return normalized.slice(this.rootPath.length + 1);
  }

  getBreadcrumbs(path: string, rootName: string): LocationPathSegment[] {
    const resolvedRootName =
      rootName.trim() || baseNameOf(this.rootPath) || this.rootPath;
    const segments: LocationPathSegment[] = [
      { name: resolvedRootName, path: this.rootPath },
    ];
    const relative = this.getRelativePath(path);
    if (relative === ".") return segments;
    let cumulative = this.rootPath;
    for (const part of relative.split("/")) {
      cumulative = joinPath(cumulative, part);
      segments.push({ name: part, path: cumulative });
    }
    return segments;
  }
}
