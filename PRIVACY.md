# Privacy

Effective date: July 10, 2026

Lumina is a local Windows file manager. Your files, folders, tags, thumbnails,
settings, and custom wallpapers are processed locally on your device.

## Data Lumina handles

Lumina can access folders you select so it can list files, generate thumbnails,
rename items, create folders, move or copy files, and send items to the Recycle
Bin. Tags are stored directly in filenames. Application preferences and selected
locations are stored in Lumina's local Electron user-data directory.

Lumina does not include telemetry, analytics, advertising, user accounts, cloud
storage, crash reporting, or an automatic-update client. It does not upload your
file contents or usage data to the project maintainer.

## Network access

The desktop application does not need a network connection for its file-management
features. If Lumina opens an external web link, that link is handled by your default
browser and is subject to the destination site's privacy policy. Downloading Lumina
from GitHub is separately governed by GitHub's privacy terms.

## Removing local data

Uninstalling Lumina does not automatically delete user preferences. After closing
Lumina, you can remove its local settings by deleting `%APPDATA%\Lumina`. This does
not delete files in any folder you managed with Lumina.

## Questions

For a privacy question, open a minimal report in the
[Lumina issue tracker](https://github.com/FelixHenrikChristian/Lumina/issues) and
do not include private file paths or sensitive file contents.
