# Desktop Workspace

The desktop workspace is the fastest path for local MQTT debugging.

## Run

```sh
dotnet run --project src/FluxMq.UI/FluxMq.UI.csproj -f net10.0-windows10.0.19041.0
```

## Broker

The default profile uses:

```text
localhost:1883
```

Use the Broker panel to change host, port, client ID, credentials, TLS, and subscription filter.

## Workspace Areas

- Broker: connection profile, test, connect, disconnect.
- Runtime: load, save, validate, run, and stop flow definitions.
- Fork Flow: component catalog, diagram canvas, collapsible definition editor, grid, navigator, and live activity labels.
- Sessions: define recording sessions, group them by project, and load stored traffic.
- Traffic: publish, LiteDB recording, topic tree, recent messages, and payload inspector.

The left and right workspace columns can be collapsed from the center toolbar. On desktop, drag the slim splitters beside the designer to resize the side columns.

Runtime and traffic updates refresh diagram node activity without rebuilding the diagram, so node positions and collapsed state remain stable while messages arrive.

## Files

The Runtime panel saves and loads the current flow application JSON from the file path shown in the panel.

## Sessions And Projects

Use the Sessions panel to name a recording session and assign it to a project. Stored sessions are listed by project, and selecting one loads its recorded messages into the Traffic panel.

The topic tree and message table switch together. Live traffic is shown by default; selecting a stored session changes both views to the selected session. Selecting a topic branch filters the table to that branch and its child topics.

## Flow Execution

The Run button starts the current definition through the same application host used by the command-line tools. Validation and runtime build errors are shown in the Runtime panel.
