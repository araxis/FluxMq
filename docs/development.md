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
  /FluxMq.Studio    runnable alpha UI host for flow definition editing and runtime control
  /FluxMq.Core      domain models, MQTT session, topic index, payload inspection
  /FluxMq.Pipeline  Dataflow pipeline and concrete flow components
  /FluxMq.Replay    recorded session replay orchestration
  /FluxMq.Storage   LiteDB persistence
  /FluxMq.UI        reusable Blazor UI components
/tests
  /FluxMq.Core.Tests
  /FluxMq.Pipeline.Tests
  /FluxMq.Replay.Tests
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
npm run build --prefix docs-site
```

## Documentation Locations

- Use `docs/` for durable project documentation.
- Use `memory/` for working decisions, progress, and planning continuity.

## Alpha UI Host

Run the alpha workspace UI:

```powershell
dotnet run --project src/FluxMq.Studio
```

The workspace currently supports:

- editing a flow application JSON definition
- validating through `FlowApplicationHost`
- starting and stopping the runtime
- reviewing host, definition, and runtime diagnostics
