# FluxMQ Decisions

This file records project decisions so they do not get lost across sessions.

## Accepted Decisions

### 2026-05-22 - Components use explicit actor command inputs for side effects

Decision: Side-effecting flow components should consume explicit request/command input types instead of raw `MqttEnvelope` values when the operation needs intent beyond "observe this message." User-facing names should prefer actor language such as MQTT Publisher, File Writer, Recorder, HTTP Sender, or Email Sender instead of generic "sink" names.

Reasoning:
- A publish node should publish a `MqttPublishRequest`, not guess that any envelope should be republished as-is.
- A recording node should receive a `MqttRecordingRequest` that says what to record and where, instead of hiding `SessionId` in constructor configuration.
- Filters and routers become more meaningful when the next step is visible: `filter -> dynamic mapper -> actor`.
- Metrics remain stream observers over `MqttEnvelope`; they should not care about connection or subscription details.

Status: Accepted.

### 2026-05-22 - Dynamic mapping is a core runtime capability

Decision: Dynamic mapping and expression-based filtering/routing are first-class FluxMQ runtime capabilities, not optional UI sugar. The runtime should support expression-backed filters and mappers, starting with Dynamic Expresso for C#-style expressions and JSONata for JSON payload mapping/querying.

Reasoning:
- Developer ELT flows require mapping from incoming protocol messages into explicit actor commands such as `MqttPublishRequest`, `FileWriteRequest`, `HttpRequest`, and `EmailSendRequest`.
- Request models are actor input contracts, not user-facing component types. The visual/user-facing component is `flow.mapper`, currently fixed to `MqttEnvelope` input and configured with engine, typed runtime output target, output contract, and a single expression that returns the output object.
- A typical flow is: receive `MqttEnvelope`, filter by QoS/topic/payload, map to a publish request, then publish to another broker.
- Hard-coded mappers are useful tests and defaults, but the product power comes from user-authored mapping logic.
- The same expression/mapping foundation later supports ops features such as assertions, counters, summaries, rates, fault counts, and schema-based test expectations.

Status: Accepted.

### 2026-05-23 - Mapper contracts configure intent, validators live in runtime

Decision: The Dynamic Mapper editor owns the output contract selection because it describes what the mapper is expected to emit, but the validation implementation belongs in runtime/components as reusable services and standalone nodes.

Reasoning:
- `typed` mapper output keeps today's actor-request wiring path.
- `any` lets authors preview arbitrary expression output without pretending it is an actor command.
- `json-schema-file` records a schema contract, but JSON Schema validation must also be available as `json.schema-validator` for ops checks, assertions, and non-mapper flows.
- UI preview, CLI/runtime execution, and future assertion nodes should not each invent their own schema evaluator.

Status: Accepted.

### 2026-05-22 - FluxMQ has developer ELT and ops/testing eras

Decision: Treat FluxMQ as a serious flow platform with two product eras: first developer-oriented ELT/integration flows, then ops/QA-oriented testing, assertions, and observability over MQTT and future protocols.

Reasoning:
- MQTT is the first protocol, but the component model should also fit AMQP, HTTP, Bluetooth, file IO, email, and composed multi-protocol workflows.
- Developers need protocol bridging and transformation.
- Ops and QA teams need expectations like publish message X, receive response Y, validate response with JSON Schema, count faults, and measure per-topic rates.
- These features should share the same runtime primitives rather than becoming a separate dashboard-only system.

Status: Accepted.

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

### 2026-05-06 - Message/client pipeline is the architectural spine

Decision: Center the architecture around MQTT clients, message ingestion, processing, storage, and UI projection.

Reasoning:
- FluxMQ's real value comes from high-throughput debugging and replay, not just UI panels.
- The app needs a clean flow from MQTTnet into channels, processing, storage, metrics, and UI state.

Status: Accepted.

### 2026-05-14 - Workflow and application state exposed as ISourceBlock, not events

Decision: `Workflow.StateChanges` and `ApplicationRuntime.StateChanges` are `ISourceBlock<T>` backed by `BroadcastBlock<T>`, not `event EventHandler<T>`.

Reasoning:
- `IFlowNode.Errors` already uses `ISourceBlock<FlowError>` as the pipeline's data-out contract; state changes should follow the same pattern so they are first-class pipeline data.
- Consumers (logging, telemetry, UI projections) subscribe via `LinkTo` and can route, filter, or buffer state changes through Fork Flow like any other output.
- `event` handlers cannot participate in the dataflow graph and require manual subscription management.

