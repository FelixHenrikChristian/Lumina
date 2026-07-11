import assert from "node:assert/strict";
import { existsSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const read = (relativePath: string) => readFileSync(join(root, relativePath), "utf8");
const packageJson = JSON.parse(read("package.json"));

const requiredDocuments = [
  "LICENSE",
  "THIRD_PARTY_NOTICES.md",
  "PRIVACY.md",
  "SECURITY.md",
  "SUPPORT.md",
  "CHANGELOG.md",
  "README.md",
  "README.zh-CN.md",
  "docs/releasing.md",
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
  const currentVersion = packageJson.version.replace(/\./g, "\\.");
  assert.match(changelog, new RegExp(`## \\[${currentVersion}\\] - \\d{4}-\\d{2}-\\d{2}`));
  assert.match(changelog, new RegExp(`compare/v${currentVersion}\\.\\.\\.HEAD`));
});

test("README is a version-neutral product page with a separate maintainer guide", () => {
  const readme = read("README.md");
  assert.match(readme, /releases\/latest/);
  assert.match(readme, /build\/icon\.png/);
  assert.match(readme, /img\.shields\.io\/github\/v\/release/);
  assert.match(readme, /Lumina-Setup-<version>\.exe/);
  assert.match(readme, /Lumina-Portable-<version>\.exe/);
  assert.match(readme, /unsigned/i);
  assert.match(readme, /Installed builds automatically check for updates/i);
  assert.match(readme, /\| \*\*File operations\*\* \|/);
  assert.match(readme, /TagSpaces-style import\/export/);
  assert.match(readme, /Privacy by design/);
  assert.match(readme, /docs\/releasing\.md/);
  assert.doesNotMatch(readme, /must be upgraded manually once/i);
  assert.doesNotMatch(readme, /npm version|git tag -a|Draft Release/);
});

test("English and Simplified Chinese READMEs stay linked and release-compatible", () => {
  const english = read("README.md");
  const chinese = read("README.zh-CN.md");
  assert.match(english, /href="README\.zh-CN\.md">简体中文<\/a>/);
  assert.match(chinese, /href="README\.md">English<\/a>/);
  assert.match(chinese, /Lumina-Setup-<version>\.exe/);
  assert.match(chinese, /Lumina-Portable-<version>\.exe/);
  assert.match(chinese, /自动检查更新/);
  assert.match(chinese, /\| \*\*文件操作\*\* \|/);
  assert.match(chinese, /兼容 TagSpaces 风格的导入与导出/);
  assert.match(chinese, /隐私优先/);
  assert.match(chinese, /docs\/releasing\.md/);
  assert.doesNotMatch(chinese, /需要手动升级一次/);
  assert.doesNotMatch(chinese, /npm version|git tag -a|Draft Release/);
});

test("maintainer release guide owns the complete version and draft workflow", () => {
  const guide = read("docs/releasing.md");
  assert.match(guide, /npm version \$newVersion --no-git-tag-version/);
  assert.match(guide, /node scripts\/verify-release\.mjs --tag "v\$version"/);
  assert.match(guide, /git tag -a "v\$version"/);
  assert.match(guide, /Draft Release/);
  assert.match(guide, /latest\.yml/);
  assert.match(guide, /Never replace an existing published tag/i);
});
