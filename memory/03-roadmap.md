# FluxMQ Roadmap

This is the staged implementation plan.

## Current V1 Critical Path

The project is now past the basic runtime, component, scenario, and dashboard-layout foundations. The remaining V1 path is to finish package alignment, test/scenario component unification, dashboard runtime/widget binding, designer polish, release packaging, sample definitions, smoke documentation, and stale-code cleanup. Keep these as small vertical slices and merge each one independently.

## Stage 0 - Project Setup

Goals:
- Create solution and project structure.
- Add core class libraries.
- Add baseline tests where practical.
- Add package references for MQTTnet and LiteDB.

Deliverable:
- Buildable core library solution.

## Stage 1 - Core MQTT Client

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
- Metrics and dashboard blocks must consume runtime/projection streams, not separate live/offline code paths.
- Stored sessions must drive the same observability path as live broker traffic.

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
- Define a config-first `ApplicationDefinition` model with shared resources and object-shaped workflows.
- Support natural receiving-port links such as `Input: "source.Output"` and link objects such as `{ "From": "source.Output", "When": "condition" }`.
- Validate definitions before runtime graph construction.
- Introduce a host-independent flow application runtime class-library boundary.
- Load the first alpha definitions through the .NET configuration system.
- Let the runtime own shared resources, named workflow lifecycle, reload coordination, and component error supervision.
- Define `IPipelineNode` abstraction: display name, port descriptors, configurable properties with schema.
- Implement cold-start graph construction from `ApplicationDefinition`. Initial builder slice is in place with registered factories, factory placement context, typed port adapters, shared resource links, phase-based start ordering, disposal ordering, and structured build errors.
- Implement reload patching: diff two application definitions and apply only changed links/config/resources when safe.
- Integrate Blazor.Diagrams as the visual canvas in `FluxMq.UI`.
- Node palette (available block types from registered modules).
- Property panel (edit selected node's config; triggers hot-reload on save).
- Persist flow application definitions in LiteDB.
- Load/switch between saved flow application definitions at runtime.
- Add source binding so a logical source node can be run from live MQTT traffic, stored sessions, replay, imports, or test data without changing downstream workflow nodes.
- Move UI live update behavior behind runtime/projection streams so topic tree, recent messages, payload inspector, metrics, and dashboards share one update model.

Application host note:
- `FluxMq.App` is now the class-library workflow application host boundary.
- It should not become a generic placeholder UI shell.
- `FluxMq.Cli` is a thin host over `FluxMq.App`; the first commands validate and run flow application configuration with text and JSON output.

Hot-reload constraints:
- Config-only change on a node: delegate swap in-place, block stays running.
- Add connection: link new target block to existing source, no interruption.
- Remove connection: unlink target, complete it cleanly, no effect on remaining targets.
- Structural changes (e.g. remove entry-point block): coordinated brief pause, explicit and fast.

Deliverable:
- Users can build, save, and live-edit message pipeline topologies visually.
- Pipeline changes take effect immediately without stopping the session.

## Source-Agnostic Runtime Refactor

Goals:
- Replace split live/offline behavior with a single runtime source model.
- Treat source mode as an execution binding, not as a different workflow definition.
- Keep public update contracts Dataflow-native.
- Keep channels as internal producer details only where they are useful.
- Add streaming storage reads for stored sessions instead of loading full lists for runtime execution.
- Add deterministic ordering for stored messages so replay and offline dashboards are repeatable.
- Move UI update behavior into projection components that hold current state and publish typed updates.

Deliverable:
- Live broker traffic, stored sessions, replay, imports, and tests can feed the same workflows, UI projections, and dashboard blocks without changing downstream flow definitions.

## Alpha Desktop Workspace

Goals:
- Make `FluxMq.UI` a MAUI Blazor Hybrid desktop app.
- Use MudBlazor for the operational workspace.
- Use Blazor.Diagrams for the first visual Fork Flow surface.
- Connect to a local MQTT broker through normal TCP.
- Let users test, connect, subscribe, publish, inspect topics, and inspect payloads.
- Let users edit, save, load, validate, run, and stop flow application JSON files.
- Keep runtime execution behind `FluxMq.App` so CLI and desktop use the same host boundary.

Deliverable:
- A usable first alpha desktop app for Windows that can operate against a default local Mosquitto broker on `localhost:1883`.
