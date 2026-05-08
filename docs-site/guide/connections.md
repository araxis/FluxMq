# Connections

Connections define how FluxMQ talks to an MQTT broker.

## Connection Profile

A connection profile should contain:

- name
- host
- port
- transport settings
- authentication settings
- default subscription preferences

## Connection State

FluxMQ treats connection state as flow input. A Fork Flow can react when a session connects, disconnects, or changes state.

## Recommended Workflow

1. Create a named broker profile.
2. Connect and verify session state.
3. Subscribe to the topic area you want to inspect.
4. Start recording only after the connection is stable.
