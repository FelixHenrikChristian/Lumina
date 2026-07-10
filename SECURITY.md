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

Lumina 1.0.0 Windows executables are unsigned. Windows may display an Unknown
Publisher or SmartScreen warning. Download binaries only from the official
[GitHub Releases page](https://github.com/FelixHenrikChristian/Lumina/releases).

Every release includes `SHA256SUMS.txt`. Compare the SHA-256 value of your download
with that file before running it:

```powershell
Get-FileHash .\Lumina-Setup-1.0.0.exe -Algorithm SHA256
```

A checksum detects corruption or an unexpected file, but it does not replace a
digital signature. Treat a checksum copied from any unofficial location as
untrusted.
