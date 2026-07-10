# Privacy

Effective date: July 11, 2026

Lumina is a local Windows file manager. Your files, folders, tags, thumbnails,
settings, and custom wallpapers are processed locally on your device.

## Data Lumina handles

Lumina can access folders you select so it can list files, generate thumbnails,
rename items, create folders, move or copy files, and send items to the Recycle
Bin. Tags are stored directly in filenames. Application preferences and selected
locations are stored in Lumina's local Electron user-data directory.

Lumina does not include telemetry, analytics, advertising, user accounts, cloud
storage, or crash reporting. It does not upload your file contents, tags, paths,
settings, or usage data to the project maintainer.

## Network access

The desktop application does not need a network connection for its file-management
features. Packaged builds check the official GitHub Releases feed shortly after
startup and periodically while Lumina is running. Installed builds can download an
update after you approve it; portable builds open the release page for manual
download. These requests expose ordinary network metadata, such as your IP address
and request headers, to GitHub. The update library also creates a random staged-
rollout identifier in Lumina's local user-data directory and sends it as an update
request header so a release can be offered to a stable percentage of installations.
The identifier is not tied to a Lumina account and the requests do not include
information about files managed by Lumina.

If Lumina opens an external web link, that link is handled by your default browser
and is subject to the destination site's privacy policy. Update checks and downloads
are governed by GitHub's privacy terms.

## Removing local data

Uninstalling Lumina does not automatically delete user preferences. After closing
Lumina, you can remove its local settings by deleting `%APPDATA%\Lumina`. This does
not delete files in any folder you managed with Lumina.

## Questions

For a privacy question, open a minimal report in the
[Lumina issue tracker](https://github.com/FelixHenrikChristian/Lumina/issues) and
do not include private file paths or sensitive file contents.
