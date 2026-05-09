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

The same flow definition should eventually be editable through configuration and through a drag-and-drop interface.

## Current Building Blocks

- MQTT message source: reads live messages from an active session.
- Replay source: emits messages from a stored recording.
- Topic filter: forwards only matching messages.
- Condition router: sends each message to a true or false branch.
- Payload inspector: converts raw payloads into readable inspection results.
- MQTT publish sink: publishes messages through an active session.
- Recording sink: stores messages for a recording session.
- Metrics sink: tracks counters and broadcasts metric snapshots.
