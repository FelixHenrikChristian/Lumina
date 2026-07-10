import { createHash } from "node:crypto";
import { existsSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

import { packageVersionFromTag, readPackageVersion } from "./verify-release.mjs";

export function expectedArtifactNames(version) {
  if (packageVersionFromTag(`v${version}`) === null) {
    throw new Error(`Cannot create artifact names for invalid version "${version}".`);
  }
  return [`Lumina-Setup-${version}.exe`, `Lumina-Portable-${version}.exe`];
}

export function writeReleaseChecksums({
  releaseDirectory = fileURLToPath(new URL("../release/", import.meta.url)),
  version = readPackageVersion(),
} = {}) {
  const lines = expectedArtifactNames(version).map((name) => {
    const artifactPath = join(releaseDirectory, name);
    if (!existsSync(artifactPath)) {
      throw new Error(`Missing release artifact: ${artifactPath}`);
    }
    const hash = createHash("sha256").update(readFileSync(artifactPath)).digest("hex");
    return `${hash}  ${name}`;
  });

  const outputPath = join(releaseDirectory, "SHA256SUMS.txt");
  writeFileSync(outputPath, `${lines.join("\n")}\n`, "ascii");
  return outputPath;
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  try {
    const outputPath = writeReleaseChecksums();
    console.log(`Wrote release checksums to ${outputPath}.`);
  } catch (error) {
    console.error(error instanceof Error ? error.message : error);
    process.exitCode = 1;
  }
}
