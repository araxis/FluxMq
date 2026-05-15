# Inspect Payloads

Payload inspection helps make raw MQTT messages understandable.

## Typical Flow

```text
Traffic source
  -> Topic filter or condition router
  -> Payload inspector
  -> UI projection
```

## Inspection Goals

- identify payload format
- render readable values
- compare messages over time
- highlight malformed or unexpected payloads

## Notes

Payload inspection should not mutate the original MQTT message. Transformations belong in explicit mapper components.

The traffic source is responsible for binding the workflow to live or stored messages. Filtering, inspection, and projection stay in separate components so each step remains visible in the flow.

Use a topic filter when non-matching messages should be dropped. Use a condition router when non-matching messages should continue through another branch.
