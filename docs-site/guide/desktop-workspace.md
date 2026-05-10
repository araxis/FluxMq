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
- Fork Flow: component catalog, diagram canvas, and JSON definition editor.
- Traffic: publish, LiteDB recording, topic tree, recent messages, and payload inspector.

## Files

The Runtime panel saves and loads the current flow application JSON from the file path shown in the panel.

## Flow Execution

The Run button starts the current definition through the same application host used by the command-line tools. Validation and runtime build errors are shown in the Runtime panel.
