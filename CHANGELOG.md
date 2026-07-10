# Changelog

All notable changes to Lumina are documented in this file. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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

[Unreleased]: https://github.com/FelixHenrikChristian/Lumina/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/FelixHenrikChristian/Lumina/releases/tag/v1.0.0
