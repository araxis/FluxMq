# Replay

Replay is the foundation for time-travel debugging in FluxMQ.

## Current Runtime Component

`replay.source` uses the same session-store-backed runtime path as `session.source`, with timing preservation enabled.

It reads stored session records, converts them to `MqttEnvelope` values, and emits them in stored sequence order.

## Behavior

- streams messages from the session store
- preserves relative timing when replaying
- supports speed multiplier
- exposes Dataflow lifecycle behavior
- publishes failures through `Errors`
- uses injectable delay behavior for deterministic tests

```mermaid
flowchart LR
    Store["Session store"] --> Convert["SessionRecord to MqttEnvelope"]
    Convert --> Replay["replay.source"]
    Replay --> Output["Output: MqttEnvelope"]
    Replay --> Errors["Errors: FlowError"]
```

## Timing

Given messages:

```text
10:00:00 message A
10:00:02 message B
10:00:03 message C
```

At `speed = 1`:

```text
A immediately
wait 2 seconds
B
wait 1 second
C
```

At `speed = 2`:

```text
A immediately
wait 1 second
B
wait 0.5 seconds
C
```

```mermaid
sequenceDiagram
    participant Replay as ReplaySource
    participant Out as Output Port
    Replay->>Out: message A
    Note over Replay: wait relative delay / speed
    Replay->>Out: message B
    Note over Replay: wait relative delay / speed
    Replay->>Out: message C
```

## Storage Integration

FluxMQ adapts the shared session contracts to the local message repository:

```text
FluxMqSessionStore.ReadMessagesAsync(sessionId)
  -> SessionRecord
  -> MqttEnvelope
  -> replay.source Output
```

For source-agnostic workflow execution, stored sessions can also enter the graph directly through `session.source`. Downstream nodes link to that source node's `Output` port.

```mermaid
flowchart LR
    Repository["IMessageRepository"] --> Store["FluxMqSessionStore"]
    Store --> Source["session.source or replay.source"]
```

## Replay To MQTT

Recorded sessions can be replayed back through an MQTT client by mapping each replayed envelope into a `MqttPublishRequest`, then linking the request stream to `MqttPublisherComponent`.

```mermaid
flowchart LR
    Repository["IMessageRepository"] --> Store["FluxMqSessionStore"]
    Store --> Replay["replay.source"]
    Replay --> Mapper["flow.mapper: MqttPublishRequest"]
    Mapper --> Publish["MqttPublisherComponent"]
    Publish --> Broker["MQTT broker"]
    Replay --> Errors["Errors"]
    Mapper --> Errors
    Publish --> Errors
```

The replay source controls timing. The explicit mapper owns the envelope-to-request transformation. The publisher owns broker publishing and converts publish exceptions into `FlowError` values, so one failed publish does not stop the rest of the replay.

## Next Replay Steps

Likely next components:

- replay UI controls
- speed control UI
- pause/resume support
