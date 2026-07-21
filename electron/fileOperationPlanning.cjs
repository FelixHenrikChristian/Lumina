// Pure planning for native transfer/paste operations: turns a source list
// plus per-item conflict resolutions (decided in Lumina's themed dialog)
// into sequential Shell operation batches. Kept free of Electron imports so
// tests can exercise it directly.
const path = require("node:path");

const RESOLUTION_ACTIONS = new Set(["replace", "skip", "keepBoth"]);

/** Validates a renderer-supplied resolutions record; returns a clean copy. */
function normalizeResolutions(resolutions) {
  const clean = {};
  if (!resolutions || typeof resolutions !== "object") return clean;
  for (const [key, value] of Object.entries(resolutions)) {
    if (typeof key === "string" && RESOLUTION_ACTIONS.has(value)) {
      clean[key.toLowerCase()] = value;
    }
  }
  return clean;
}

function isSameDirectory(source, destination) {
  return path.dirname(source).toLowerCase() === destination.toLowerCase();
}

/**
 * Splits sources into Shell batches. "skip" items are dropped; "keepBoth"
 * items run with renameOnCollision; copies pasted into their own folder
 * auto-rename like Explorer's "- Copy". Everything runs with
 * noConfirmation so the Shell can never raise its own conflict prompt —
 * conflicts were already resolved (or never existed) by the time we run.
 */
function planTransferBatches(sources, destination, move, resolutions) {
  const clean = normalizeResolutions(resolutions);
  const action = move ? "move" : "copy";
  const sameDirectoryCopies = [];
  const keepBoth = [];
  const regular = [];
  for (const source of sources) {
    const resolution = clean[source.toLowerCase()];
    if (resolution === "skip") continue;
    if (!move && isSameDirectory(source, destination)) sameDirectoryCopies.push(source);
    else if (resolution === "keepBoth") keepBoth.push(source);
    else regular.push(source);
  }
  const batches = [];
  if (sameDirectoryCopies.length > 0) {
    batches.push({ action: "copy", sources: sameDirectoryCopies, renameOnCollision: true });
  }
  if (keepBoth.length > 0) {
    batches.push({ action, sources: keepBoth, renameOnCollision: true });
  }
  if (regular.length > 0) {
    batches.push({ action, sources: regular, renameOnCollision: false });
  }
  return batches;
}

/**
 * Explorer refuses dropping a folder into itself or its own subtree; the
 * Shell would otherwise fail mid-operation with an opaque error. Returns the
 * offending source, or null when the import is safe.
 */
function findRecursiveImportSource(sources, destination) {
  const target = destination.toLowerCase();
  for (const source of sources) {
    const prefix = source.toLowerCase();
    if (target === prefix || target.startsWith(`${prefix}\\`) || target.startsWith(`${prefix}/`)) {
      return source;
    }
  }
  return null;
}

module.exports = { findRecursiveImportSource, normalizeResolutions, planTransferBatches };
