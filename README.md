# FluxMQ

<p align="center">
  <img src="design/ui-mockups/01-main-workspace.png" alt="FluxMQ workspace" width="100%">
</p>

FluxMQ is a next-generation MQTT debugging and observability platform built as a cross-platform desktop application.

The long-term goal is to go beyond a passive MQTT client and provide a focused tool for:

- Exploring MQTT topic trees.
- Inspecting and decoding payloads.
- Recording and replaying message sessions.
- Observing broker and topic activity in real time.
- Extending the app through stable modules and, later, plugins.

## Status

FluxMQ is in early foundation work.

Current state:

- .NET MAUI Blazor Hybrid app scaffold.
- Windows desktop target for the first development phase.
- Core project structure in place.
- MQTTnet selected for MQTT integration.
- LiteDB selected for local-first storage.
- MudBlazor wired into the app shell.
- Initial test projects created for core, pipeline, and storage layers.
- Project memory and planning files tracked in `memory/`.

## Architecture Direction

FluxMQ will be built around the message/session flow first:

```text
MQTTnet Client
  -> MqttSession
  -> Channel<MqttEnvelope>
  -> Message Pipeline
  -> Storage / Metrics / Topic Index
  -> Blazor UI State
```

Formal external plugins are planned later. The first implementation will use internal modules for payload inspection, observability, and replay so the extension contracts can mature naturally.

## Visual Direction

FluxMQ is aiming for an IDE-like desktop workspace: dense, operational, and optimized for repeated debugging work rather than a marketing-style interface.

### Intro Animation

![FluxMQ intro animation](design/intro-animation/out/fluxmq-intro.gif)

### Mockups

![FluxMQ payload debugger mockup](design/ui-mockups/02-payload-debugger.png)

Observability and replay timeline:

![FluxMQ observability replay mockup](design/ui-mockups/03-observability-replay.png)

## Repository Layout

```text
/src
  /FluxMq.App
  /FluxMq.Core
  /FluxMq.Pipeline
  /FluxMq.Replay
  /FluxMq.Storage
  /FluxMq.UI
/tests
  /FluxMq.Core.Tests
  /FluxMq.Pipeline.Tests
  /FluxMq.Replay.Tests
  /FluxMq.Storage.Tests
/docs
/memory
```

## Requirements

- Windows for the current MAUI desktop target.
- .NET SDK compatible with the repo's `global.json`.
- .NET MAUI Windows workload.

The current development environment uses .NET 11 preview SDK to build .NET 10 projects.

## Build

```powershell
dotnet restore FluxMq.sln
dotnet build FluxMq.sln --no-restore
dotnet test FluxMq.sln --no-build
```

## Project Memory

Planning, decisions, roadmap, and progress are tracked in `memory/`.

Start here:

- [Memory Index](memory/00-index.md)
- [Decisions](memory/01-decisions.md)
- [Architecture Plan](memory/02-architecture-plan.md)
- [Roadmap](memory/03-roadmap.md)
- [Progress Log](memory/04-progress-log.md)

## Documentation

Developer and architecture documentation is tracked in `docs/`.

Start here:

- [Documentation Index](docs/README.md)
- [Architecture](docs/architecture.md)
- [Fork Flow](docs/fork-flow.md)
- [Flow Errors](docs/flow-errors.md)
- [Replay](docs/replay.md)

## License

License is not decided yet.
