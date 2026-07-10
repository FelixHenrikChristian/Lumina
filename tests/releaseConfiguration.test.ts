import assert from "node:assert/strict";
import { createHash } from "node:crypto";
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
const readBytes = (relativePath: string) => readFileSync(join(root, relativePath));
const packageJson = JSON.parse(read("package.json"));
const packageLock = JSON.parse(read("package-lock.json"));

const pngInfo = (bytes: Buffer) => {
  assert.deepEqual([...bytes.subarray(0, 8)], [137, 80, 78, 71, 13, 10, 26, 10]);
  return {
    width: bytes.readUInt32BE(16),
    height: bytes.readUInt32BE(20),
    colorType: bytes[25],
  };
};

test("release metadata has one consistent public version identity", () => {
  assert.equal(packageVersionFromTag(`v${packageJson.version}`), packageJson.version);
  assert.equal(packageLock.version, packageJson.version);
  assert.equal(packageLock.packages[""].version, packageJson.version);
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

test("automatic updates use the public GitHub provider and NSIS differential packages", () => {
  assert.match(packageJson.dependencies["electron-updater"], /^\^6\./);
  assert.deepEqual(packageJson.build.publish, [
    {
      provider: "github",
      owner: "FelixHenrikChristian",
      repo: "Lumina",
    },
  ]);
  assert.equal(packageJson.build.nsis.differentialPackage, true);
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

test("release icons use the transparent Lumina artwork at desktop and browser sizes", () => {
  const desktopIcon = readBytes("build/icon.png");
  const windowsIcon = readBytes("build/icon.ico");
  const favicon = readBytes("public/favicon.png");

  assert.deepEqual(pngInfo(desktopIcon), { width: 1024, height: 1024, colorType: 6 });
  assert.deepEqual(pngInfo(favicon), { width: 256, height: 256, colorType: 6 });
  assert.notEqual(
    createHash("sha256").update(desktopIcon).digest("hex"),
    "6413bfe9d546e5225fc9256a0ecec48df3a1272ea4bdc91c15b8e000222a95d3",
  );
  assert.equal(windowsIcon.readUInt16LE(0), 0);
  assert.equal(windowsIcon.readUInt16LE(2), 1);
  assert.ok(windowsIcon.readUInt16LE(4) >= 8);
  assert.match(read("index.html"), /<link rel="icon" type="image\/png" href="\/favicon\.png" \/>/);
  assert.equal(existsSync(join(root, "public", "favicon.svg")), false);
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
