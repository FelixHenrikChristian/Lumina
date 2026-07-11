# Releasing Lumina

This is the maintainer-only release checklist. Public-facing download and product
information belongs in the root READMEs.

## 1. Check the release state

- Work from `main` after all intended changes have been committed and pushed.
- Confirm the repository is public so `electron-updater` can read the release feed.
- Confirm the target `vX.Y.Z` tag and GitHub Release do not already exist.
- Never replace an existing published tag or its assets.
- Decide whether the Windows binaries will be code-signed. Unsigned releases work,
  but Windows may display Unknown Publisher and SmartScreen warnings.

## 2. Prepare the version and Changelog

```powershell
$newVersion = Read-Host "Release version"
npm version $newVersion --no-git-tag-version
$version = node -p "require('./package.json').version"
```

Move the completed entries under `Unreleased` into a dated version section in
`CHANGELOG.md`, then update its comparison links.

Verify that the intended tag matches the package version:

```powershell
node scripts/verify-release.mjs --tag "v$version"
```

## 3. Run the release checks

```powershell
npm test
npm run lint
npm run build
npm run app:smoke
npm audit --audit-level=high
npm run pack:win
```

Close any process that is running or previewing `release/win-unpacked` before
packaging. On Windows, an open `app.asar` can make electron-builder fail with
`EBUSY`.

Confirm that `release/` contains matching assets from the same build:

- `Lumina-Setup-<version>.exe`
- `Lumina-Setup-<version>.exe.blockmap`
- `Lumina-Portable-<version>.exe`
- `latest.yml`

The version and installer SHA-512 in `latest.yml` must match the generated setup
executable.

## 4. Commit, push, and tag

Commit the version and Changelog changes using the repository's commit-message
convention, then push `main` before the tag:

```powershell
git push origin main
git tag -a "v$version" -m "Lumina $version"
git push origin "v$version"
```

Pushing the tag runs `.github/workflows/release.yml`. The workflow repeats the
checks in a clean Windows runner, builds both distributions, and uploads all four
assets to a **Draft Release**. It never publishes the release automatically.

## 5. Review and publish the draft

Before clicking **Publish release**, verify:

- The generated release notes and version are correct.
- Both executables, the blockmap, and `latest.yml` are attached.
- GitHub's displayed digests belong to the assets produced by this workflow run.
- The installer starts successfully on a clean Windows system.

Draft releases are invisible to the automatic updater. After publishing, confirm
that the release feed is publicly accessible and test an update from an older
installed build.
