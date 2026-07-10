import assert from "node:assert/strict";
import { existsSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const read = (relativePath: string) => readFileSync(join(root, relativePath), "utf8");

test("GitHub Actions builds two executables and creates only a draft release", () => {
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
  assert.match(workflow, /release\/Lumina-Setup-\*\.exe/);
  assert.match(workflow, /release\/Lumina-Portable-\*\.exe/);
  assert.doesNotMatch(workflow, /SHA256SUMS|create-release-checksums/);
  assert.equal(existsSync(join(root, "scripts", "create-release-checksums.mjs")), false);
  assert.match(workflow, /RELEASE_TAG:\s*\$\{\{ github\.ref_name \}\}/);
  assert.match(workflow, /\$env:RELEASE_TAG/);
  assert.doesNotMatch(workflow, /run:[^\r\n]*\$\{\{ github\.ref_name \}\}/);
  assert.match(workflow, /gh release create[^\r\n]*--draft[^\r\n]*--verify-tag[^\r\n]*--generate-notes/);
  assert.match(workflow, /gh release upload[^\r\n]*--clobber/);
  assert.match(workflow, /already published; refusing to replace its assets/);
  assert.match(workflow, /if:\s*startsWith\(github\.ref, 'refs\/tags\/'\)/);
  assert.doesNotMatch(workflow, /gh release create[^\r\n]*--latest/);
});
