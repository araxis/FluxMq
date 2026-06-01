# Project Overview

FluxMQ is a workflow-runtime platform for MQTT debugging, observability, recording, and replay.

The product direction is to move beyond a passive MQTT client. FluxMQ should help developers and operators inspect topic activity, decode payloads, record real sessions, replay traffic, and build configurable message flows.

FluxMQ is also a dynamic ELT app. Sources extract message streams, mapper nodes transform typed data into explicit actor requests, and actors load the result into brokers, files, recorders, HTTP endpoints, or future targets.

## Current Capabilities

- MQTT client model and connection manager.
- Package-backed workflow runtime with typed ports, lifecycle, mapping, and conditional links.
- Topic index and topic tree UI components.
- LiteDB persistence for profiles, sessions, and messages.
- Payload inspector for JSON, XML, Base64, text, binary, and empty payloads.
- Concrete Fork Flow components:
  - connection state trigger
  - traffic source
  - MQTT connection resource
  - MQTT trigger
  - topic filter
  - dynamic mapper
  - payload inspector mapper
  - MQTT publisher actor
  - package-backed file writer actor
  - replay source
- Flow error ports with stable numeric error codes.
- Metrics snapshots for flow observability.
- FluxMQ application definition model with engine-owned resources/workflows plus app-owned dashboards/tests.
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

The project is still in foundation work. Runtime behavior now lives behind the package engine before the full drag-and-drop Fork Flow editor is finished. This keeps the runtime honest: the visual editor will represent real executable flow application definitions, not a separate UI-only model.

Fork Flow now has an application definition model with shared resources and named workflows, plus FluxMQ-owned dashboards and tests in the app document. The package runtime builder can create registered nodes, link typed ports, and evaluate per-link conditions from that definition. The preferred alpha shape uses `mqtt.connection` plus `mqtt.trigger` for live traffic, and explicit sources such as `session.source`, `replay.source`, and `generated.source` for stored, replayed, or configured message traffic. Actor nodes consume explicit request models, so users add `flow.mapper` nodes when a source output must become a request such as `MqttPublishRequest` or `FileWriteRequest`. The first desktop alpha can edit, save, load, validate, and run this JSON through the same host boundary. Runtime reload and richer graph patching are still planned work.
