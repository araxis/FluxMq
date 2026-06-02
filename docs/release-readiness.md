# Release Readiness

This checklist is the pre-V1 release gate. Run commands from the repository root.

## Local Command Gate

```powershell
dotnet restore FluxMq.sln
dotnet test FluxMq.sln --no-restore --nologo
.\eng\verify-samples.ps1
```

Expected result:

- solution tests pass
- the CLI builds once for sample verification
- `metrics-only.json` validates
- `generated-traffic-inspect.json` validates and runs for a bounded duration without requiring a broker

## Windows Package Gate

```powershell
dotnet restore .\FluxMq.sln -p:RuntimeIdentifierOverride=win-x64 -p:RuntimeIdentifier=win-x64 --nologo
dotnet test .\FluxMq.sln --configuration Release --no-restore --verbosity minimal -m:1 -p:RuntimeIdentifierOverride=win-x64 -p:RuntimeIdentifier=win-x64 -p:UseSharedCompilation=false --nologo
dotnet tool install --global wix --version 6.0.2
.\eng\package-windows.ps1 -Configuration Release -Version 0.1.0
```

Expected result:

- release-shaped solution tests pass
- portable Windows zip is created
- MSI installer is created from the same publish output

Pull requests run a faster Debug/no-RID/no-apphost restore/test path for development feedback. Pushes to `main`, tags, and manual workflow dispatch run the release-shaped restore/test portion. Package artifact generation is manual while the project is still in active development.

## Manual UI Gate

Run these checks against the desktop app before cutting a V1 candidate:

- No active app: the live inspector/publisher toggle is hidden and the right side panel is not visible.
- Pipeline artifact: the component panel search filters available components, selected links use a visible selected style, and links with conditions show the condition affordance.
- Pipeline artifact: the live inspector/publisher panel can be opened and closed, and publish controls target app-level broker resources.
- Dashboard artifact: edit mode shows dashboard widgets; live mode uses the full dashboard width without the live side panel.
- Dashboard artifact with a local broker: configure counter, latest, and rate widgets for published MQTT messages, publish to the configured topic, and confirm all three widgets update.
- Test artifact: run a scenario that publishes and expects MQTT events; runner-owned logs appear under test scope and do not increment app dashboard widgets.
- Logs artifact: scope, level, and search filters work on the first-level Logs page; duplicate live-inspector logs are not present.

## Blocker Rules

Do not cut a V1 candidate while any of these are true:

- local tests or sample verification fail
- release-shaped Windows package validation fails
- the desktop app opens with misleading global panels on incompatible artifacts
- sample documentation points to a broker-dependent flow as the default smoke path
- developer-facing MQTT client examples use `session` when they mean an MQTT client
