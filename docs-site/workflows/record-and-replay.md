# Record And Replay

This workflow captures live MQTT traffic and replays it later.

## Steps

1. Connect to the broker.
2. Add `mqtt.trigger` for the target topics.
3. Link it to `mqtt.recorder`.
4. Run the app and exercise the system under test.
5. Stop the app.
6. Load the stored session into the Topics page for analysis, or create a replay pipeline.
7. Replay the session into a selected broker.

## Replay Flow Shape

```text
Recorded session
  -> replay.source
  -> flow.filter
  -> mqtt.publisher
```

Use a topic filter when the recording contains traffic that should not be replayed.

## Recording Flow Shape

```text
mqtt.trigger
  -> flow.filter
  -> mqtt.recorder
```

The recorder writes MQTT messages into local storage for the active recording session.
