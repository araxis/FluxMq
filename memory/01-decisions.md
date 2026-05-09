# FluxMQ Decisions

This file records project decisions so they do not get lost across sessions.

## Accepted Decisions

### 2026-05-06 - Use LiteDB for local storage

Decision: Use LiteDB as the first local database.

Reasoning:
- FluxMQ is starting as a local-first desktop debugging and observability tool.
- LiteDB fits embedded desktop storage without requiring a separate database service.
- It is a good match for connection profiles, recorded sessions, replay metadata, payload indexes, app settings, and lightweight metrics.
- SQLite can remain an option later if relational querying, multi-process access, or heavier analytics become necessary.

Status: Accepted.

### 2026-05-06 - Build core first, formal plugin runtime later

Decision: Do not make external plugins the foundation of the MVP. Build stable internal modules first, then expose proven contracts through a plugin runtime.

Reasoning:
- The proposal's plugin direction is strong, but plugin APIs are expensive to change once externalized.
- Payload inspection, observability, and replay should first exist as internal modules.
- Once those module boundaries feel right, they can become plugin contracts.

Status: Accepted.

### 2026-05-06 - Message/session pipeline is the architectural spine

Decision: Center the architecture around MQTT sessions, message ingestion, processing, storage, and UI projection.

Reasoning:
- FluxMQ's real value comes from high-throughput debugging and replay, not just UI panels.
- The app needs a clean flow from MQTTnet into channels, processing, storage, metrics, and UI state.

Status: Accepted.

### 2026-05-06 - Keep project memory in Markdown

Decision: Use a dedicated `memory` folder with Markdown files for decisions, steps, progress, and architecture notes.

Reasoning:
- Keeps planning visible and versionable.
- Makes it easy to resume work without reconstructing context.

Status: Accepted.

### 2026-05-06 - Start with Windows-only MAUI target

Decision: Target `net10.0-windows10.0.19041.0` for the first MAUI Blazor Hybrid scaffold.

Reasoning:
- The first development environment is Windows desktop.
- The MAUI template generated mobile and Mac targets, but the available workload set did not cleanly restore all generated targets.
- Keeping the first target Windows-only makes the scaffold buildable immediately.
- Cross-platform targets can be reintroduced after the Windows desktop core is useful.

Status: Superseded by the 2026-05-09 workflow application host decision.

### 2026-05-06 - Use classic `.sln` instead of `.slnx`

Decision: Use a classic Visual Studio `.sln` file.

Reasoning:
- The .NET 11 preview CLI generated an empty `.slnx` and reported successful project additions that did not persist.
- A classic `.sln` restored, built, and tested reliably.

Status: Accepted.

### 2026-05-06 - Support both dark and light themes

Decision: FluxMQ will support both dark and light UI themes. Neither is the canonical mode.

Reasoning:
- Theme support (dark and light) is a baseline expectation for a desktop app, not a differentiating design choice.
- Calling it out in copy or docs implies it is optional or notable, which it is not.
- The IDE-like, operational character of the UI is the relevant design statement, not the color scheme.

Status: Accepted.

### 2026-05-07 - Visual pipeline editor with Blazor.Diagrams + JSON config + hot reload

Decision: Pipeline topologies will be user-defined via a visual drag-and-drop editor (Blazor.Diagrams), serialized to JSON, stored in LiteDB, and executed as live Dataflow networks. Pipeline changes must be hot-reloadable — no stop/start required.

Concept:
```
Blazor.Diagrams canvas (user drags nodes, draws connections)
  ↕ serialize / deserialize
FlowApplicationDefinition (JSON: resources + workflows + per-node config)
  ↕ stored in LiteDB
Flow application runtime
  → instantiates Dataflow blocks by node type
  → links them according to receiving-port links
  → produces running workflow graphs
```

Node library (`FluxMq.Modules.*`):
- Each module registers one or more node types.
- A node type declares: display name, input/output port descriptors, configurable properties (with schema for the UI property panel).
- Examples: TopicFilter, JsonDecoder, StorageSink, MetricsSink, ReplaySink, UiProjectionSink.

Hot-reload requirement:
- When node config changes: update the block's behaviour in-place (delegate swap) without touching the rest of the graph.
- When a connection is added/removed: patch only the affected link in the Dataflow graph; do not drain or restart unaffected blocks.
- In-flight messages in unaffected blocks must not be dropped during a patch.
- Some structural changes (e.g. removing the entry-point block) may require a brief coordinated pause; this is acceptable but must be explicit and fast.
- The flow application runtime must therefore support two modes: `Build` (cold start) and `Patch(delta)` (hot update from a diff of two `FlowApplicationDefinition` versions).

Architectural constraints for module authors:
- Block processing logic must be wrapped in a replaceable delegate so config-only changes can hot-swap without recreating the Dataflow block.
- Blocks must be disposable and must complete cleanly when unlinked.

When to introduce:
- Not before Stage 4 (Payload Inspector). At that point the module contracts will have been exercised enough to know what node metadata needs to express.
- Module contracts written from Stage 2 onwards must be designed with this in mind.

