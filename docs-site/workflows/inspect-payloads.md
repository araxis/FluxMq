# Inspect Payloads

Payload inspection helps make raw MQTT messages understandable.

## Typical Flow

```text
MQTT source
  -> Topic filter
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
