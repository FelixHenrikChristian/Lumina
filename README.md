# Lumina (React)

A web rewrite of [Lumina](../Lumina), the WinUI 3 tag-oriented file manager, restyled
with a **liquid glass** theme. Tags live in filenames as a leading bracket group
(`[tag1 tag2] name.ext`), so the library stays portable and database-free — the same
convention as the WinUI app and TagAnything/TagSpaces.

## Liquid glass

The theme uses [`liquid-glass-react`](https://github.com/rdev/liquid-glass-react)
(rdev) for hero surfaces — dialogs render on a `LiquidGlass` panel with displacement,
refraction, and chromatic aberration. Large structural panels (sidebar, explorer,
menus, popovers) use a matching CSS glass system (`backdrop-filter` blur/saturation,
inner highlights, aurora wallpaper) so the whole UI reads as one material without
paying the mouse-tracking cost on every element.

> Why not [`@callstack/liquid-glass`](https://github.com/callstack/liquid-glass)?
> It is a React Native binding to the native iOS 26 Liquid Glass material (requires
> Xcode 26), so it cannot run in a browser or on Windows.

The displacement effect is fully visible in Chromium (Chrome/Edge); Safari/Firefox
degrade to plain frosted blur.

## Features

- **Locations** — manage real folders via the File System Access API (Chrome/Edge;
  handles persist in IndexedDB and permissions are re-requested on return), or an
  in-memory **demo library** that works in any browser.
- **File explorer** — zoomable card grid (Ctrl+wheel or toolbar), thumbnails for
  images, name/modified/type/size sorting (directories first), breadcrumbs,
  back/forward/up history, folder creation, rename (F2), delete, recursive search
  with parent-folder badges, and the full keyboard model from
  [`docs/file-explorer-input.md`](../Lumina/docs/file-explorer-input.md)
  (arrows by row/column, Shift/Ctrl selection, Ctrl+A, Home/End, Enter, F5,
  Alt+arrows, Backspace, Ctrl+F).
- **Tags** — grouped tag library with colors, drag chips onto files to tag them
  (the filename is rewritten), drag chips between files to move tags, right-click
  to remove; filter the explorer by any tag combination; TagSpaces-compatible
  JSON import/export.
- **Settings** — English/简体中文 (or follow the system), hide file extensions,
  toggle parent-folder badges. Everything persists to `localStorage`
  (`lumina.*` keys), mirroring the WinUI JSON stores.

## Project layout

- `src/core` — UI-independent port of `Lumina.Core`: models, tag parser, sorting,
  localization, stores, TagSpaces transfer.
- `src/fs` — `FileBrowserService` abstraction with File System Access and
  in-memory demo adapters.
- `src/state` — zustand store mirroring the WinUI ViewModels (navigation history,
  selection/focus/anchor, filters, file operations).
- `src/components` — the liquid-glass UI: sidebars, explorer grid, dialogs,
  menus, icons.

## Run

```powershell
npm install
npm run dev      # http://localhost:5173
npm run build    # type-check + production bundle
npm run lint
```

Use Chrome or Edge to manage real folders; any browser can use the demo library.
