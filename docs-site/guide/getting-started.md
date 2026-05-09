# Getting Started

FluxMQ is in early foundation work. The current repository focuses on the core MQTT, storage, flow component, workflow runtime, and host boundary libraries.

## What FluxMQ Helps With

- Connect to MQTT brokers.
- Inspect live topic traffic.
- Decode and compare payloads.
- Record message sessions for later analysis.
- Replay recorded traffic through controlled flows.

## Current Product Direction

FluxMQ is designed as a focused desktop workspace, closer to an IDE than a dashboard. The goal is to make repeated debugging work fast, visible, and reliable.

## Current Alpha Entry Points

The first runnable UI workspace:

```sh
dotnet run --project src/FluxMq.Studio
```

The Studio workspace currently supports:

- editing a flow application definition JSON
- loading a starter sample
- validating the definition
- starting and stopping the runtime
- reviewing diagnostics from host, definition, and runtime layers

The command-line surface validates the same definitions through the same host boundary:

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
