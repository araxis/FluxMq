# V1 Candidate Notes

Last checked: 2026-06-05

These notes summarize the current V1 candidate state. Use them with the [release readiness checklist](release-readiness.md).

## Candidate Status

The current candidate is ready for PR review after focused packaged-desktop QA. The command gates, release-shaped tests, sample validation, docs-site build, and Windows package gate passed. The packaged desktop app opened `samples\flow-applications\operations-dashboard-test-studio.json` and completed the focused dashboard/test-studio workflow.

Concrete blockers found and fixed in this QA slice:

- generated source traffic now projects runtime source envelopes into dashboard `mqtt.message.received` events, so generated traffic drives Dashboard Live widgets
- Dashboard Live now keeps the saved grid on wide windows but switches to a scrollable live feed on medium/narrow windows; narrow shells hide auxiliary side panels so widgets remain usable
- Test Studio now occupies the full artifact region instead of the empty tool-column width, so Runner Console timeline, live events, diagnosis, logs, and report actions are visible
- compact payload/QoS distribution rows now fit small widget cards without horizontal clipping

No public schema, widget type, step type, package, or FluxFlow changes were made.

## Validated Commands

Local command gate:

```powershell
dotnet restore .\FluxMq.sln
dotnet test .\FluxMq.sln --no-restore --nologo -m:1 -p:UseSharedCompilation=false -p:UseAppHost=false --verbosity minimal
.\eng\verify-samples.ps1
```

Observed result:

- restore passed
- solution tests passed with 615 tests
- broker-free sample verification passed
- generated traffic sample validated and ran for a bounded duration

Windows package gate:

```powershell
dotnet restore .\FluxMq.sln -p:RuntimeIdentifierOverride=win-x64 -p:RuntimeIdentifier=win-x64 --nologo
dotnet test .\FluxMq.sln --configuration Release --no-restore --verbosity minimal -m:1 -p:RuntimeIdentifierOverride=win-x64 -p:RuntimeIdentifier=win-x64 -p:UseSharedCompilation=false --nologo
.\eng\package-windows.ps1 -Configuration Release -Version 0.1.0
```

Observed result:

- release-shaped restore passed
- release-shaped solution tests passed with 615 tests
- portable Windows package was created
- MSI installer was created from the same publish output

Additional validation:

```powershell
dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --nologo -p:UseSharedCompilation=false -p:UseAppHost=false --verbosity minimal
dotnet run --project src\FluxMq.Cli\FluxMq.Cli.csproj -- validate --config samples\flow-applications\operations-dashboard-test-studio.json --output json
npm run build
```

Observed result:

- focused UI tests passed with 303 tests
- operations dashboard/test-studio sample returned `isValid: true`
- docs-site build passed

## Candidate Artifacts

Expected local artifacts:

- `artifacts\windows\dist\FluxMQ-0.1.0-portable-win-x64.zip`
- `artifacts\windows\dist\FluxMQ-0.1.0-win-x64.msi`

The packaged desktop QA used:

- `artifacts\windows\portable\FluxMQ\FluxMq.UI.exe --open samples\flow-applications\operations-dashboard-test-studio.json`

## Manual Candidate Results

Packaged desktop checks completed on 2026-06-05:

- delete confirmations appeared for pipeline `operationsMonitor`, dashboard `operations`, and test `operationsSmoke`; each was canceled without mutating the sample
- active-artifact fallback remains covered by service tests for pipeline, dashboard, and test removal
- running the sample pipeline generated three runtime source events and drove Dashboard Live widgets
- Dashboard Live was checked at wide `1366x900`, medium `900x900`, and narrow `480x900`; final checks reported no widget horizontal overflow
- Dashboard Design mode showed the 4-column / 3-row / 7-cell / 7-widget layout, track controls, widget edit actions, and KPI widget settings dialog; switching Design to Live and back preserved the design counts
- Runner Console ran `operationsSmoke` with the app runtime event stream attached; the run passed and showed preflight, timeline, live events, diagnosis, runner logs, and report/history actions
- Logs page exposed scope, level, and search filters; the runner-owned test log stayed under Test runner scope and did not increase dashboard message metrics
- Dashboard Live still showed exactly three `mqtt.message.received` rows after the runner completed

Screenshots captured during local QA are under `artifacts\windows\candidate-*.png` and are not part of the tracked source tree.

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
