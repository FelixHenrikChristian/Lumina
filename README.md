# Lumina

**English** | [简体中文](README.zh-CN.md)

Lumina is a local, tag-oriented file manager for Windows with a liquid-glass
interface. It stores tags as a leading group in each filename, for example
`[work urgent] report.pdf`, so tagged files remain portable and do not depend on
a separate database.

## Download

Download the current release from
[GitHub Releases](https://github.com/FelixHenrikChristian/Lumina/releases/latest).
Lumina supports 64-bit Windows 10 and Windows 11.

- `Lumina-Setup-<version>.exe` installs Lumina, adds Start menu and desktop
  shortcuts, and includes an uninstaller.
- `Lumina-Portable-<version>.exe` runs without installation. Preferences are still
  stored in the current Windows user's application-data directory.

Versions released before automatic update support must be upgraded manually
once; subsequent installed releases can be updated from inside Lumina. Portable
builds continue to be replaced manually.

> [!WARNING]
> Lumina's Windows executables are currently unsigned. Windows may show an
> Unknown Publisher or SmartScreen warning. Download them only from the official
> Releases page and compare the SHA-256 digest shown by GitHub before running them.

```powershell
Get-FileHash .\Lumina-Setup-*.exe -Algorithm SHA256
```

## Features

- Native folder picker and local Windows filesystem access.
- Grid view with image and Windows Shell video thumbnails.
- Breadcrumbs, navigation history, recursive search, sorting, zoom, multi-select,
  and keyboard navigation.
- Folder creation, rename, copy, move, paste, undo/redo paste, Recycle Bin, open
  with the default application, and reveal in File Explorer.
- Tag groups, tag colors, drag-and-drop tagging, multi-tag filtering, and
  TagSpaces-style import/export.
- English and Simplified Chinese interfaces.
- Custom wallpapers and configurable liquid-glass appearance.
- Automatic update checks for installed builds, with user-controlled download,
  progress, and restart; portable builds link to the manual download.

Lumina processes files locally and does not include telemetry, accounts, cloud
storage, advertising, or crash reporting. Update checks contact the official
GitHub Releases feed but do not upload managed files, tags, or usage data. See
[PRIVACY.md](PRIVACY.md) for details.

## Development

Requirements:

- Windows 10 or Windows 11
- Node.js 22.12 or newer
- npm

Install the locked dependencies and run the checks:

```powershell
npm ci
npm test
npm run lint
npm run build
npm run app:smoke
```

Useful commands:

```powershell
npm run dev       # browser development server
npm run app:dev   # Vite + Electron with live reload
npm run dist      # production build, installer, and portable executable
```

The Electron renderer uses context isolation with Node.js integration disabled.
Native filesystem operations are exposed through explicit IPC handlers and are
validated against folder roots selected by the user.

## Release process

The release workflow runs on a `vX.Y.Z` tag. It verifies that the tag matches
`package.json`, runs tests and the desktop smoke test on a GitHub-hosted Windows
runner, builds both executables, and creates a **Draft Release**.
It also uploads the update metadata and differential-download blockmap, but it
never publishes the release automatically. Update clients cannot see the draft.

Before preparing a release, update `package.json` and `package-lock.json`
together. Published tags and their assets must never be replaced.

```powershell
$newVersion = Read-Host "Release version"
npm version $newVersion --no-git-tag-version
$version = node -p "require('./package.json').version"
git tag -a "v$version" -m "Lumina $version"
git push origin "v$version"
```

After the Action succeeds, open the generated draft on GitHub and check the
release notes, both executables, `latest.yml`, and the installer blockmap. Click
**Publish release** only after those assets have been verified.

## Project information

- Changes: [CHANGELOG.md](CHANGELOG.md)
- Support: [SUPPORT.md](SUPPORT.md)
- Security: [SECURITY.md](SECURITY.md)
- Privacy: [PRIVACY.md](PRIVACY.md)
- Third-party notices: [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)

Lumina is available under the [MIT License](LICENSE).
