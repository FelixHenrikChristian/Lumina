# Security Policy

## Supported versions

Only the latest published version of Lumina receives security fixes.

## Reporting a vulnerability

Use GitHub's **Private vulnerability reporting** for this repository when it is
available. Do not disclose exploit details, private file paths, or sensitive data
in a public issue. If private reporting is unavailable, open a public issue that
only asks the maintainer to establish private contact.

Include the affected Lumina version, Windows version, impact, reproduction steps,
and any suggested mitigation. Please allow reasonable time for investigation before
public disclosure.

## Release authenticity

Lumina's Windows executables are currently unsigned. Windows may display an
Unknown Publisher or SmartScreen warning. Download binaries only from the
official [GitHub Releases page](https://github.com/FelixHenrikChristian/Lumina/releases).

GitHub displays an immutable SHA-256 digest next to each uploaded release asset.
Calculate the digest of your download and compare it with the value shown on the
official release page before running it:

```powershell
Get-FileHash .\Lumina-Setup-*.exe -Algorithm SHA256
```

A digest detects corruption or an unexpected file, but it does not replace a
digital signature. Treat a digest copied from any unofficial location as untrusted.

## Automatic update security

Update-enabled installed builds retrieve release metadata and installers only from
the official GitHub repository. The updater validates the installer's SHA-512 value
against `latest.yml` before offering a restart. Draft releases remain invisible to
clients until the maintainer reviews and publishes them.

The current Windows binaries are still unsigned. Update metadata and hashes protect
against accidental corruption, but they do not provide the publisher identity of a
Windows code-signing certificate. Code signing is recommended before relying on
unattended distribution.
