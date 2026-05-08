# Fork Flow

Fork Flow is the planned user-configurable pipeline system in FluxMQ.

Flows are built from sources, triggers, filters, mappers, routers, and sinks.

```text
source or trigger
  -> filter or mapper
  -> sink or projection
```

## Examples

Record and inspect messages:

```text
MQTT source
  -> Topic filter
  -> Payload inspector
  -> UI projection
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

## Design Goal

The same flow definition should eventually be editable through configuration and through a drag-and-drop interface.
