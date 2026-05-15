# Record And Replay

This workflow captures live MQTT traffic and replays it later.

## Steps

1. Connect to the broker.
2. Subscribe to the target topics.
3. Start recording.
4. Exercise the system under test.
5. Stop recording.
6. Create a replay flow.
7. Replay the session into a selected broker or analysis view.

## Replay Flow Shape

```text
Recorded session
  -> Traffic source or replay source
  -> Topic filter
  -> MQTT publish sink
```

Use a topic filter when the recording contains traffic that should not be replayed.

## Recording Flow Shape

```text
Traffic source
  -> Topic filter
  -> Recording sink
```

The recording sink writes MQTT messages into local storage for the active recording session.