Status: Planned — target Stage 8.

### 2026-05-07 - MqttConnectionManager uses a session factory for testability

Decision: `MqttConnectionManager` accepts a `Func<MqttConnectionProfile, IMqttSession>` factory instead of hard-coding `new MqttSession(profile)`.

Reasoning:
- Allows tests to inject `FakeMqttSession` without a live broker.
- The default factory (`profile => new MqttSession(profile)`) keeps production behaviour unchanged.
- This is also the natural seam where Polly reconnect logic will be introduced — the factory or the manager's `ConnectAsync` wraps the call in a retry policy.

Status: Accepted.

### 2026-05-07 - Reconnect policy deferred to next step (Polly)

Decision: Reconnect on unexpected disconnect is not implemented yet. A comment in `MqttConnectionManager.OnSessionStateChanged` marks the exact extension point.

Reasoning:
- Polly is the agreed library for retry/reconnect.
- Getting the state notification pipeline right first (this step) is a prerequisite.
- The seam is clear: `OnSessionStateChanged` detects `Faulted`/`Disconnected` from an unexpected drop; Polly retry wraps `session.ConnectAsync` on next step.

Status: Deferred — next step.

### 2026-05-07 - Use TPL Dataflow for the message pipeline in FluxMq.Pipeline

Decision: Replace the hand-rolled `MessagePipeline` + `IMessageProcessor` with TPL Dataflow blocks (`BufferBlock` → `BroadcastBlock` → consumer `ActionBlock`s).

Reasoning:
- The pipeline topology is a graph (fan-out to topic index, storage, metrics, UI state), not a simple sequential list — Dataflow expresses this naturally.
- Dataflow provides backpressure, per-block parallelism, completion/fault propagation, and filtered linking out of the box.
- `FluxMq.Core` is unchanged — `MqttSession` still produces `Channel<MqttEnvelope>`. Dataflow is strictly a `FluxMq.Pipeline` concern.

Design:
- `MqttPipeline` feeds the channel into a `BufferBlock` (bounded, absorbs bursts).
- `BufferBlock` links to `BroadcastBlock` with identity clone (`MqttEnvelope` is immutable).
- Consumers call `pipeline.LinkTo(actionBlock)` or access `pipeline.Output` directly for filtered linking.

Status: Accepted.

### 2026-05-06 - Use a static mockup image as the README top banner

Decision: Use `design/ui-mockups/01-main-workspace.png` as the static banner at the top of the README. The animated GIF remains in the Visual Direction / Intro Animation section below.

Reasoning:
- A static image loads instantly and is always visible on any Markdown renderer.
- The GIF adds value lower in the page where motion is appropriate, but a banner should be immediate.
- Keeping both means first-time readers get a quick visual impression, then an animated walkthrough further down.

Status: Accepted.

### 2026-05-08 - Explore concrete Dataflow components before formal flow contracts

Decision: Build a small set of concrete Dataflow-backed components before defining shared flow contracts.

Reasoning:
- FluxMQ's user-defined Fork Flow model should grow from real working components, not from premature abstractions.
- Repositories, connection managers, UI components, and storage contexts remain normal services.
- Only behaviors that participate in configurable event movement should become flow components.
- Concrete components can expose typed `Input` and `Output` blocks first; repeated patterns will tell us later what contracts, descriptors, and config models are actually needed.
- Flow components should expose Dataflow lifecycle behavior (`Complete`, `Fault`, `Completion`) so Fork Flow execution can propagate completion and failure consistently.
- Flow nodes use `FlowNodeId`, a typed identifier, rather than primitive IDs.

Initial concrete components:
- Connection state trigger.
- Topic filter.
- Payload inspector mapper.

Status: Accepted.

### 2026-05-08 - Flow component failures must not terminate the app

Decision: Runtime component failures should be converted into typed flow error events instead of escaping as unhandled exceptions.

Reasoning:
- FluxMQ is a workflow-runtime tool that should remain usable even when a flow component, decoder, mapper, sink, or user-defined configuration fails.
- Component failures are operational data; they should be observable, routeable, and inspectable in Fork Flow.
- Each flow component should eventually expose an error output port for internal exceptions and recoverable processing failures.
- Flow errors should include stable plain numeric codes so dynamic components can route errors without depending on exception types or message text.
- The host shell and flow supervisor must isolate failed components so one node cannot terminate the running application.
- Truly unrecoverable node failures may stop that node, but the supervisor still converts the failure into flow state and an error event.

Status: Accepted.

### 2026-05-09 - Plan OpenTelemetry as an observability export layer

Decision: Add OpenTelemetry support later as an instrumentation and export layer for FluxMQ runtime activity.

Reasoning:
- FluxMQ should expose useful internal metrics in the app even when no external telemetry collector is configured.
- Flow components such as `MqttMetricsSinkComponent` should remain local, deterministic, and useful for UI projections.
- OpenTelemetry should complement those components by exporting selected counters, traces, and diagnostic events to external tools.
- The integration should not become a required runtime dependency for basic desktop use.
- OpenTelemetry naming and cardinality need deliberate design before implementation, especially around MQTT topics, sessions, profiles, flow nodes, and error codes.

