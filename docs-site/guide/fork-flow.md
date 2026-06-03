# Fork Flow

Fork Flow is the user-configurable pipeline system in FluxMQ.

Pipelines are built from sources, triggers, filters, mappers, routers, observers, and actors.

```text
source or trigger
  -> filter, router, mapper, validator, or assertion
  -> observer or actor
```

## Examples

Record and inspect messages:

```text
mqtt.trigger
  -> flow.filter
  -> mqtt.payload-inspector
```

Record selected messages:

```text
mqtt.trigger
  -> flow.filter
  -> mqtt.recorder
```

Replay selected traffic:

```text
replay.source
  -> flow.filter
  -> mqtt.publisher
```

React to connection state:

```text
mqtt.connection-state-trigger
  -> flow.when
  -> flow.logger
```

Branch live traffic:

```text
mqtt.trigger
  -> flow.when
  -> matching branch / non-matching branch
```

Observe traffic:

```text
mqtt.trigger
  -> mqtt.metrics
```

## Design Goal

The same app definition should be editable through configuration and through the drag-and-drop interface.

FluxMQ app files keep resources, workflows, dashboards, and tests together. The runtime builds executable resources and workflows from that file; dashboards and tests stay app-owned.

## Application Definition Shape

A FluxMQ app definition describes shared resources, workflows, dashboards, tests, nodes, and links.

```text
Flow application definition
  -> resources
  -> workflows
     -> nodes
        -> receiving port links
  -> dashboards
  -> tests
```

Each workflow is an object. Each node is a property inside that workflow object.

```json
{
  "workflows": {
    "observeTraffic": {
      "traffic": {
        "type": "mqtt.trigger",
        "configuration": {
          "connection": "local-broker",
          "subscriptions": [
            "factory/#",
            { "topicFilter": "telemetry/#", "qos": 1 }
          ]
        }
      },
      "metrics": {
        "type": "mqtt.metrics",
        "Input": "traffic.Output"
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

Links can also carry `when` conditions. Link conditions are evaluated by the FluxMQ host expression engine.

Validation catches broken node references, malformed links, empty ports, and duplicate links before the flow runs.

The runtime builder creates registered node types and links compatible typed ports from the definition. If a node type or port is missing, or two ports carry incompatible value types, the build returns errors instead of starting a partial flow.

Common runtime component ids include:

- `mqtt.connection`: shared resource that owns an MQTT broker client and publishes `FlowError` on `Errors`.
- `mqtt.trigger`: workflow node that references a connection resource, subscribes to topic filters, emits `MqttEnvelope` on `Output`, and publishes `FlowError` on `Errors`.
- `generated.source`: emits configured MQTT envelope samples.
- `replay.source`: emits messages from a stored session.
- `mqtt.payload-inspector`: `Input` receives `MqttEnvelope`, `Output` publishes `InspectedMqttMessage`, and `Errors` publishes `FlowError`.
- `mqtt.metrics`: `Input` receives `MqttEnvelope`, `Snapshots` publishes metric snapshots, and `Errors` publishes `FlowError`.
- `flow.filter`, `flow.when`, `flow.mapper`, and `flow.assert`: control, mapping, routing, and assertion components.
- `json.schema-validator`, `json.parse`, and `json.stringify`: JSON and validation components.
- `mqtt.publisher`, `mqtt.recorder`, `file.writer`, `http.request`, and `flow.logger`: actors and observers.

The host boundary can build and control a configured app from this section:

```json
{
  "FluxMq": {
    "FlowApplication": {
      "workflows": {
        "observe": {
            "metrics": {
              "type": "mqtt.metrics"
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

The desktop app includes a diagram canvas that projects the current workflow into nodes and links, a JSON view for the same app file, local file save/load, and run controls that call the same host boundary as the command-line host.

## Current Building Blocks

- MQTT connection: owns the shared broker session.
- MQTT trigger: subscribes through a connection and emits matching live messages.
- Generated source: emits configured MQTT messages without a broker.
- Replay source: emits messages from a stored recording.
- Flow filter: forwards only matching messages.
- Flow when: sends each message to a true or false branch.
- Payload inspector: converts raw payloads into readable inspection results.
- MQTT publisher: publishes messages through an app-level connection.
- MQTT recorder: stores messages for a recording session.
- MQTT metrics: tracks counters and broadcasts metric snapshots.
