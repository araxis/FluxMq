# V1 Candidate Notes

Last checked: 2026-06-02

These notes summarize the current V1 candidate state. Use them with the [release readiness checklist](release-readiness.md).

## Candidate Status

The current candidate is ready for focused manual candidate testing. The command gates and Windows package gate have passed, and the packaged desktop app opened without an obvious shell-level blocker during manual smoke testing.

Richer component configuration dialogs and edit views remain planned designer polish. Treat them as release blockers only when a concrete candidate workflow cannot be completed.

## Validated Commands

Local command gate:

```powershell
dotnet restore .\FluxMq.sln --nologo
dotnet test .\FluxMq.sln --no-restore --nologo -m:1 -p:UseSharedCompilation=false -p:UseAppHost=false --verbosity minimal
.\eng\verify-samples.ps1
```

Observed result:

- solution tests passed with 565 tests
- broker-free sample verification passed
- generated traffic sample validated and ran for a bounded duration

Windows package gate:

```powershell
dotnet restore .\FluxMq.sln -p:RuntimeIdentifierOverride=win-x64 -p:RuntimeIdentifier=win-x64 --nologo
dotnet test .\FluxMq.sln --configuration Release --no-restore --verbosity minimal -m:1 -p:RuntimeIdentifierOverride=win-x64 -p:RuntimeIdentifier=win-x64 -p:UseSharedCompilation=false --nologo
.\eng\package-windows.ps1 -Configuration Release -Version 0.1.0
```

Observed result:

- release-shaped solution tests passed with 565 tests
- portable Windows package was created
- MSI installer was created from the same publish output

## Candidate Artifacts

Expected local artifacts:

- `artifacts\windows\dist\FluxMQ-0.1.0-portable-win-x64.zip`
- `artifacts\windows\dist\FluxMQ-0.1.0-win-x64.msi`

The portable app smoke check used:

- `artifacts\windows\portable\FluxMQ\FluxMq.UI.exe`

## Manual Candidate Focus

Use the packaged desktop build for these checks:

- create or open an app definition
- validate, run, and stop a pipeline
- inspect live MQTT traffic through a pipeline artifact
- verify dashboard live mode uses the full width without the live side panel
- verify dashboard counter, latest, and rate widgets update from app runtime events
- run a test scenario that publishes and expects MQTT events
- verify Logs filtering by scope, level, and search

## Repackage Rules

Run the Windows package gate again before publishing if any of these change:

- runtime code
- UI code or visual assets
- package references
- `Directory.Build.props`
- Windows project settings
- packaging scripts
- sample definitions used as release smoke inputs
- version number or artifact naming

Docs-only and memory-only changes do not require repackaging unless they change release commands or candidate artifact expectations.
