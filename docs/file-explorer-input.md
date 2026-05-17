# File Explorer Input Reference

This document records the mouse and keyboard behavior implemented for the
file explorer grid in `src/Lumina.App/Views/FileExplorerView`.

## Mouse

| Input | Behavior |
| --- | --- |
| Click a file card | Selects that file or folder and focuses the file grid. |
| Click empty grid space | Clears the current selection and focuses the file grid. |
| Shift + click a file card | Extends selection from the current anchor to the clicked file card. |
| Ctrl + click a file card | Toggles the clicked file card in or out of the selection. |
| Double-click a folder card | Opens that folder inside Lumina. |
| Double-click a file card | Opens the file with the Windows default app. |
| Mouse wheel | Scrolls the current folder vertically. |
| Ctrl + mouse wheel | Zooms file cards in or out using the configured grid zoom levels. |

## Keyboard

Keyboard navigation applies when the file grid has focus. Clicking a card or
empty grid space gives focus to the file grid.

| Key | Behavior |
| --- | --- |
| Left Arrow | Selects the previous file card. |
| Right Arrow | Selects the next file card. |
| Up Arrow | Moves selection to the previous row using the current grid column count. |
| Down Arrow | Moves selection to the next row using the current grid column count. |
| Shift + Arrow | Extends selection from the selection anchor in the arrow direction. |
| Ctrl + Arrow | Moves keyboard focus without changing the selected files. |
| Ctrl + Space | Toggles the focused file card in or out of the selection. |
| Ctrl + A | Selects all file cards in the current folder. |
| Home | Selects the first file card. |
| End | Selects the last file card. |
| Enter | Opens the selected file or folder. |
| F2 | Renames the focused file or folder. |
| Delete | Moves the selected files or folders to the Recycle Bin. |
| Ctrl + D | Moves the selected files or folders to the Recycle Bin. |
| Shift + Delete | Permanently deletes the selected files or folders after confirmation. |
| Ctrl + C | Copies the selected files or folders. |
| Ctrl + X | Cuts the selected files or folders. |
| Ctrl + V | Pastes copied or cut files into the current folder. |
| Ctrl + F | Focuses folder search. |
| Ctrl + E | Focuses folder search. |
| Escape | Clears the current selection. |
| F5 | Refreshes the current folder. |
| Alt + Up Arrow | Opens the parent folder. |
| Alt + Left Arrow | Navigates backward in folder history. |
| Alt + Right Arrow | Navigates forward in folder history. |
| Backspace | Navigates backward in folder history. |

## Implementation Notes

- Navigation keys are handled with `PreviewKeyDown` so selection moves before
  `ScrollViewer` can consume arrow, Home, or End keys for scrolling.
- After keyboard selection changes, the view scrolls just enough to keep the
  selected file card visible.
- Up and Down movement uses the current rendered grid column count, so it stays
  aligned with Ctrl + mouse wheel zoom changes.
- Multi-select separates keyboard focus from selection so Ctrl + Arrow can move
  the focus rectangle without changing selected file cards.
