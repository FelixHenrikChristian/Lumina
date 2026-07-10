import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

import {
  expectedArtifactNames,
  writeReleaseChecksums,
} from "../scripts/create-release-checksums.mjs";

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const read = (relativePath: string) => readFileSync(join(root, relativePath), "utf8");

test("release artifact names are deterministic", () => {
  assert.deepEqual(expectedArtifactNames("1.0.0"), [
    "Lumina-Setup-1.0.0.exe",
    "Lumina-Portable-1.0.0.exe",
  ]);
});

test("checksum script hashes both public artifacts", () => {
  const directory = mkdtempSync(join(tmpdir(), "lumina-release-"));
  try {
    const [setup, portable] = expectedArtifactNames("1.0.0");
    writeFileSync(join(directory, setup), "setup fixture");
    writeFileSync(join(directory, portable), "portable fixture");

    const outputPath = writeReleaseChecksums({ releaseDirectory: directory, version: "1.0.0" });
    const lines = readFileSync(outputPath, "utf8").trim().split("\n");
    const setupHash = createHash("sha256").update("setup fixture").digest("hex");
    const portableHash = createHash("sha256").update("portable fixture").digest("hex");

    assert.deepEqual(lines, [`${setupHash}  ${setup}`, `${portableHash}  ${portable}`]);
  } finally {
    rmSync(directory, { recursive: true, force: true });
  }
});

test("checksum script fails when an artifact is missing", () => {
  const directory = mkdtempSync(join(tmpdir(), "lumina-release-"));
  try {
    assert.throws(
      () => writeReleaseChecksums({ releaseDirectory: directory, version: "1.0.0" }),
      /Missing release artifact/,
    );
  } finally {
    rmSync(directory, { recursive: true, force: true });
  }
});

test("GitHub Actions builds artifacts and creates only a draft release", () => {
  const workflow = read(".github/workflows/release.yml");
  assert.match(workflow, /workflow_dispatch:/);
  assert.match(workflow, /tags:\s*\r?\n\s*- ["']v\*\.\*\.\*["']/);
  assert.match(workflow, /contents:\s*write/);
  assert.match(workflow, /cancel-in-progress:\s*false/);
  assert.match(workflow, /actions\/checkout@v6/);
  assert.match(workflow, /actions\/setup-node@v6/);
  assert.match(workflow, /node-version:\s*24/);
  assert.match(workflow, /actions\/upload-artifact@v7/);
  assert.match(workflow, /node scripts\/verify-release\.mjs/);
  assert.match(workflow, /npm test/);
  assert.match(workflow, /npm run lint/);
  assert.match(workflow, /npm run build/);
  assert.match(workflow, /npm run app:smoke/);
  assert.match(workflow, /npm run pack:win/);
  assert.match(workflow, /node scripts\/create-release-checksums\.mjs/);
  assert.match(workflow, /RELEASE_TAG:\s*\$\{\{ github\.ref_name \}\}/);
  assert.match(workflow, /\$env:RELEASE_TAG/);
  assert.doesNotMatch(workflow, /run:[^\r\n]*\$\{\{ github\.ref_name \}\}/);
  assert.match(workflow, /gh release create[^\r\n]*--draft[^\r\n]*--verify-tag[^\r\n]*--generate-notes/);
  assert.match(workflow, /gh release upload[^\r\n]*--clobber/);
  assert.match(workflow, /already published; refusing to replace its assets/);
  assert.match(workflow, /if:\s*startsWith\(github\.ref, 'refs\/tags\/'\)/);
  assert.doesNotMatch(workflow, /gh release create[^\r\n]*--latest/);
});
