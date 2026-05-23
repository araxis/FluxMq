# Dynamic Mapping And Ops Vision

## Product Direction

FluxMQ is not a toy MQTT visualizer. The runtime should become a serious flow platform for two related audiences:

- Developers building ELT-style integrations and protocol bridges.
- Ops and QA teams testing, asserting, measuring, and diagnosing message-based systems.

MQTT is the first protocol, not the final boundary. The same model should eventually support AMQP, HTTP, Bluetooth, file IO, email, and composed multi-protocol workflows.

## Developer Era: ELT And Integration Flows

The developer experience should center on typed messages, dynamic mapping, and explicit actor commands.

Example:

```text
mqtt trigger
  -> filter: envelope.QualityOfService >= 1
  -> dynamic mapper: MqttEnvelope -> MqttPublishRequest
  -> mqtt publisher: publish to broker2
```

The publisher component must not be a vague "sink" that guesses intent from an `MqttEnvelope`. It should be an actor that expects an explicit command:

```json
{
  "Topic": "target/topic",
  "Qos": 1,
  "Payload": {
    "value": 42
  },
  "Retain": false
}
```

The mapper is the important programmable part. Users should be able to map:

- `MqttEnvelope -> MqttPublishRequest`
- `MqttEnvelope -> FileWriteRequest`
- `MqttEnvelope -> EmailSendRequest`
- `MqttEnvelope -> HttpRequest`
- protocol message -> another protocol command

## Dynamic Mapper Engines

FluxMQ should support more than fixed built-in mappers.

Candidate engines:

- Dynamic Expresso for C#-style predicates and lightweight expressions. First runtime slice implemented with `DynamicExpresso.Core 2.19.3`.
- Jsonata or equivalent for JSON payload query, mapping, and transformation.
- Later, UI-assisted mapping for ops users who should not need to write code.

Filter/router examples:

- `envelope.QualityOfService >= 1`
- `topic.StartsWith("factory/")`
- JSON payload fields matched through Jsonata or schema-aware helpers.

Mapper examples:

- Copy input topic to output topic.
- Rewrite `factory/a` to `factory/a/commands`.
- Project a JSON payload into another JSON shape.
- Wrap payload and metadata into an HTTP or file command.

## Ops And QA Era: Assertions, Metrics, Testing

After the developer ELT foundation is strong, FluxMQ should grow an ops/testing layer.

Examples:

- "When I publish MQTT message topic `a` with payload `x`, I expect a response on broker B with topic `a/resp`."
- "The response payload must be valid against this JSON Schema."
- "Measure messages per second for this topic."
- "Count fault messages matching this predicate."
- "Create a counter, summary, or metric dynamically from Jsonata, C# expression, or UI helper."

This is a different product layer from raw ELT, but it should reuse the same runtime primitives:

- Sources produce events/messages.
- Dynamic filters and routers select data.
- Dynamic mappers create commands or metric observations.
- Actor components perform side effects.
- Assertion components observe streams and produce pass/fail results.
- Metric components observe streams and produce counters, summaries, rates, and fault counts.

## Naming Direction

Avoid calling active side-effect components "sink" in user-facing language when a clearer actor name exists.

Preferred language:

- MQTT Publisher, not Publish Sink.
- File Writer, not File Sink.
- Recorder or Recording Writer, not Recording Sink.
- Mapper, Filter, Router, Validator, Assertion, Metric.

Internal code should keep moving toward actor names and observer names now that compatibility aliases are not required.

## Implemented Runtime Proof Points

- `mqtt.publish-request` dynamically maps `MqttEnvelope` into `MqttPublishRequest`.
- `mqtt.publisher` consumes `MqttPublishRequest` and publishes through the configured broker.
- `file.write-request` dynamically maps `MqttEnvelope` into `FileWriteRequest`.
- `file.writer` consumes `FileWriteRequest` and writes/creates/appends files based only on the input command.
- `mqtt.metrics` observes an input stream and emits snapshots without caring whether the stream came from live MQTT, replay, stored sessions, or generated data.
