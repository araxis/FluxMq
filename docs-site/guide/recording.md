# Recording

Recording captures MQTT messages into local storage so a session can be inspected, loaded into the topic view, or replayed later.

## What A Recording Captures

- topic
- payload
- quality of service
- retain flag
- received timestamp
- session identity

## Typical Use

1. Connect to a broker.
2. Add a pipeline with `mqtt.trigger`.
3. Link the trigger to `mqtt.recorder`, optionally through `flow.filter` or `flow.when`.
4. Start the app.
5. Reproduce the system behavior you want to analyze.
6. Stop the app and load the stored session when needed.

## Notes

Recordings are local-first. This keeps debugging data available without requiring a hosted backend.

## Flow Shape

```text
mqtt.trigger
  -> optional flow.filter
  -> mqtt.recorder
```

The recorder stores each incoming message for the selected recording session. If storing one message fails, the failure is reported as a flow error and later messages can continue recording.
