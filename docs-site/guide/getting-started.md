# Getting Started

FluxMQ is still pre-release, but the desktop app is now the main product surface. A FluxMQ app file contains app-level resources, pipelines, dashboards, and tests.

## Start With The Sample App

The fastest way to understand the current workspace is to open the operations monitor sample:

- [Download operations-monitor.json](/samples/operations-monitor.json)
- [Read the sample app walkthrough](sample-app.md)

The sample contains broker resources, two pipelines, one dashboard, one test scenario, and saved designer positions.

![Operations monitor workspace](/screenshots/sample-workspace.png)

## What FluxMQ Helps With

- Connect to MQTT brokers.
- Inspect live topic traffic.
- Decode and compare payloads.
- Record message sessions for later analysis.
- Replay recorded traffic through controlled flows.
- Build live dashboards from runtime events.
- Run integration-style test scenarios against the app.

## Current Product Direction

FluxMQ is designed as a focused desktop workspace, closer to an IDE than a dashboard. The goal is to make repeated debugging work fast, visible, and reliable.

## Current Alpha Entry Points

### Desktop Workspace

The desktop workspace is `FluxMq.UI`.

Run it on Windows with:

```sh
dotnet run --project src/FluxMq.UI/FluxMq.UI.csproj -f net10.0-windows10.0.19041.0
```

Use the Open file action to load `docs-site/public/samples/operations-monitor.json`.

The default broker profile points to `localhost:1883`. Broker resources belong to the app, not to one pipeline, dashboard, or test.

### Command Line

The command-line surface can validate and run app definitions:

```sh
dotnet run --project src/FluxMq.Cli -- validate --config samples/flow-applications/metrics-only.json
```

Use `--output json` when validation results need to be consumed by a script or CI pipeline.

To exercise the same definition through the runtime host lifecycle:

```sh
dotnet run --project src/FluxMq.Cli -- run --config samples/flow-applications/metrics-only.json --duration-ms 1000
```

For a broker-free smoke check, use the generated-traffic sample:

```sh
dotnet run --project src/FluxMq.Cli -- run --config samples/flow-applications/generated-traffic-inspect.json --duration-ms 1000
```

## Current Workspace Shape

- Connections: app-owned MQTT broker resources.
- Pipelines: runnable workflow graphs.
- Dashboards: visual runtime-event projections.
- Tests: scenario steps and expectations.
- Logs: scoped app, runtime, and test-runner entries.
- Topics: first-class topic tree and message inspection view.

## Documentation Scope

This site explains user-facing workflows. Developer architecture, implementation notes, and design decisions live in the repository developer documentation.
