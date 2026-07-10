import { readFileSync } from "node:fs";
import { pathToFileURL } from "node:url";

const releaseTagPattern = /^v(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$/;

export function packageVersionFromTag(tag) {
  return releaseTagPattern.test(tag) ? tag.slice(1) : null;
}

export function assertReleaseTagMatchesVersion(tag, version) {
  const taggedVersion = packageVersionFromTag(tag);
  if (taggedVersion === null) {
    throw new Error(`Release tag "${tag}" must use the form vX.Y.Z.`);
  }
  if (taggedVersion !== version) {
    throw new Error(`Release tag version ${taggedVersion} does not match package version ${version}.`);
  }
}

export function readPackageVersion() {
  const packageJson = JSON.parse(readFileSync(new URL("../package.json", import.meta.url), "utf8"));
  const version = packageJson.version;
  if (typeof version !== "string" || packageVersionFromTag(`v${version}`) === null) {
    throw new Error(`package.json version "${String(version)}" is not a valid release version.`);
  }
  return version;
}

function main(args) {
  const version = readPackageVersion();
  if (args.length === 0) {
    console.log(`Verified package version ${version}.`);
    return;
  }
  if (args.length !== 2 || args[0] !== "--tag") {
    throw new Error("Usage: node scripts/verify-release.mjs [--tag vX.Y.Z]");
  }
  assertReleaseTagMatchesVersion(args[1], version);
  console.log(`Verified release tag ${args[1]} for package version ${version}.`);
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  try {
    main(process.argv.slice(2));
  } catch (error) {
    console.error(error instanceof Error ? error.message : error);
    process.exitCode = 1;
  }
}
