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
  /FluxMq.Core      domain models, MQTT session, topic index, payload inspection
  /FluxMq.Pipeline  Dataflow pipeline and concrete flow components
  /FluxMq.Replay    recorded session replay orchestration
  /FluxMq.Storage   LiteDB persistence
  /FluxMq.UI        MAUI Blazor Hybrid desktop workspace
/tests
  /FluxMq.Core.Tests
  /FluxMq.Pipeline.Tests
  /FluxMq.Replay.Tests
  /FluxMq.Storage.Tests
  /FluxMq.UI.Tests
/docs
  contributor and Wiki-ready documentation
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

## Documentation Locations

- Use `docs/` for durable project documentation.
- Use `memory/` for working decisions, progress, and planning continuity.
