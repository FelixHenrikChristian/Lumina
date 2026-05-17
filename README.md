# Lumina

Lumina is the WinUI 3 migration target for TagAnything. The first scaffold keeps the native Windows shell separate from the legacy Electron code so file, tag, and settings logic can move across in small slices.

## Project Layout

- `src/Lumina.App`: WinUI 3 packaged desktop app and XAML views.
- `src/Lumina.Core`: UI-independent models and services.
- `tests/Lumina.Core.Tests`: unit tests for migration-safe core logic.
- `docs/file-explorer-input.md`: file explorer mouse and keyboard input reference.

## Current Scope

- WinUI shell with a left `NavigationView`.
- Sidebar placeholders for locations, tags, and settings.
- File explorer placeholder for the future virtualized grid.
- Core models for locations, tags, tag groups, files, and display settings.
- JSON settings store targeting `%LocalAppData%\Lumina`.
- File-name tag parser compatible with `[tag1 tag2] filename.ext`.

## Build

```powershell
dotnet restore .\Lumina.sln
dotnet test .\tests\Lumina.Core.Tests\Lumina.Core.Tests.csproj

$Platform = $env:PROCESSOR_ARCHITECTURE
dotnet build .\src\Lumina.App\Lumina.App.csproj -c Debug -p:Platform=$Platform
```

## Migration Notes

The initial migration follows `..\TagAnything\docs\winui3-migration-plan.md`: keep the app native, keep core logic outside XAML, persist app data as JSON, and continue using file-name tags instead of introducing a database in the first pass.
