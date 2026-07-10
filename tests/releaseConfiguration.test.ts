import assert from "node:assert/strict";
import { existsSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

import {
  assertReleaseTagMatchesVersion,
  packageVersionFromTag,
} from "../scripts/verify-release.mjs";

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const read = (relativePath: string) => readFileSync(join(root, relativePath), "utf8");
const packageJson = JSON.parse(read("package.json"));
const packageLock = JSON.parse(read("package-lock.json"));

test("release metadata has one public 1.0.0 identity", () => {
  assert.equal(packageJson.version, "1.0.0");
  assert.equal(packageLock.version, "1.0.0");
  assert.equal(packageLock.packages[""].version, "1.0.0");
  assert.equal(packageJson.author, "FelixHenrikChristian");
  assert.equal(packageJson.license, "MIT");
  assert.equal(packageJson.homepage, "https://github.com/FelixHenrikChristian/Lumina#readme");
  assert.equal(packageJson.repository.url, "git+https://github.com/FelixHenrikChristian/Lumina.git");
  assert.equal(packageJson.bugs.url, "https://github.com/FelixHenrikChristian/Lumina/issues");
  assert.equal(packageJson.engines.node, ">=22.12.0");
});

test("Electron is the only desktop packager", () => {
  assert.equal(packageJson.scripts.tauri, undefined);
  assert.equal(packageJson.scripts["pack:win"], "electron-builder --win --publish never");
  assert.equal(existsSync(join(root, "src-tauri")), false);
});

test("Windows packages use explicit icons and stable artifact names", () => {
  const build = packageJson.build;
  assert.equal(build.appId, "com.felixhenrikchristian.lumina");
  assert.equal(build.productName, "Lumina");
  assert.equal(build.copyright, "Copyright © 2026 FelixHenrikChristian");
  assert.equal(build.directories.buildResources, "build");
  assert.equal(build.win.icon, "build/icon.ico");
  assert.deepEqual(build.win.target, [
    { target: "nsis", arch: ["x64"] },
    { target: "portable", arch: ["x64"] },
  ]);
  assert.equal(build.nsis.artifactName, "Lumina-Setup-${version}.${ext}");
  assert.equal(build.portable.artifactName, "Lumina-Portable-${version}.${ext}");
  assert.equal(existsSync(join(root, "build", "icon.ico")), true);
  assert.equal(existsSync(join(root, "build", "icon.png")), true);
});

test("release tags are strict and match package versions", () => {
  assert.equal(packageVersionFromTag("v1.0.0"), "1.0.0");
  assert.equal(packageVersionFromTag("1.0.0"), null);
  assert.equal(packageVersionFromTag("v1.0"), null);
  assert.equal(packageVersionFromTag("v1.0.0.0"), null);
  assert.doesNotThrow(() => assertReleaseTagMatchesVersion("v1.0.0", "1.0.0"));
  assert.throws(
    () => assertReleaseTagMatchesVersion("v1.0.1", "1.0.0"),
    /does not match package version/,
  );
});
