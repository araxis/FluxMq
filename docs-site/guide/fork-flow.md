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
MQTT connection resource
  -> MQTT trigger
  -> Topic filter
  -> Payload inspector
  -> UI projection
```

Record selected messages:

```text
MQTT connection resource
  -> MQTT trigger
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
MQTT connection resource
  -> MQTT trigger
  -> Condition router
  -> Matching branch / non-matching branch
```

Observe traffic:

```text
MQTT connection resource
  -> MQTT trigger
  -> Metrics sink
  -> UI projection
```

## Design Goal

The same flow application definition should be editable through configuration and through the drag-and-drop interface.

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
      "type": "mqtt.connection",
      "configuration": {
        "profile": {
          "name": "local-broker",
          "host": "localhost",
          "port": 1883
        }
      }
    }
  },
  "workflows": {
    "observeTraffic": {
      "trigger": {
        "type": "mqtt.trigger",
        "configuration": {
          "connection": "broker",
          "subscriptions": [
            "factory/#",
            { "topicFilter": "telemetry/#", "qos": 1 }
          ]
        }
      },
      "metrics": {
        "type": "mqtt.metrics-sink",
        "Input": "trigger.Output"
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

- `mqtt.connection`: shared resource that owns an MQTT session and publishes `FlowError` on `Errors`.
- `mqtt.trigger`: workflow node that references a connection resource, subscribes to topic filters, emits `MqttEnvelope` on `Output`, and publishes `FlowError` on `Errors`.
- `mqtt.payload-inspector`: `Input` receives `MqttEnvelope`, `Output` publishes `InspectedMqttMessage`, and `Errors` publishes `FlowError`.
- `mqtt.metrics-sink`: `Input` receives `MqttEnvelope`, `Snapshots` publishes `MqttMetricsSnapshot`, and `Errors` publishes `FlowError`.

Connection configuration supports:

- `profile` object (`name` required; host/port/clientId/useTls/username/password/keepAliveSeconds/cleanStart optional)

Trigger configuration supports:

- `connection` resource name
- `subscriptions` as string, array of strings, or array of objects
- `qos` per subscription as `0|1|2` or `AtMostOnce|AtLeastOnce|ExactlyOnce`
- optional `boundedCapacity`

The runtime package owns the definition model, graph builder, typed ports, lifecycle state, and error contracts. Concrete MQTT, replay, storage, and metrics components live in the component package and are registered by the application host.

The mapper and metrics nodes were chosen first because they have stable constructors and do not need external services or expression configuration.

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

Runtime factories can tell whether they are building a shared resource or a workflow node. Startup order is controlled with `phase` on each node definition; lower phases start first across both resources and workflow nodes. Workflow nodes are stopped and disposed before shared resources.

Reloading will be owned by the runtime layer. The UI can edit and save definitions, but the runtime is responsible for validating the next definition, keeping unaffected resources alive, patching workflow graphs, and reporting reload failures.

The first desktop alpha includes a Blazor.Diagrams canvas that projects the current definition into nodes and links, a JSON editor for the same definition, local file save/load, and run controls that call the same host boundary as the command-line host.

## Current Building Blocks

- MQTT connection: owns the shared broker session.
- MQTT trigger: subscribes through a connection and emits matching live messages.
- Replay source: emits messages from a stored recording.
- Topic filter: forwards only matching messages.
- Condition router: sends each message to a true or false branch.
- Payload inspector: converts raw payloads into readable inspection results.
- MQTT publish sink: publishes messages through an active session.
- Recording sink: stores messages for a recording session.
- Metrics sink: tracks counters and broadcasts metric snapshots.
