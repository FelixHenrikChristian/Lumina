# Changelog

All notable changes to Lumina are documented in this file. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.2.0] - 2026-07-19

### Added

- Themed in-app progress dialog for native copy, move, and delete operations,
  driven by live Windows Shell progress and supporting mid-operation
  cancellation.
- Themed conflict resolution for pastes and transfers: replace, skip, or keep
  both, decided per item before the operation runs.

### Changed

- Native file operations no longer show Windows-owned dialogs. Shell progress
  windows, delete confirmations, conflict prompts, and error popups are
  replaced by Lumina's dialogs and in-app error messages, while Recycle Bin
  deletes and File Explorer's undo history keep working.
- Deleting always asks for confirmation inside Lumina, including on managed
  native folders.
- File-operation payloads stream over stdin, so large multi-file selections no
  longer risk the Windows command-line length limit.
- The transfer-conflict dialog is fully localized in English and Simplified
  Chinese instead of using fixed Chinese strings.

### Known limitations

- Because Windows confirmation prompts are suppressed, items too large for the
  Recycle Bin are deleted permanently without an extra size warning.

## [1.1.0] - 2026-07-11

### Added

- Automatic GitHub Releases update checks for packaged builds, including an
  in-app version status, user-approved NSIS downloads, progress, and restart.
- Portable-build update notifications that open the official manual download.
- Simplified Chinese README with matching download, development, and release
  guidance.

### Changed

- Draft releases now include `latest.yml` and the NSIS differential blockmap so
  updates become visible only after the draft is manually published.
- Public READMEs now use a product-focused bilingual layout, while version-neutral
  maintainer release steps live in `docs/releasing.md`.

## [1.0.0] - 2026-07-10

### Added

- Native Windows folder selection and filesystem operations through a sandboxed
  Electron bridge.
- Tag-oriented file organization using leading filename groups, with import and
  export compatible with TagSpaces-style data.
- Image and Windows Shell video thumbnails, recursive search, sorting, multi-select,
  keyboard navigation, and file history.
- English and Simplified Chinese interfaces, appearance controls, custom wallpapers,
  and configurable liquid-glass effects.
- NSIS installer and portable Windows x64 distributions.

### Security

- Renderer context isolation with Node.js integration disabled.
- Explicit IPC handlers that validate operations against user-selected folder roots.

### Known limitations

- Windows binaries are not digitally signed and may trigger SmartScreen warnings.
- Updates are downloaded manually from GitHub Releases; automatic updates are not
  included.

[Unreleased]: https://github.com/FelixHenrikChristian/Lumina/compare/v1.2.0...HEAD
[1.2.0]: https://github.com/FelixHenrikChristian/Lumina/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/FelixHenrikChristian/Lumina/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/FelixHenrikChristian/Lumina/releases/tag/v1.0.0
