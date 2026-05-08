# Local Development

## Requirements

- Windows for the current desktop target.
- .NET SDK compatible with `global.json`.
- .NET MAUI Windows workload.

The current solution targets .NET 10 projects and is built with a preview SDK in the active development environment.

## Restore, Build, Test

```powershell
dotnet restore FluxMq.sln
dotnet build FluxMq.sln --no-restore
dotnet test FluxMq.sln --no-build
```

For a direct app build:

```powershell
dotnet build src\FluxMq.App\FluxMq.App.csproj
```

## Repository Layout

```text
/src
  /FluxMq.App       MAUI Blazor Hybrid shell
  /FluxMq.Core      domain models, MQTT session, topic index, payload inspection
  /FluxMq.Pipeline  Dataflow pipeline and concrete flow components
  /FluxMq.Storage   LiteDB persistence
  /FluxMq.UI        reusable Blazor UI components
/tests
  /FluxMq.Core.Tests
  /FluxMq.Pipeline.Tests
  /FluxMq.Storage.Tests
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
dotnet build src\FluxMq.App\FluxMq.App.csproj
```

## Documentation Locations

- Use `docs/` for durable project documentation.
- Use `memory/` for working decisions, progress, and planning continuity.