Initial scope:
- Message throughput and payload-size metrics.
- Session lifecycle events.
- Flow node processing counts and error counts.
- Replay and recording operation spans.
- Optional exporter configuration from app settings.

Status: Planned.

### 2026-05-09 - Start Fork Flow configuration with application definitions and validation

Decision: Introduce an object-shaped Fork Flow application definition model before building the runtime graph builder.

Reasoning:
- Users should eventually be able to define Fork Flow through configuration and through the visual editor.
- The top-level model should represent one runnable application package with shared resources and multiple named workflows.
- Hand-authored config is more natural when workflows and nodes are named object properties instead of arrays.
- Shared resources such as broker connections and databases should live outside workflow objects so multiple workflows can reference them.
- Port links should live on the receiving component port, for example `Input: "source.Output"`.
- Ports should support one link, multiple links, and link objects with conditional routing metadata.
- Validation should catch broken graph references before runtime graph construction starts.
- Component factories, schemas, hot reload, and visual editor metadata should come after this definition shape proves useful.
- This keeps the project config-first without prematurely forcing every component into a heavy contract system.

Status: Accepted.

### 2026-05-09 - Keep flow application runtime host-independent

Decision: The future flow application runtime should be a class-library boundary that can run under the desktop app, a console runner, a service process, or command/tool hosts.

Reasoning:
- Reloading is a runtime concern, not a UI concern.
- The runtime should own application definition loading, validation, shared resource lifetime, workflow start/stop, completion propagation, reload coordination, and component error supervision.
- Hosts should call runtime APIs instead of knowing graph construction or patching details.
- Packaging the runtime independently keeps later command-line and service hosting natural.

Status: Planned.

### 2026-05-09 - Build runtime graphs through registered factories and typed ports

Decision: The first cold-start flow application runtime builder uses registered node factories and typed runtime port adapters instead of hard-coding component construction into the builder.

Reasoning:
- Component configuration schemas are not stable enough yet to bake concrete construction into the runtime core.
- Factories let the builder stay focused on validation, node creation orchestration, graph linking, and lifecycle shape.
- Typed runtime ports catch incompatible links during build before any workflow starts.
- Build failures should be structured results for ordinary definition and registration mistakes.
- Completion should start from graph entry nodes so Dataflow completion propagates through linked graphs in order.

Status: Accepted.

### 2026-05-09 - Remove the early Blazor shell and reserve FluxMq.App for the workflow application host

Decision: Remove the early Blazor shell from the current solution. Keep `FluxMq.App` reserved for a later application builder and workflow runtime host instead of keeping a placeholder UI app.

Reasoning:
- The current foundation work is centered on the runtime, definitions, components, storage, replay, and reusable UI pieces.
- A placeholder shell creates misleading architecture pressure before the runtime host shape is clear.
- The eventual `FluxMq.App` should compose and control flow applications, including loading, lifecycle, and reload behavior.
- Keeping the solution focused on libraries makes the runtime easier to test and package.

Status: Accepted.

### 2026-05-09 - Register only stable no-service pipeline components first

Decision: Start concrete runtime factory registrations with `mqtt.payload-inspector` and `mqtt.metrics-sink`.

Reasoning:
- Both components have stable constructor needs and typed port surfaces.
- Neither requires external service lifetime management.
- This lets real flow definitions build through the runtime without forcing unstable expression, predicate, connection, storage, replay, or publish configuration contracts.
- Predicate-driven and service-backed nodes should be registered after their configuration schemas and resource lifetime rules are clearer.

Status: Accepted.

### 2026-05-09 - Load alpha flow definitions through .NET configuration

Decision: Use the .NET configuration system as the first definition-loading boundary for the workflow application host.

Reasoning:
- JSON files are the simplest alpha input, but they should not become a custom loading mechanism.
- The same host path can later compose file, environment, command-line, LiteDB-backed, and UI-produced values.
- Keeping loading at the configuration boundary makes `FluxMq.Cli` natural without coupling the runtime to command-line parsing.
- `FluxMq.App` remains a class-library host boundary responsible for composition and lifecycle rather than a desktop UI project.

Status: Accepted.

### 2026-05-09 - Keep FluxMq.Cli lightweight at first

Decision: Add `FluxMq.Cli` as a thin host over `FluxMq.App`, not as a separate runtime path.

Reasoning:
- CLI support will be important for validating, running, inspecting, and automating flow applications.
- The first slice should avoid heavy CLI feature work until the host boundary is stable.
- CLI commands should call the same application host used by desktop or service hosts.
- The CLI should be automation-first: stable exit codes, predictable standard streams, and JSON output for tools and CI.
- Rich terminal output can be added later through a proper CLI library, while command execution stays separated from output rendering.
- `run` should exercise the host lifecycle only; message production, resource ownership, and service integrations should remain inside registered runtime components and resources.

Status: Accepted.
