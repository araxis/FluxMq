# Recording

Recording captures MQTT messages into local storage so a session can be inspected or replayed later.

## What A Recording Captures

- topic
- payload
- quality of service
- retain flag
- received timestamp
- session identity

## Typical Use

1. Connect to a broker.
2. Select the topics to observe.
3. Start a recording session.
4. Reproduce the system behavior you want to analyze.
5. Stop recording and name the session.

## Notes

Recordings are local-first. This keeps debugging data available without requiring a hosted backend.

## Flow Shape

```text
Traffic source
  -> Optional topic filter
  -> Recording sink
```

The recording sink stores each incoming message for the selected recording session. If storing one message fails, the failure is reported as a flow error and later messages can continue recording.