Status: Accepted.

### 2026-05-14 - Phase-based lifecycle management via NodeDefinition.Phase

Decision: Startup ordering is controlled by an integer `Phase` property on `NodeDefinition` (default `0`). Lower values start first. All ordering logic lives in `ApplicationRuntime` and `Workflow`; components do not declare their own phase.

Reasoning:
- Startup order must be expressible per-node through configuration, not through component code or marker interfaces.
- Components should remain free of lifecycle ordering concerns — a node's phase is an operational concern belonging to the application definition, not to the node type.
- An earlier design using `IPreExecutionProcessor` and `IPostExecutionProcessor` marker interfaces on components was rejected because it mixed runtime ordering into the component layer.
- `IFlowStartable` was removed; `StartAsync` is now a default interface method on `IFlowNode` (`Task.CompletedTask` unless overridden), so every node is startable without extra interfaces.
- `RuntimeNode` carries `int Phase` (stamped by the builder from `NodeDefinition.Phase` after the factory runs, so factory code is unaffected).
- `ApplicationRuntime.StartAsync` and `Workflow.StartAsync` group all nodes by `Phase` ascending and await each group before the next. Resources and workflow nodes are unified in the loop so a workflow node at `Phase = -100` starts before a resource at `Phase = 0`.

Status: Accepted.

### 2026-05-09 - Give runtime factories placement context

Decision: Runtime node factories receive placement context, and runtime disposal releases workflow nodes before shared resources.

Reasoning:
- Service-backed resources need to know when they are declared as shared resources instead of ordinary workflow nodes.
- Resource lifetime should outlive dependent workflow nodes during shutdown.
- The builder should keep construction generic and let registered factories enforce resource-specific placement rules.

Status: Superseded by the 2026-05-14 phase-based lifecycle decision for startup ordering. Factory placement context remains accepted.

### 2026-05-15 - Move concrete components into FluxMq.Components

Decision: Keep `FluxMq.Pipeline` focused on definitions, runtime graph construction, typed ports, lifecycle primitives, and flow error contracts. Move concrete MQTT components, replay orchestration, LiteDB storage, and related tests into `FluxMq.Components`. Keep runtime component registration in `FluxMq.App` as the composition boundary.

Reasoning:
- The runtime primitives should not depend on LiteDB or concrete MQTT/storage component implementations.
- Concrete flow nodes are still first-class user-facing components, but they are not the runtime itself.
- `FluxMq.App` is the right composition point because it can register production components while tests can still register focused fake nodes.
- `FluxMq.UI` can reference the component package for local persistence, live metrics, replay, and desktop workspace services without pulling those dependencies into the runtime core.
- This boundary is closer to the future package shape: runtime primitives, concrete components, and host composition can evolve independently.

Status: Accepted.

### 2026-05-15 - Make runtime behavior source-agnostic

Decision: Live broker traffic and stored/offline traffic must enter Fork Flow through the same runtime source shape. Workflows, projections, dashboards, and UI update paths should consume typed runtime ports without knowing whether the input came from an online broker, stored session, replay, import, or test source.

Reasoning:
- The current live workspace path reads broker messages directly in UI service code, while stored sessions are loaded as a separate list. That split will create duplicate behavior in topic views, payload inspection, metrics, dashboard blocks, and future replay workflows.
- The workflow definition should describe message movement, not whether the source is online or offline.
- Source selection is an execution binding concern. A logical source node can be bound at runtime to a live MQTT source, stored session source, replay source, imported file source, or synthetic test source.
- Dashboards should bind to runtime/projection outputs only. A dashboard should not have separate live and replay implementations.
- Stored traffic must be streamable, not only loaded as full lists, so large sessions can drive the same runtime path without exhausting memory.

Status: Accepted.

### 2026-05-15 - Prefer Dataflow streams over EventHandler for runtime updates

Decision: Public runtime, component, projection, dashboard, and source update contracts should use Dataflow blocks (`ISourceBlock<T>`, `ITargetBlock<T>`, or typed runtime ports) rather than `EventHandler`. Channels may remain internal implementation details for low-level producers such as MQTT intake, but they should be adapted to Dataflow at the runtime boundary.

