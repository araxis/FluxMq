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
- Internal metrics snapshots for UI projection.
- Planned OpenTelemetry export for selected runtime signals.

Deliverable:
- First operational dashboard.

OpenTelemetry is not required for the MVP dashboard. It should be introduced after the internal metric model is stable enough to define safe names, units, attributes, and cardinality limits.

## Stage 7 - Formal Plugin Runtime

Goals:
- Extract stable module contracts.
- Add plugin abstractions.
- Add runtime loading.
- Add permission and failure isolation model.

Deliverable:
- External extensibility foundation.

## Stage 8 - Visual Pipeline Editor

Goals:
- Define `IPipelineNode` abstraction: display name, port descriptors, configurable properties with schema.
- Define `PipelineDefinition` JSON model (nodes + connections + per-node config).
- Implement `PipelineBuilder.Build(definition)` — cold-start Dataflow graph from JSON.
- Implement `PipelineBuilder.Patch(current, next)` — hot-reload: diff two definitions and apply only changed links/config without stopping unaffected blocks or dropping in-flight messages.
- Integrate Blazor.Diagrams as the visual canvas in `FluxMq.UI`.
- Node palette (available block types from registered modules).
- Property panel (edit selected node's config; triggers hot-reload on save).
- Persist pipeline definitions in LiteDB.
- Load/switch between saved pipeline definitions at runtime.

Hot-reload constraints:
- Config-only change on a node: delegate swap in-place, block stays running.
- Add connection: link new target block to existing source, no interruption.
- Remove connection: unlink target, complete it cleanly, no effect on remaining targets.
- Structural changes (e.g. remove entry-point block): coordinated brief pause, explicit and fast.

Deliverable:
- Users can build, save, and live-edit message pipeline topologies visually.
- Pipeline changes take effect immediately without stopping the session.
