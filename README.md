# FluxMQ

<p align="center">
  <img src="design/ui-mockups/01-main-workspace.png" alt="FluxMQ workspace" width="100%">
</p>

FluxMQ is a desktop workspace for MQTT debugging, observability, and testing, built around a host-independent workflow runtime. It goes beyond a passive MQTT client: compose message-processing pipelines visually, watch live dashboards and metrics, script and run test scenarios, and explore topic trees and payloads — all from one dense, IDE-like app.

## Highlights

- **Visual pipeline builder** — a node-graph canvas of MQTT triggers/sources, filters, condition routers, dynamic mappers, payload inspectors, publishers, recorders, HTTP/file actors, timers, and correlation/join/merge/window steps; typed ports with drag-to-connect; each node edited in a focused dialog, with Monaco editors for expressions, JSON, and JSONata mapping.
- **Live dashboards** — KPI tiles, gauges, line/area/bar/donut charts, event tables, topic activity/tree, and QoS/payload breakdowns on an editable grid; light and dark themes.
- **Metrics** — a flat, one-class-per-metric framework (message/topic counts, event rate, payload bytes, retained, plus windowed variants) surfaced on dashboards and pipeline nodes.
- **Test scenario studio** — Setup / Stimulus / Observe / Assert / Cleanup phase lanes of steps (publish, trigger, expect, conditional/wait, and assertions including metric thresholds and JSON-schema validation), executed with a run report.
- **Interactive debugging** — topic explorer, payload inspector, and an MQTT publisher panel.
- **Headless CLI** — `FluxMq.Cli` validates and runs application JSON without a UI; broker-free generated-traffic samples are included.

## Status

FluxMQ is an actively developed .NET MAUI Blazor Hybrid desktop app (Windows) on top of a reusable workflow runtime.

- Workflow runtime via the `FluxFlow.Engine` package: typed ports, runtime building, phase-ordered lifecycle, mapping, and conditional links.
- MQTTnet for MQTT integration; LiteDB for local-first storage.
- Application definitions keep app-owned resources, workflows, dashboards, metrics, and tests together while projecting executable resources/workflows into the engine runtime.
- `FluxMq.App` loads a `FluxMq:FlowApplication` through .NET configuration; `FluxMq.Cli` validates and runs an application JSON file.
- MudBlazor UI with a shared light/dark design-token theme, a Z.Blazor.Diagrams canvas, and Monaco editors.
- Project memory and planning files tracked in `memory/`.

## Architecture Direction

FluxMQ will be built around the message/session flow first:

```text
MQTTnet Client
  -> MqttBrokerClient
  -> Channel<MqttEnvelope>
  -> FluxFlow.Engine Application Runtime
  -> Storage / Metrics / Topic Index
  -> UI Projection / Host Integration
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
  /FluxMq.Cli
  /FluxMq.Components
  /FluxMq.Core
  /FluxMq.Scenarios
  /FluxMq.UI
/tests
  /FluxMq.App.Tests
  /FluxMq.Cli.Tests
  /FluxMq.Components.Tests
  /FluxMq.Core.Tests
  /FluxMq.Scenarios.Tests
  /FluxMq.UI.Tests
/docs
/docs-site
/eng
/installer
/memory
```

## Requirements

- .NET SDK compatible with the repo's `global.json`.

The current development environment uses .NET 11 preview SDK to build .NET 10 projects.

## Build

```powershell
dotnet restore FluxMq.sln
dotnet build FluxMq.sln --no-restore
dotnet test FluxMq.sln --no-build
```

Validate the first sample flow application:

```powershell
dotnet run --project src\FluxMq.Cli -- validate --config samples\flow-applications\metrics-only.json
```

Use JSON output when the command is consumed by scripts or CI:

```powershell
dotnet run --project src\FluxMq.Cli -- validate --config samples\flow-applications\metrics-only.json --output json
```

Run a broker-free generated traffic flow application for a bounded smoke test:

```powershell
dotnet run --project src\FluxMq.Cli -- run --config samples\flow-applications\generated-traffic-inspect.json --duration-ms 1000
```

Run the local sample verification script before release-readiness checks:

```powershell
.\eng\verify-samples.ps1
```

## Windows Packages

The Windows validation workflow runs release-shaped Windows restore and tests on PRs and main. Manual workflow dispatch still builds:

- `FluxMQ-<version>-portable-win-x64.zip`
- `FluxMQ-<version>-win-x64.msi`

The reusable local packaging entry point is:

```powershell
dotnet tool install --global wix --version 6.0.2
.\eng\package-windows.ps1 -Configuration Release -Version 0.1.0
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

User-facing documentation is tracked in `docs-site/` and published as a static GitHub Pages site.

Start here:

- [Documentation Index](docs/README.md)
- [Documentation Strategy](docs/documentation-strategy.md)
- [Architecture](docs/architecture.md)
- [Fork Flow](docs/fork-flow.md)
- [Flow Components](docs/flow-components.md)
- [Flow Errors](docs/flow-errors.md)
- [Replay](docs/replay.md)
- [Release Readiness](docs/release-readiness.md)

## License

License is not decided yet.
