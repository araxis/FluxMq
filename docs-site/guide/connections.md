# Connections

Connections define how a FluxMQ app talks to MQTT brokers.

## Connection Profile

A connection resource contains:

- name
- host
- port
- transport settings
- authentication settings
- default subscription preferences

## Connection State

Connections are app-level resources. Pipelines, dashboards, and tests refer to those resources by name instead of duplicating broker settings.

Pipeline nodes can also observe connection state with `mqtt.connection-state-trigger`.

## Recommended Workflow

1. Create a named broker profile.
2. Connect and verify broker state.
3. Use the live tools panel to publish a small message when checking a pipeline or dashboard.
4. Add `mqtt.trigger` nodes for workflow subscriptions.
5. Start recording only after the connection is stable.
