# Project Overview

FluxMQ is a workflow-runtime platform for MQTT debugging, observability, recording, and replay.

The product direction is to move beyond a passive MQTT client. FluxMQ should help developers and operators inspect topic activity, decode payloads, record real sessions, replay traffic, and build configurable message flows.

## Current Capabilities

- MQTT session model and connection manager.
- TPL Dataflow-based message pipeline foundation.
- Topic index and topic tree UI components.
- LiteDB persistence for profiles, sessions, and messages.
- Payload inspector for JSON, XML, Base64, text, binary, and empty payloads.
- Concrete Fork Flow components:
  - connection state trigger
  - traffic source
  - MQTT connection resource
  - MQTT trigger
  - topic filter
  - payload inspector mapper
  - replay source
- Flow error ports with stable numeric error codes.
- Metrics snapshots for flow observability.
- Initial Fork Flow application definition model and validation.
- MAUI Blazor Hybrid desktop alpha surface in `FluxMq.UI`.
- Desktop broker connection, publish, topic inspection, payload inspection, LiteDB-backed recording, definition file load/save, validation, run, and stop controls.

## Product Shape

FluxMQ is designed as an operational workspace:

- dense and inspectable
- built around live and recorded MQTT traffic
- friendly to high-throughput debugging
- extensible through internal components first, later through stable plugin contracts

## Observability Direction

FluxMQ will keep local observability useful inside the desktop app first.

OpenTelemetry support is planned later for exporting selected runtime metrics, traces, and diagnostics to external tooling. It should remain optional and should not replace the local metrics used by the UI.

## Current Development Phase

The project is still in foundation work. Core runtime pieces are being built before the full drag-and-drop Fork Flow editor. This keeps the runtime honest: the visual editor will represent real executable flow application definitions, not a separate UI-only model.

Fork Flow now has an initial application definition model with shared resources and named workflows. The first cold-start runtime builder can create registered nodes and link typed ports from that definition. The preferred alpha shape uses a logical `traffic.source` node so downstream components can consume live broker traffic, stored sessions, or generated data through the same `MqttEnvelope` port. The first desktop alpha can edit, save, load, validate, and run this JSON through the same host boundary. Runtime reload and richer graph patching are still planned work.
