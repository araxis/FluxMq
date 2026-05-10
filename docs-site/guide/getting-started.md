# Getting Started

FluxMQ is in early alpha work. The current repository includes the core MQTT, storage, flow component, workflow runtime, command-line host, and desktop workspace.

## What FluxMQ Helps With

- Connect to MQTT brokers.
- Inspect live topic traffic.
- Decode and compare payloads.
- Record message sessions for later analysis.
- Replay recorded traffic through controlled flows.

## Current Product Direction

FluxMQ is designed as a focused desktop workspace, closer to an IDE than a dashboard. The goal is to make repeated debugging work fast, visible, and reliable.

## Current Alpha Entry Points

### Desktop Workspace

The first desktop workspace is `FluxMq.UI`, a MAUI Blazor Hybrid app.

Run it on Windows with:

```sh
dotnet run --project src/FluxMq.UI/FluxMq.UI.csproj -f net10.0-windows10.0.19041.0
```

The default broker profile points to `localhost:1883`.

### Windows Packages

The repository workflow builds early Windows desktop packages:

- portable `win-x64` zip
- MSI installer

Both artifacts are produced from the same desktop app publish output.

### Command Line

The first command-line surface validates a flow application definition through the same host boundary planned for future app surfaces:

```sh
dotnet run --project src/FluxMq.Cli -- validate --config samples/flow-applications/metrics-only.json
```

Use `--output json` when validation results need to be consumed by a script or CI pipeline.

To exercise the same definition through the runtime host lifecycle:

```sh
dotnet run --project src/FluxMq.Cli -- run --config samples/flow-applications/metrics-only.json --duration-ms 1000
```

## Documentation Scope

This site explains user-facing workflows. Developer architecture, implementation notes, and design decisions live in the repository developer documentation.
