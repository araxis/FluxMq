# Inspect Payloads

Payload inspection helps make raw MQTT messages understandable.

## Typical Flow

```text
mqtt.trigger
  -> flow.filter or flow.when
  -> mqtt.payload-inspector
```

## Inspection Goals

- identify payload format
- render readable values
- compare messages over time
- highlight malformed or unexpected payloads

## Notes

Payload inspection should not mutate the original MQTT message. Transformations belong in explicit mapper components.

The trigger is responsible for subscribing to live broker topics. Filtering, routing, inspection, and output handling stay in separate components so each step remains visible in the flow.

Use `flow.filter` when non-matching messages should be dropped. Use `flow.when` when non-matching messages should continue through another branch.
