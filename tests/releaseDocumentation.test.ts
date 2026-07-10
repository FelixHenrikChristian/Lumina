import assert from "node:assert/strict";
import { existsSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const read = (relativePath: string) => readFileSync(join(root, relativePath), "utf8");

const requiredDocuments = [
  "LICENSE",
  "THIRD_PARTY_NOTICES.md",
  "PRIVACY.md",
  "SECURITY.md",
  "SUPPORT.md",
  "CHANGELOG.md",
  "README.md",
];

test("public release documentation is present", () => {
  for (const file of requiredDocuments) {
    assert.equal(existsSync(join(root, file)), true, `${file} should exist`);
  }
});

test("MIT license identifies the public copyright holder", () => {
  const license = read("LICENSE");
  assert.match(license, /^MIT License/m);
  assert.match(license, /Copyright \(c\) 2026 FelixHenrikChristian/);
  assert.match(license, /THE SOFTWARE IS PROVIDED "AS IS"/);
});

test("privacy and security documents describe the unsigned local application", () => {
  const privacy = read("PRIVACY.md");
  const security = read("SECURITY.md");
  assert.match(privacy, /processed locally/i);
  assert.match(privacy, /does not include telemetry/i);
  assert.match(security, /private vulnerability reporting/i);
  assert.match(security, /unsigned/i);
  assert.match(security, /SHA256SUMS\.txt/);
});

test("support, notices, and changelog point users to the right resources", () => {
  const support = read("SUPPORT.md");
  const notices = read("THIRD_PARTY_NOTICES.md");
  const changelog = read("CHANGELOG.md");
  assert.match(support, /github\.com\/FelixHenrikChristian\/Lumina\/issues/);
  for (const dependency of ["Electron", "React", "Zustand", "liquid-glass-react"]) {
    assert.match(notices, new RegExp(dependency, "i"));
  }
  assert.match(changelog, /## \[1\.0\.0\] - 2026-07-10/);
});

test("README explains downloads, unsigned builds, and tag-based releases", () => {
  const readme = read("README.md");
  assert.match(readme, /releases\/latest/);
  assert.match(readme, /Lumina-Setup-1\.0\.0\.exe/);
  assert.match(readme, /Lumina-Portable-1\.0\.0\.exe/);
  assert.match(readme, /unsigned/i);
  assert.match(readme, /git tag -a v1\.0\.0/);
  assert.match(readme, /Draft Release/);
});
