# Replay

Replay sends recorded MQTT messages back through a controlled flow.

## Timing

Replay preserves relative message timing by default. Speed controls can make a session slower or faster while keeping the original order.

## Replay To MQTT

A recorded session can be replayed back to a broker by linking the replay source to an MQTT publish sink.

```text
Recorded session
  -> Replay source
  -> Optional filters
  -> MQTT publish sink
  -> Broker
```

## Failure Handling

Publish failures are reported as flow errors. A failed publish should not stop the rest of the replay.
