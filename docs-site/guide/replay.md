# Replay

Replay sends recorded MQTT messages through a controlled workflow.

## Timing

Replay preserves relative message timing by default. Speed controls can make a session slower or faster while keeping the original order.

## Replay To MQTT

A recorded session can be replayed back to a broker by linking `replay.source` to `mqtt.publisher`.

For analysis, a stored session can also be selected in the desktop workspace so the topic tree, message table, and payload inspector show stored traffic instead of live traffic.

```text
Recorded session
  -> Replay source
  -> Optional filters
  -> MQTT publisher
  -> Broker
```

## Failure Handling

Publish failures are reported as flow errors. A failed publish should not stop the rest of the replay.
