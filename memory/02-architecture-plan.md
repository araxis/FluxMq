# FluxMQ Architecture Plan

This is the current architecture direction. The original proposal remains useful as the product north star, but this plan is the working build direction.

## Architectural Principle

Build FluxMQ around the message/session flow first. Treat plugins as a future formalization of stable internal module contracts.

## Proposed Solution Structure

```text
/src
  /FluxMq.App
  /FluxMq.Core
  /FluxMq.Pipeline
  /FluxMq.Storage
  /FluxMq.UI
  /FluxMq.Modules.PayloadInspector
  /FluxMq.Modules.Observability
  /FluxMq.Modules.Replay
  /FluxMq.Plugins.Abstractions
  /FluxMq.Plugins.Runtime
/memory
```

## Project Responsibilities

### FluxMq.App

MAUI and Blazor Hybrid app shell.

Responsibilities:
- App startup and dependency injection composition.
- Window/app lifecycle.
- Main layout.
- Platform-specific services.

### FluxMq.Core

Core domain model and MQTT session behavior.

Responsibilities:
- Connection profiles.
- MQTT session lifecycle.
- Topic model.
- Message envelope model.
- Broker capability abstractions.

### FluxMq.Pipeline

Message ingestion and processing.

Responsibilities:
- Channel-based message ingestion.
- Ordered message processors.
- Decode/enrichment hooks.
- Filtering and drop rules.
- Backpressure strategy.

### FluxMq.Storage

Persistence using LiteDB.

Responsibilities:
- Connection profile persistence.
- Session recording.
- Replay session metadata.
- Message storage.
- Lightweight metrics snapshots.
- App settings.

### FluxMq.UI

Reusable Blazor UI components.

Responsibilities:
- Topic tree components.
- Message timeline components.
- Payload viewing components.
- Shared layout and state helpers.

### FluxMq.Modules.PayloadInspector

Internal module for payload decoding and comparison.

Responsibilities:
- JSON/XML/Base64/binary detection.
- Pretty printing.
- Diff support.
- Schema-aware inspection later.

### FluxMq.Modules.Observability

Internal module for metrics and operational dashboards.

Responsibilities:
- Messages per second.
- Payload size statistics.
- Topic activity.
- Silence/spike detection later.

### FluxMq.Modules.Replay

Internal module for time travel and replay.

Responsibilities:
- Session timeline.
- Replay with timing control.
- Export/import later.

### FluxMq.Plugins.Abstractions

Future public plugin contracts.

Initial status:
- Keep minimal or postpone until internal module contracts settle.

### FluxMq.Plugins.Runtime

Future dynamic plugin loader.

Initial status:
- Not MVP-critical.
- Add after internal modules prove extension boundaries.

## Core Message Flow

```text
MQTTnet Client
  -> MqttSession
  -> Channel<MqttEnvelope>
  -> Message Pipeline
  -> Storage / Metrics / Topic Index
  -> Blazor UI State
```

## Core Domain Types

Early types to define:

```text
MqttConnectionProfile
MqttSession
MqttEnvelope
DecodedPayload
TopicNode
MessageTimeline
ReplaySession
```

## Storage Direction

Use LiteDB for local-first persistence.

Likely collections:

```text
connection_profiles
sessions
messages
topic_snapshots
replay_sessions
metric_snapshots
app_settings
```

## Plugin Direction

Start with internal modules:

```text
FluxMq.Modules.PayloadInspector
FluxMq.Modules.Observability
FluxMq.Modules.Replay
```

Later, extract stable extension points:

```text
IMessageProcessor
IPayloadDecoder
IUiContribution
IFluxMqModule
```

Avoid external assembly loading until the core app behavior is useful and the contracts have been exercised internally.
