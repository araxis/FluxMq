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

Status: Accepted.

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
PipelineDefinition (JSON: nodes + connections + per-node config)
  ↕ stored in LiteDB
PipelineBuilder
  → instantiates Dataflow blocks by node type
  → links them according to connections
  → produces a running MqttPipeline
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
- The `PipelineBuilder` must therefore support two modes: `Build` (cold start) and `Patch(delta)` (hot update from a diff of two `PipelineDefinition` versions).

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
- FluxMQ is a desktop tool that should remain usable even when a flow component, decoder, mapper, sink, or user-defined configuration fails.
- Component failures are operational data; they should be observable, routeable, and inspectable in Fork Flow.
- Each flow component should eventually expose an error output port for internal exceptions and recoverable processing failures.
- The app shell and flow supervisor must isolate failed components so one node cannot terminate the running application.
- Truly unrecoverable node failures may stop that node, but the supervisor still converts the failure into flow state and an error event.

Status: Accepted.
