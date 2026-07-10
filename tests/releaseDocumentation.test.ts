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
  "README.zh-CN.md",
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
  assert.match(security, /SHA-256 digest/i);
  assert.doesNotMatch(security, /SHA256SUMS\.txt/);
});

test("support, notices, and changelog point users to the right resources", () => {
  const support = read("SUPPORT.md");
  const notices = read("THIRD_PARTY_NOTICES.md");
  const changelog = read("CHANGELOG.md");
  assert.match(support, /github\.com\/FelixHenrikChristian\/Lumina\/issues/);
  for (const dependency of ["Electron", "electron-updater", "React", "Zustand", "liquid-glass-react"]) {
    assert.match(notices, new RegExp(dependency, "i"));
  }
  assert.match(changelog, /## \[1\.0\.0\] - 2026-07-10/);
});

test("README explains version-neutral downloads, unsigned builds, and tag-based releases", () => {
  const readme = read("README.md");
  assert.match(readme, /releases\/latest/);
  assert.match(readme, /Lumina-Setup-<version>\.exe/);
  assert.match(readme, /Lumina-Portable-<version>\.exe/);
  assert.match(readme, /unsigned/i);
  assert.match(readme, /npm version \$newVersion --no-git-tag-version/);
  assert.match(readme, /\$version = node -p/);
  assert.match(readme, /git tag -a "v\$version"/);
  assert.match(readme, /Draft Release/);
  assert.match(readme, /Automatic update checks/i);
  assert.match(readme, /latest\.yml/);
  assert.match(readme, /before automatic update support must be upgraded manually/i);
});

test("English and Simplified Chinese READMEs stay linked and release-compatible", () => {
  const english = read("README.md");
  const chinese = read("README.zh-CN.md");
  assert.match(english, /\[简体中文\]\(README\.zh-CN\.md\)/);
  assert.match(chinese, /\[English\]\(README\.md\)/);
  assert.match(chinese, /Lumina-Setup-<version>\.exe/);
  assert.match(chinese, /Lumina-Portable-<version>\.exe/);
  assert.match(chinese, /npm version \$newVersion --no-git-tag-version/);
  assert.match(chinese, /git tag -a "v\$version"/);
  assert.match(chinese, /自动检查更新/);
  assert.match(chinese, /latest\.yml/);
});