Reasoning:
- Dataflow carries the semantics FluxMQ needs: backpressure, completion, fault propagation, linking, unlinking, fan-out, and typed ports.
- `EventHandler` is too weak for runtime behavior because it has no native completion, fault, backpressure, or graph-linking semantics.
- Channels are excellent for single producer/consumer hot paths, but they do not express graph composition by themselves.
- UI components can still trigger `StateHasChanged`, but the data they observe should come from source/projection streams or durable projection state rather than ad hoc events.
- Projection objects should hold current state, while Dataflow streams carry updates. A late UI subscriber should read the latest projection snapshot and then receive future updates.

Status: Accepted.

### 2026-05-16 - Override MudBlazor styles via app.css, not component isolated CSS

Decision: All MudBlazor internal CSS class overrides go in `wwwroot/app.css` as plain global rules. Component `.razor.css` files are used only for classes that the component itself renders directly.

Reasoning:
- Blazor CSS isolation applies the scope attribute (`b-xxx`) only to HTML elements authored in the current component's template. Child component internals (e.g. `MudTreeView`'s `.mud-treeview-item-content`) never receive the scope attribute, so `[b-xxx] .mud-treeview-item-content` never matches.
- `::deep` in isolated CSS requires the component to have a plain HTML wrapper element (`<div>`) around the MudBlazor component. Without the wrapper, the scope attribute has no ancestor that is an ancestor of the target element.
- MudBlazor confirms this limitation in their own documentation.

Status: Accepted.

### 2026-05-22 - Use an explicit desktop grid shell for the FluxMQ redesign

Decision: The redesigned `FluxMq.UI` workspace shell uses a purpose-built CSS grid layout instead of MudBlazor drawers/app bars for the primary desktop frame.

Reasoning:
- `memory/fluxmq-redesign.html` defines a precise operational desktop layout: 48px top bar, 52px icon rail, compact left explorer, full canvas, right inspector, and 28px status bar.
- MudBlazor drawers and app bars are useful primitives, but their generated padding, clipping, and responsive behavior made exact panel alignment, hover actions, and canvas sizing harder to control.
- The shell grid is a stable application frame, while MudBlazor remains the component system for buttons, tabs, forms, dialogs, tables, chips, and icons.
- Component-isolated CSS is used for authored shell/panel elements; global `app.css` remains the place for MudBlazor internals and shared design tokens.

Status: Accepted.

### 2026-05-22 - Flux redesign tokens must follow Light/Dark/System theme

Decision: The custom `--flux-*` design tokens are scoped under shell classes (`flux-theme-light` / `flux-theme-dark`) derived from `AppThemeService.IsDarkMode`. They must not be hard-coded globally on `:root`.

Reasoning:
- FluxMQ explicitly supports Light, Dark, and System themes.
- The redesign can keep its visual language in both modes, but the shell, panels, canvas, buttons, borders, shadows, and hover states must switch with the selected theme.
- Heavy MudBlazor internal overrides should be scoped under `.flux-shell` so app dialogs, popovers, and providers can continue to follow MudBlazor's own theme variables.

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
ApplicationDefinition (JSON: resources + workflows + per-node config)
  ↕ stored in LiteDB
Flow application runtime
  → instantiates Dataflow blocks by node type
  → links them according to receiving-port links
  → produces running workflow graphs
