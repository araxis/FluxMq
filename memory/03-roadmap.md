# FluxMQ Roadmap

This is the staged implementation plan.

## Stage 0 - Project Setup

Goals:
- Create solution and project structure.
- Add MAUI Blazor Hybrid app.
- Add core class libraries.
- Add baseline tests where practical.
- Add package references for MQTTnet, LiteDB, and MudBlazor.

Deliverable:
- Empty but runnable app shell.

## Stage 1 - Core MQTT Session

Goals:
- Define connection profile model.
- Connect/disconnect to MQTT broker.
- Subscribe to topics.
- Receive messages through a channel.
- Publish simple messages.

Deliverable:
- Minimal working MQTT client inside FluxMQ.

## Stage 2 - Topic Explorer MVP

Goals:
- Build topic index from incoming messages.
- Render topic tree in Blazor.
- Show latest message/activity per topic.
- Add basic search/filter.

Deliverable:
- Usable real-time topic explorer.

## Stage 3 - LiteDB Persistence

Goals:
- Store connection profiles.
- Record message sessions.
- Store message envelopes with timestamps and topic.
- Add simple session list/load flow.

Deliverable:
- Local session recording and persistence.

## Stage 4 - Payload Inspector

Goals:
- Detect JSON, XML, Base64, text, and binary payloads.
- Show raw and formatted views.
- Add basic payload metadata.

Deliverable:
- Practical payload debugging view.

## Stage 5 - Replay MVP

Goals:
- Replay recorded sessions.
- Preserve relative timing.
- Allow speed control.
- Support replay publish into a selected broker.

Deliverable:
- First time-travel/debugging feature.

## Stage 6 - Observability MVP

Goals:
- Messages/sec.
- Payload size distribution.
- Topic activity overview.
- Basic silence/spike indicators.

Deliverable:
- First operational dashboard.

## Stage 7 - Formal Plugin Runtime

Goals:
- Extract stable module contracts.
- Add plugin abstractions.
- Add runtime loading.
- Add permission and failure isolation model.

Deliverable:
- External extensibility foundation.
