<p align="center">
  <img src="build/icon.png" width="132" height="132" alt="Lumina app icon">
</p>

<h1 align="center">Lumina</h1>

<p align="center">
  <strong>A local-first, tag-oriented file manager for Windows.</strong><br>
  Organize files with portable filename tags—no database or cloud account required.
</p>

<p align="center">
  <a href="https://github.com/FelixHenrikChristian/Lumina/releases/latest"><img src="https://img.shields.io/github/v/release/FelixHenrikChristian/Lumina?display_name=tag&sort=semver" alt="Latest release"></a>
  <a href="LICENSE"><img src="https://img.shields.io/github/license/FelixHenrikChristian/Lumina" alt="MIT License"></a>
  <img src="https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4?logo=windows&logoColor=white" alt="Windows 10 and 11">
</p>

<p align="center">
  <strong>English</strong> · <a href="README.zh-CN.md">简体中文</a>
</p>

Lumina stores tags as a leading group in each filename—for example,
`[work urgent] report.pdf`. Tagged files remain understandable and portable in
File Explorer, backups, removable drives, and other applications.

## Download

**[Download the latest release →](https://github.com/FelixHenrikChristian/Lumina/releases/latest)**

| Package | Best for |
| --- | --- |
| `Lumina-Setup-<version>.exe` | Regular use, Start menu and desktop shortcuts, uninstaller, and in-app updates |
| `Lumina-Portable-<version>.exe` | No-install use; update by downloading and replacing the executable manually |

Lumina supports 64-bit Windows 10 and Windows 11.

> [!WARNING]
> Lumina's Windows executables are currently unsigned. Windows may show an
> Unknown Publisher or SmartScreen warning. Download only from the official
> Releases page and compare the SHA-256 digest shown by GitHub before running.

```powershell
Get-FileHash .\Lumina-Setup-*.exe -Algorithm SHA256
```

## Highlights

| Area | Details |
| --- | --- |
| **Local access** | Native folder picker and direct access to the local Windows filesystem |
| **Browsing** | Grid view, breadcrumbs, navigation history, recursive search, sorting, zoom, multi-select, and keyboard navigation |
| **File operations** | Create folders, rename, copy, move, paste, undo/redo paste, use the Recycle Bin, open with the default application, and reveal items in File Explorer |
| **Tagging** | Portable filename tags, tag groups, tag colors, drag-and-drop tagging, multi-tag filtering, and TagSpaces-style import/export |
| **Thumbnails** | Image previews and Windows Shell video thumbnails |
| **Interface** | English and Simplified Chinese, custom wallpapers, and configurable liquid-glass effects |
| **Updates** | Installed builds automatically check for updates and let you control download, progress, and restart; portable builds open the manual download page |

## Privacy by design

Files, tags, thumbnails, settings, and wallpapers stay on your device. Lumina
does not include telemetry, accounts, cloud storage, advertising, or crash
reporting. Update checks contact the official GitHub Releases feed but do not
upload managed files, tags, or usage data. See the [privacy policy](PRIVACY.md).

## Development

<details>
<summary><strong>Build and test Lumina locally</strong></summary>

### Requirements

- Windows 10 or Windows 11
- Node.js 22.12 or newer
- npm

### Install and verify

```powershell
npm ci
npm test
npm run lint
npm run build
npm run app:smoke
```

### Common commands

```powershell
npm run dev       # browser development server
npm run app:dev   # Vite + Electron with live reload
npm run dist      # production build, installer, and portable executable
```

The Electron renderer uses context isolation with Node.js integration disabled.
Native filesystem operations are exposed through explicit IPC handlers and
validated against folder roots selected by the user.

</details>

## Project links

[Changelog](CHANGELOG.md) · [Security](SECURITY.md) · [Privacy](PRIVACY.md) ·
[Support](SUPPORT.md) · [Third-party notices](THIRD_PARTY_NOTICES.md) ·
[Maintainer release guide](docs/releasing.md)

Lumina is available under the [MIT License](LICENSE).
