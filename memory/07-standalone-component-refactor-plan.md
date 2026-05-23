# Standalone Component Refactor Plan

## Goal

Flow components should be clear, standalone actors, mappers, filters, routers, validators, assertions, or metric observers. A component should do one obvious thing based on its typed input, not quietly infer behavior from ambient shared resources or UI state.

## Design Rules

- Sources produce domain events or messages.
- Mappers transform one typed input into another typed output.
- Filters and routers decide which messages continue and on which branch.
- Actor components consume explicit command/request objects and perform side effects.
- Shared resources may exist at the host boundary, but ordinary component semantics should be visible in ports and input types.
- Metrics and projections observe the input stream. They must not care whether data came from a connection, subscription, replay, stored session, import, or generated source.

## Component Shape

### Sources

Clear source components:

- `mqtt.live-source`: live broker traffic.
- `session.source`: stored session stream.
- `replay.source`: timed replay stream.
- `generated.source`: deterministic test/demo traffic.

The generic source alias has been removed; use explicit source names in new definitions.

### Mappers

Mapper nodes are a core product feature, not glue. They prepare intent for actor nodes and should become dynamically configurable:

- `mqtt.payload-inspector`: `MqttEnvelope -> InspectedMqttMessage`.
- `flow.mapper`: user-facing dynamic mapper from `MqttEnvelope` to a configured output request type.
- Current mapper output targets: `MqttPublishRequest`, `MqttRecordingRequest`, and `FileWriteRequest`.
- Future output targets: `HttpRequest`, `EmailSendRequest`, `database.write-request`, `json.schema-validation-result`.

Request models are actor input contracts, not user-facing components. A graph should show a dynamic mapper node between a source/filter/router and an actor whenever the port types differ.

Dynamic mapping engines:

- Dynamic Expresso for C#-style expressions and object construction.
- Jsonata for JSON payload query, mapping, and transformation.
- Later, UI-assisted mappers for ops users.

Example developer flow:

```text
mqtt trigger -> filter(qos >= 1) -> dynamic mapper -> mqtt publisher on broker2
```

### Filters And Routers

Filters and routers should use expression engines instead of hard-coded placeholder predicates:

- Use Dynamic Expresso for C#-like scalar predicates over envelopes and inspected messages.
- Evaluate Jsonata as a candidate for JSON payload querying/transformation.
- Add JSON Schema validation as its own component, not as hidden filter behavior.

### Actor Components

Actor components should accept explicit commands:

- MQTT Publisher: `MqttPublishRequest -> publish`.
- Recorder / Recording Writer: `MqttRecordingRequest -> repository write`.
- File Writer: `FileWriteRequest -> file write`.
- HTTP Sender: `HttpRequest -> HTTP call`.
- Email Sender: `EmailSendRequest -> email`.

This makes flow intent visible: `source -> filter -> dynamic mapper -> actor`.

Avoid user-facing "sink" naming where an actor name is clearer. Internal code may keep old names during migration, but product language should move to Publisher, Writer, Recorder, Sender, etc.

## First Implemented Slice

- Added `MqttPublishRequest` and `MqttPublisherComponent`; publishing now consumes `MqttPublishRequest` instead of raw `MqttEnvelope`.
- Added `MqttRecordingRequest` and `MqttRecorderComponent`; recording now consumes `MqttRecordingRequest`, so `SessionId` lives on the input request.
- Added request-specific mapper components as internal/runtime adapters.
- Added runtime-level mapper/predicate/expression abstractions under `FluxMq.Pipeline.Mapping`.
- Added Dynamic Expresso as the first concrete expression engine in `FluxMq.Pipeline.Mapping`.
- Added Jsonata as the JSON-oriented mapper engine.
- Changed `mqtt.message-filter` expression configuration from a placeholder to real Dynamic Expresso evaluation.
- Added `flow.mapper` as the user-facing dynamic mapper node; request-specific mapper node types are compatibility aliases, not catalog components.
- Added `mqtt.publisher` as the MQTT publish actor node type.
- Renamed the metrics observer to `MqttMetricsComponent` / `mqtt.metrics`.
- Added `FileWriteRequest` and `file.writer` as the first non-MQTT proof of the mapper-to-actor pattern.
- Registered runtime node types and user-facing catalog entries:
  - `flow.mapper`
  - `mqtt.publisher`
  - `mqtt.recorder`
  - `mqtt.metrics`
  - `file.writer`

## Next Refactoring Steps

1. Generalize dynamic mapper targets beyond the current request adapters so the same node can produce `HttpRequest`, `EmailSendRequest`, and future command types.
2. Add richer File Writer configuration and UI editing for path/content/mode expressions.
3. Harden Jsonata mapping around binary payloads, JSON payload objects, and schema-aware helper variables.
4. Add JSON Schema validator component with a typed validation-result output.
5. Add assertion and metric components for the ops/QA era.
6. Rework node editor fields so each mapper's expressions and required actor input are obvious.
7. Move dashboard/metric blocks to consume projection/runtime outputs only, never separate live/replay paths.
