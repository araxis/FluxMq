# Observe Traffic

Traffic observation turns live MQTT messages into operational counters.

## Typical Flow

```text
Traffic source
  -> Optional topic filter
  -> Metrics sink
  -> UI projection
```

## Metrics

The metrics sink tracks:

- total message count
- total payload bytes
- minimum payload size
- maximum payload size
- average payload size
- retained message count
- unique topic count
- last observed topic

## Notes

Metrics are a projection of traffic. They do not store messages and do not change the messages flowing through other branches. The same metrics path can run from live traffic or a selected stored session.

OpenTelemetry export is planned for a later version. Local metrics will continue to work without an external collector.
