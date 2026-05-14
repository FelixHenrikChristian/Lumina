# Agent Notes

- Keep UI-specific code in `src/Lumina.App`.
- Keep file, tag, search, rename, and persistence logic in `src/Lumina.Core`.
- Add tests under `tests/Lumina.Core.Tests` for public core behavior.
- Prefer WinUI-native controls and Windows App SDK patterns over Electron-era abstractions.
- Do not migrate legacy `localStorage` directly; import from an explicit exported JSON file later.
