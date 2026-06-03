# Observe Traffic

Traffic observation turns runtime MQTT events into operational counters, latest-event cards, and rates.

## Typical Flow

```text
mqtt.trigger
  -> optional flow.filter
  -> mqtt.metrics
```

## Dashboard Widgets

Dashboard widgets observe runtime events. Current widgets include:

- event counter
- latest event
- event rate

They can filter MQTT message events by topic prefix, excluded topic prefix, QoS, retain flag, status, and payload text.

## Metrics Component

The `mqtt.metrics` component tracks stream-level metrics for messages that reach its input:

- total messages
- current and average rate
- payload bytes
- average payload size
- retained message count
- unique topic count
- last observed topic

Metrics are a projection of traffic. They do not store messages and do not change the messages flowing through other branches.

Local dashboards and metrics work without an external collector.
