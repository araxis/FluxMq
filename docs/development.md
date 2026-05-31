# Local Development

## Requirements

- .NET SDK compatible with `global.json`.

The current solution targets .NET 10 projects and is built with a preview SDK in the active development environment.

## Restore, Build, Test

```powershell
dotnet restore FluxMq.sln
dotnet build FluxMq.sln --no-restore
dotnet test FluxMq.sln --no-build
```

## Repository Layout

```text
/src
  /FluxMq.Core        domain models, MQTT client, topic index, payload inspection
  /FluxMq.Components  concrete flow components, replay orchestration, LiteDB persistence
  /FluxMq.App         app definition, validation, runtime composition, host boundary
  /FluxMq.Scenarios   scenario/test-runner primitives
  /FluxMq.Cli         command-line validation and run host
  /FluxMq.UI          MAUI Blazor Hybrid desktop workspace
/tests
  /FluxMq.Core.Tests
  /FluxMq.Components.Tests
  /FluxMq.App.Tests
  /FluxMq.Scenarios.Tests
  /FluxMq.Cli.Tests
  /FluxMq.UI.Tests
/docs
  contributor and Wiki-ready documentation
/eng
  repeatable local build and packaging scripts
/installer
  Windows installer authoring
/memory
  decisions, roadmap, progress, and working context
```

## Branching

Use feature branches for changes and open pull requests into `main`.

Recommended branch names:

```text
feature/<short-topic>
fix/<short-topic>
docs/<short-topic>
```

Before opening a PR:

```powershell
dotnet test FluxMq.sln
npm run build --prefix docs-site
```

## Desktop App

`FluxMq.UI` is a Windows-first MAUI Blazor Hybrid app for the first alpha.

Build it directly with:

```powershell
dotnet build src\FluxMq.UI\FluxMq.UI.csproj
```

Run it from Visual Studio or with:

```powershell
dotnet run --project src\FluxMq.UI\FluxMq.UI.csproj -f net10.0-windows10.0.19041.0
```

The alpha workspace assumes a local MQTT broker is available at `localhost:1883` unless the user edits the broker profile in the app.

## Windows Packaging

The `Windows Desktop Packages` workflow builds the MAUI desktop app on a Windows runner and uploads two artifacts:

- a portable `win-x64` zip containing the published `FluxMq.UI.exe` folder
- an MSI installer built from the same publish output with WiX

The workflow is backed by `eng/package-windows.ps1` and `installer/FluxMq.UI/Product.wxs`.

Run the packaging script locally from PowerShell:

```powershell
dotnet tool install --global wix --version 6.0.2
.\eng\package-windows.ps1 -Configuration Release -Version 0.1.0
```

The portable package is built with `WindowsPackageType=None`, `RuntimeIdentifierOverride=win-x64`, and `WindowsAppSDKSelfContained=true`.

## Documentation Locations

- Use `docs/` for durable project documentation.
- Use `memory/` for working decisions, progress, and planning continuity.
