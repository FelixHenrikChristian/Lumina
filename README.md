# Lumina

Lumina is a local, tag-oriented file manager for Windows with a liquid-glass
interface. It stores tags as a leading group in each filename, for example
`[work urgent] report.pdf`, so tagged files remain portable and do not depend on
a separate database.

## Download

Download the current release from
[GitHub Releases](https://github.com/FelixHenrikChristian/Lumina/releases/latest).
Lumina supports 64-bit Windows 10 and Windows 11.

- `Lumina-Setup-1.0.0.exe` installs Lumina, adds Start menu and desktop shortcuts,
  and includes an uninstaller.
- `Lumina-Portable-1.0.0.exe` runs without installation. Preferences are still
  stored in the current Windows user's application-data directory.

> [!WARNING]
> Lumina 1.0.0 executables are unsigned. Windows may show an Unknown Publisher or
> SmartScreen warning. Download them only from the official Releases page and
> compare the SHA-256 digest shown by GitHub before running them.

```powershell
Get-FileHash .\Lumina-Setup-1.0.0.exe -Algorithm SHA256
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

Lumina processes files locally and does not include telemetry, accounts, cloud
storage, advertising, crash reporting, or automatic updates. See
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
It never publishes the release automatically.

For Lumina 1.0.0:

```powershell
git tag -a v1.0.0 -m "Lumina 1.0.0"
git push origin v1.0.0
```

After the Action succeeds, open the generated draft on GitHub, check the release
notes and both executable attachments, then click **Publish release**.

## Project information

- Changes: [CHANGELOG.md](CHANGELOG.md)
- Support: [SUPPORT.md](SUPPORT.md)
- Security: [SECURITY.md](SECURITY.md)
- Privacy: [PRIVACY.md](PRIVACY.md)
- Third-party notices: [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)

Lumina is available under the [MIT License](LICENSE).
