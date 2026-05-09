# Fork Flow

Fork Flow is the planned user-configurable pipeline system in FluxMQ.

Flows are built from sources, triggers, filters, mappers, routers, and sinks.

```text
source or trigger
  -> filter, router, or mapper
  -> sink or projection
```

## Examples

Record and inspect messages:

```text
MQTT message source
  -> Topic filter
  -> Payload inspector
  -> UI projection
```

Record selected messages:

```text
MQTT message source
  -> Topic filter
  -> Recording sink
```

Replay selected traffic:

```text
Replay source
  -> Topic filter
  -> MQTT publish sink
```

React to connection state:

```text
Connection state trigger
  -> State router
  -> Notification sink
```

Branch live traffic:

```text
MQTT message source
  -> Condition router
  -> Matching branch / non-matching branch
```

Observe traffic:

```text
MQTT message source
  -> Metrics sink
  -> UI projection
```

## Design Goal

The same flow application definition should eventually be editable through configuration and through a drag-and-drop interface.

FluxMQ now uses the .NET configuration system as the first loading path for flow application definitions. A JSON file can provide `FluxMq:FlowApplication`, and future hosts can layer command-line values, environment values, saved settings, or UI-produced definitions through the same configuration model.

## Application Definition Shape

A Fork Flow application definition describes shared resources, workflows, nodes, and links. It is the configuration package the future runtime will load regardless of whether FluxMQ is hosted by the desktop app, a console runner, or another tool process.

```text
Flow application definition
  -> resources
  -> workflows
     -> nodes
        -> receiving port links
```

Each workflow is an object. Each node is a property inside that workflow object.

```json
{
  "resources": {
    "broker": {
      "type": "mqtt.connection"
    }
  },
  "workflows": {
    "observeTraffic": {
      "source": {
        "type": "mqtt.message-source",
        "Connection": "broker.Output"
      },
      "metrics": {
        "type": "mqtt.metrics-sink",
        "Input": "source.Output"
      }
    }
  }
}
```

Links are declared on receiving ports. A port can accept one link, many links, or link objects with a condition.

```json
{
  "Input": [
    "source.Output",
    {
      "From": "replay.Output",
      "When": "topic.startsWith('factory/')"
    }
  ]
}
```

A component-level `When` can provide the default condition for links that do not specify one.

Validation catches broken node references, malformed links, empty ports, and duplicate links before the flow runs.

The first runtime builder can create registered node types and link compatible typed ports from the definition. If a node type or port is missing, or two ports carry incompatible value types, the build returns errors instead of starting a partial flow.

The first registered runtime component types are:

- `mqtt.payload-inspector`: `Input` receives `MqttEnvelope`, `Output` publishes `InspectedMqttMessage`, and `Errors` publishes `FlowError`.
- `mqtt.metrics-sink`: `Input` receives `MqttEnvelope`, `Snapshots` publishes `MqttMetricsSnapshot`, and `Errors` publishes `FlowError`.

These were chosen first because they have stable constructors and do not need external services or expression configuration.

The first host boundary can build and control a configured flow application from this section:

```json
{
  "FluxMq": {
    "FlowApplication": {
      "workflows": {
        "observe": {
          "metrics": {
            "type": "mqtt.metrics-sink"
          }
        }
      }
    }
  }
}
```

The first CLI command validates this shape from a JSON file:

```sh
dotnet run --project src/FluxMq.Cli -- validate --config samples/flow-applications/metrics-only.json
```

For automation, add `--output json` to receive structured validation results on standard output.

The same file can be started through the command-line host lifecycle:

```sh
dotnet run --project src/FluxMq.Cli -- run --config samples/flow-applications/metrics-only.json --duration-ms 1000
```

Reloading will be owned by the runtime layer. The UI can edit and save definitions, but the runtime is responsible for validating the next definition, keeping unaffected resources alive, patching workflow graphs, and reporting reload failures.

## Current Building Blocks

- MQTT message source: reads live messages from an active session.
- Replay source: emits messages from a stored recording.
- Topic filter: forwards only matching messages.
- Condition router: sends each message to a true or false branch.
- Payload inspector: converts raw payloads into readable inspection results.
- MQTT publish sink: publishes messages through an active session.
- Recording sink: stores messages for a recording session.
- Metrics sink: tracks counters and broadcasts metric snapshots.
