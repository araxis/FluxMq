# Desktop Workspace

The desktop workspace is the fastest path for local MQTT debugging and workflow validation.

## Run

```sh
dotnet run --project src/FluxMq.UI/FluxMq.UI.csproj -f net10.0-windows10.0.19041.0
```

## App Files

FluxMQ saves one app definition file. That file owns:

- resources, including MQTT broker connections
- pipelines
- dashboards
- tests

The active file path is shown in the bottom status bar.

## Default Broker

The default connection uses:

```text
localhost:1883
```

Use the Connections menu to add or edit broker resources. The live tools panel can then publish through any connected broker resource.

## Workspace Areas

- Top bar: open/save, validate, run, stop, app runtime state, broker connection state, and panel toggles.
- Connections menu: create, edit, connect, disconnect, and delete app-level broker resources.
- Pipelines: component catalog, search, diagram canvas, link editing, JSON view, and node activity.
- Dashboards: grid designer, widget catalog, live mode, event counters, latest-event cards, and event-rate widgets.
- Tests: scenario step catalog, runner actions, result history, and report export.
- Logs: scoped entries with search and level filters.
- Topics: topic tree, message table, and payload inspector.
- Live tools: inspect, publish, and live topic activity for pipeline and dashboard checks.

The left session panel and right live tools panel can be opened or closed from the shell controls. The live tools panel is available on pipeline and dashboard surfaces; tests, logs, topics, and no-app states keep the full workspace width.

Runtime and traffic updates refresh diagram node activity without rebuilding the diagram, so node positions and collapsed state remain stable while messages arrive.

## Sessions And Projects

Use the Sessions panel to name a recording session and assign it to a project. Stored sessions are listed by project, and selecting one loads its recorded messages into the topic and payload views.

The topic tree and message table switch together. Live traffic is shown by default; selecting a stored session changes both views to the selected session. Selecting a topic branch filters the table to that branch and its child topics.

## Flow Execution

The Run button starts the current app through the same host boundary used by the command-line tools. Validation, build, runtime, and scenario entries are visible in Logs.
