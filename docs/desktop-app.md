# Desktop App

`FluxMq.UI` is the first desktop workspace for FluxMQ.

It is a Windows-first MAUI Blazor Hybrid app. The Blazor surface uses MudBlazor for the app shell and Blazor.Diagrams for the first Fork Flow canvas, while the host process keeps native access to local files, LiteDB, and MQTT TCP connections.

```mermaid
flowchart LR
    UI["FluxMq.UI"] --> Broker["Local MQTT Broker"]
    UI --> Files["Flow JSON Files"]
    UI --> Host["FluxMq.App"]
    Host --> Runtime["Flow Application Runtime"]
    Runtime --> Components["Registered Flow Components"]
```

## Current Alpha Surface

- broker profile editor
- connection test, connect, disconnect, subscribe, and publish actions
- live topic tree
- recent MQTT message list
- payload inspector
- LiteDB-backed live traffic recording controls
- component catalog for registered runtime nodes
- visual diagram projection from the flow application definition with collapsible flow nodes
- JSON definition editor
- definition save and load from local files
- validate, run, and stop controls through `FluxMq.App`
- named sessions grouped by project

## Broker Assumption

The default profile points to:

```text
localhost:1883
```

This matches a default local Mosquitto service. The app should not require editing the broker configuration for the alpha path.

## Runtime Boundary

The desktop app does not own flow graph mechanics. It sends flow application definitions to `FluxMq.App`, which loads them through the .NET configuration model and builds the runtime through registered factories.

This keeps the same definition usable from the desktop app and the command-line host.

## Definition Composer

`FlowDefinitionComposer` is a UI-side adapter, not a runtime factory. Its job is to turn desktop actions such as "use this broker profile" or "add this component" into valid flow application JSON.

The runtime still builds components through `FluxMq.App` and the registered runtime factories. The composer only keeps the editor, diagram, and saved JSON synchronized around the current alpha definition shape.