```

Node library (`FluxMq.Modules.*`):
- Each module registers one or more node types.
- A node type declares: display name, input/output port descriptors, configurable properties (with schema for the UI property panel).
- Examples: TopicFilter, JsonDecoder, Recorder, MQTT Publisher, MQTT Metrics, UI Projection.

Hot-reload requirement:
- When node config changes: update the block's behaviour in-place (delegate swap) without touching the rest of the graph.
- When a connection is added/removed: patch only the affected link in the Dataflow graph; do not drain or restart unaffected blocks.
- In-flight messages in unaffected blocks must not be dropped during a patch.
- Some structural changes (e.g. removing the entry-point block) may require a brief coordinated pause; this is acceptable but must be explicit and fast.
- The flow application runtime must therefore support two modes: `Build` (cold start) and `Patch(delta)` (hot update from a diff of two `ApplicationDefinition` versions).

Architectural constraints for module authors:
- Block processing logic must be wrapped in a replaceable delegate so config-only changes can hot-swap without recreating the Dataflow block.
- Blocks must be disposable and must complete cleanly when unlinked.

When to introduce:
- Not before Stage 4 (Payload Inspector). At that point the module contracts will have been exercised enough to know what node metadata needs to express.
- Module contracts written from Stage 2 onwards must be designed with this in mind.

Status: Planned — target Stage 8.

### 2026-05-07 - MqttConnectionManager uses a client factory for testability

Decision: `MqttConnectionManager` accepts a `Func<MqttConnectionProfile, IFluxMqttClient>` factory instead of hard-coding `new FluxMqttClient(profile)`.

Reasoning:
- Allows tests to inject `FakeFluxMqttClient` without a live broker.
- The default factory (`profile => new FluxMqttClient(profile)`) keeps production behaviour unchanged.
- This is also the natural boundary where Polly reconnect logic will be introduced: the factory or the manager's `ConnectAsync` wraps the call in a retry policy.

Status: Accepted.

### 2026-05-07 - Reconnect policy deferred to next step (Polly)

Decision: Reconnect on unexpected disconnect is not implemented yet. A comment in `MqttConnectionManager.OnClientStateChanged` marks the exact extension point.

Reasoning:
- Polly is the agreed library for retry/reconnect.
- Getting the state notification pipeline right first (this step) is a prerequisite.
- The seam is clear: `OnClientStateChanged` detects `Faulted`/`Disconnected` from an unexpected drop; Polly retry wraps `client.ConnectAsync` on next step.

Status: Deferred — next step.

### 2026-05-07 - Use TPL Dataflow for the message pipeline in FluxMq.Pipeline

Decision: Replace the hand-rolled `MessagePipeline` + `IMessageProcessor` with TPL Dataflow blocks (`BufferBlock` → `BroadcastBlock` → consumer `ActionBlock`s).

Reasoning:
- The pipeline topology is a graph (fan-out to topic index, storage, metrics, UI state), not a simple sequential list — Dataflow expresses this naturally.
- Dataflow provides backpressure, per-block parallelism, completion/fault propagation, and filtered linking out of the box.
- `FluxMq.Core` is unchanged — `FluxMqttClient` still produces `Channel<MqttEnvelope>`. Dataflow is strictly a `FluxMq.Pipeline` concern.

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
- Flow components such as `MqttMetricsComponent` should remain local, deterministic, and useful for UI projections.
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

Decision: Start concrete runtime factory registrations with `mqtt.payload-inspector` and `mqtt.metrics`.

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
- Use `Spectre.Console.Cli` for command parsing and command dispatch, while keeping command execution separated from output rendering.
- `run` should exercise the host lifecycle only; message production, resource ownership, and service integrations should remain inside registered runtime components and resources.

Status: Accepted.

### 2026-05-09 - Split MQTT intake into connection resources and trigger nodes

Decision: Register `mqtt.connection` as the shared MQTT client resource and `mqtt.trigger` as the workflow node that subscribes through that resource and emits matching messages.

Reasoning:
- The runtime lifecycle path for resource start/dispose should be exercised by a real component, not only no-service components.
- Message ingestion needs a config-first path that can run under `FlowApplicationHost` and `FluxMq.Cli`.
- Connections and subscriptions have different lifetimes: a connection can be shared across workflows, while each trigger owns the topic filters that define when it emits.
- This matches the object-shaped definition model: shared broker configuration lives under `resources`, and workflow behavior lives under named workflow nodes.
- Factory-level parsing gives clear build errors for invalid `profile`, `subscriptions`, or `qos` values before runtime start.
- Keeping a client factory parameter on registration preserves deterministic testability while production stays on `FluxMqttClient`.

Status: Accepted.

### 2026-05-10 - Build the alpha desktop surface as a MAUI Blazor Hybrid app

Decision: `FluxMq.UI` is the alpha desktop application surface, implemented as a Windows-first MAUI Blazor Hybrid app using MudBlazor and Blazor.Diagrams.

Reasoning:
- The app must connect to a local MQTT broker through normal TCP, read and write local files, and host the same workflow runtime used by CLI and future hosts.
- MAUI Blazor Hybrid keeps the Blazor component model and MudBlazor UI while allowing native desktop access to files and broker connections.
- `FluxMq.App` remains the host-independent workflow application boundary, not a UI shell.
- `FluxMq.UI` composes the desktop workspace, live broker tools, visual flow definition surface, file load/save, and runtime controls.
- The first alpha target remains Windows desktop because the current broker and development environment are Windows-based.

Status: Accepted.
