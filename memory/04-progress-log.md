# FluxMQ Progress Log

Chronological progress record.

## 2026-05-06

- Read the initial FluxMQ proposal.
- Chose to treat the proposal as a product north star, not a fixed architecture.
- Agreed that LiteDB is a good first storage database.
- Decided to prioritize the message/session pipeline before formal external plugins.
- Created the `memory` folder for project continuity.
- Renamed the original proposal to `FluxMQ-Platform-Proposal.md`.
- Added project memory files:
  - `00-index.md`
  - `01-decisions.md`
  - `02-architecture-plan.md`
  - `03-roadmap.md`
  - `04-progress-log.md`
- Created the initial .NET solution scaffold:
  - `FluxMq.App`
  - `FluxMq.Core`
  - `FluxMq.Pipeline`
  - `FluxMq.Storage`
  - `FluxMq.UI`
  - `FluxMq.Core.Tests`
  - `FluxMq.Pipeline.Tests`
  - `FluxMq.Storage.Tests`
- Added initial package references:
  - MQTTnet in `FluxMq.Core`
  - LiteDB in `FluxMq.Storage`
  - MudBlazor in `FluxMq.App`
  - FluentAssertions in test projects
- Wired MudBlazor into the MAUI Blazor app.
- Normalized projects to .NET 10 target frameworks because the MAUI Blazor template generated `net10.0` targets.
- Limited the first MAUI target to Windows desktop.
- Replaced the empty generated `.slnx` with a classic `.sln`.
- Added a root `.gitignore`.
- Verified `dotnet restore`, `dotnet build`, and `dotnet test` all pass.
- Initialized a local Git repository on branch `main`.
- Created the initial commit: `11f00d1 Initial FluxMQ scaffold`.
- Checked GitHub profile through the connected GitHub app: `araxis`.
- Confirmed no existing `FluxMq` repository was found under that profile through repository search.
- Used GitHub CLI from `C:\Program Files\GitHub CLI\gh.exe`.
- Created private GitHub repository: `https://github.com/araxis/FluxMq`.
- Added `origin` remote and pushed `main`.
- Verified remote visibility is private and default branch is `main`.
- Added initial `README.md` with project vision, status, architecture direction, build commands, and links to memory docs.
- Created initial UI mockup assets under `design/ui-mockups/`:
  - `01-main-workspace.png`
  - `02-payload-debugger.png`
  - `03-observability-replay.png`
- Added `design/ui-mockups/render_fluxmq_mockups.py` to regenerate the mockups deterministically.
- Added `design/ui-mockups/README.md` describing the UI direction.
- Installed Pillow locally for Python-based mockup rendering.
- Installed Node.js LTS for Remotion work.
- Created a Remotion intro animation under `design/intro-animation/`.
- Rendered intro outputs:
  - `design/intro-animation/out/fluxmq-intro.mp4`
  - `design/intro-animation/out/fluxmq-intro-poster.png`
- Updated the root `README.md` with the UI mockups, intro poster, and intro animation link.
- Changed the intro animation README section to use an HTML `<video controls>` block with a fallback link.

- Converted the intro animation from MP4 to GIF using Remotion's built-in GIF codec:
  - Output: `design/intro-animation/out/fluxmq-intro.gif` (960×540, 15 fps, 5.8 MB).
  - Render parameters: `--codec=gif --scale=0.5 --every-nth-frame=2`.
  - Added `render:gif` npm script to `design/intro-animation/package.json`.
- Replaced the unplayable `<video>` embed in the README with a `![img]` GIF embed (GitHub does not render `<video>`).
- Added `design/ui-mockups/01-main-workspace.png` as a full-width static banner at the very top of the README.
- Removed "dark" from the Visual Direction description: FluxMQ supports both dark and light themes; it is not a defining characteristic worth calling out.
- Trimmed README noise: removed the Remotion/MP4 attribution line, the `### UI Mockups` section header, and the `Primary MQTT operations workspace:` caption.
- Merged PR #1: GIF banner + video embed fix.
- Opened PR #2 (`readme-banner-cleanup`): static mockup banner + README cleanup.

## 2026-05-07

- Implemented Stage 1 — core MQTT session and pipeline foundation (PR #4):
  - `FluxMq.Core`: `MqttConnectionProfile`, `MqttEnvelope`, `MqttSessionState`, `IMqttSession`, `MqttSession` (MQTTnet wrapper, messages → bounded `Channel<MqttEnvelope>`).
  - `FluxMq.Pipeline`: initial `IMessageProcessor` + `MessagePipeline` (sequential fan-out).
  - 13 tests passing.
- Replaced sequential pipeline with TPL Dataflow (PR #5):
  - Removed `IMessageProcessor` and `MessagePipeline`.
  - Added `MqttPipeline`: `BufferBlock` → `BroadcastBlock` → consumer `ActionBlock`s.
  - `pipeline.LinkTo(block)` for simple sinks; `pipeline.Output` for filtered linking.
  - 15 tests passing (8 core, 6 pipeline, 1 storage placeholder).

- Added connection state management:
  - `IMqttSession.StateChanged` event — fires on every state transition.
  - `MqttSession` wires `IMqttClient.DisconnectedAsync` to detect unexpected drops; sets `Faulted` if exception present, `Disconnected` otherwise.
  - `SetState` helper centralises all state writes and event firing.
  - `SessionStateChangedEventArgs` — carries session ID, profile, and new state.
  - `IMqttConnectionManager` / `MqttConnectionManager` — creates, tracks, and disposes sessions; forwards `StateChanged`; uses injected factory for testability.
  - Reconnect hook (Polly) left as a comment in `OnSessionStateChanged`.
  - 6 new connection manager tests using `FakeMqttSession` (no broker required). 21 tests total passing.

- Decided on visual pipeline editor direction (Stage 8):
  - Blazor.Diagrams for drag-and-drop topology editing.
  - Flow application definition JSON model persisted in LiteDB.
  - Flow application runtime with cold `Build` and hot `Patch` modes.
  - Hot-reload requirement: config changes and link changes apply in-place without stopping unaffected blocks or dropping in-flight messages.
  - Module contracts from Stage 2 onwards must be designed with node metadata (ports, configurable properties) in mind.

- Implemented Polly reconnect in `MqttConnectionManager`:
  - Added `MqttSessionState.Reconnecting` — surfaced to UI on each retry attempt.
  - `MqttSession.OnClientDisconnectedAsync` no longer completes the channel on unexpected drops — channel stays open so reconnect resumes message flow seamlessly.
  - `MqttConnectionManager` schedules a background reconnect task on `Faulted` or unexpected `Disconnected`; uses an injectable `ResiliencePipeline` (default: exponential backoff 1s → 30s with jitter, infinite retries).
  - `DisconnectAsync` and `RemoveAsync` cancel any in-progress reconnect before acting.
  - `BuildDefaultReconnectPipeline()` is the production default; tests inject `InstantRetry` (zero delay, 5 attempts).
  - 23 tests passing (16 core, 6 pipeline, 1 storage).

- Implemented Stage 2 — Topic Explorer MVP:
  - `FluxMq.Core/TopicIndex`: `TopicNode` (thread-safe, immutable record of a topic segment), `ITopicIndex`, `TopicIndex` (ConcurrentDictionary tree, BFS flatten for Search).
  - `TopicIndex.Changed` event fires per message; documented as high-frequency — consumers must throttle.
  - `FluxMq.UI`: removed scaffold placeholders; added MudBlazor 9.4.0; updated `_Imports.razor`.
  - Two Blazor components:
    - `TopicTreeView.razor` — search input + tree/flat-list toggle; 250ms timer-based throttle for `StateHasChanged`.
    - `TopicTreeNode.razor` — recursive expand/collapse node with name, message count, last activity timestamp.
  - 12 new `TopicIndex` tests; 35 tests total passing.

## 2026-05-08

- Implemented Stage 3 — LiteDB persistence:
  - `FluxDbContext`: wraps `ILiteDatabase`, exposes typed `ILiteCollection<T>` properties for all three domains; EnsureIndexes on `SessionId`, `Topic`, and `ProfileId`; accepts injected `ILiteDatabase` for test isolation.
  - `StoredSession` model: `Id`, `ProfileId`, `ProfileName`, `StartedAt`, `EndedAt`; static `From(MqttConnectionProfile)` factory.
  - `StoredMessage` model: `Id`, `SessionId`, `Topic`, `Payload` (byte[]), `ReceivedAt`, `QualityOfService`, `Retain`; static `From(sessionId, envelope)` factory and `ToEnvelope()` round-trip method.
  - Three repository interface + LiteDB implementation pairs:
    - `IConnectionProfileRepository` / `LiteDbConnectionProfileRepository` — `Get`, `GetAll`, `Save` (upsert), `Delete`.
    - `ISessionRepository` / `LiteDbSessionRepository` — `Start` (insert), `End` (update EndedAt), `Get`, `GetAll` (ordered desc by StartedAt), `Delete`.
    - `IMessageRepository` / `LiteDbMessageRepository` — `Add`, `AddBatch` (bulk insert), `GetBySession` (ordered asc by ReceivedAt), `GetByTopic`, `CountBySession`.
  - 19 tests across three test classes, all using `new LiteDatabase(":memory:")` — no file I/O, fully isolated per class.
  - All 19 tests passing (54 total in solution).

- Fixed LiteDB session indexing:
  - `FluxDbContext` now indexes `StoredSession.ProfileId` by field name instead of an expression.
  - Reason: LiteDB's expression mapper does not resolve the strongly typed `ConnectionProfileId` member correctly in this index expression.
  - Verified with `dotnet test FluxMq.sln`: 53 tests passing.

- Implemented Stage 4 — Payload Inspector:
  - Added `PayloadInspector` in `FluxMq.Core` for JSON, XML, Base64, plain text, binary, and empty payload detection.
  - Added payload metadata and hex dump generation.
  - Added `PayloadInspectorPanel` in `FluxMq.UI` with formatted, raw, hex, and metadata tabs.
  - Replaced the default home page with a simple MQTT message inspection workspace preview.
  - Added 6 payload inspector tests covering JSON, XML, Base64, text, binary, and empty payloads.
  - Verified `dotnet test FluxMq.sln`: 59 tests passing.
  - Verified `dotnet build src\FluxMq.App\FluxMq.App.csproj`: build passing.

- Started Fork Flow foundation with concrete Dataflow-backed components:
  - Added a connection state trigger component that broadcasts connection state changes.
  - Added a topic filter component backed by `TransformManyBlock`.
  - Added a payload inspector mapper component backed by `TransformBlock`.
  - Added a minimal `IFlowNode` lifecycle surface with typed `FlowNodeId`.
  - Added component tests for message flow, typed node IDs, completion, and fault behavior.

- Recorded Fork Flow failure isolation rule:
  - Component exceptions and recoverable processing failures should become typed flow error events.
  - Flow nodes should eventually expose an error output port.
  - A failed node must not terminate the running application.

- Implemented first flow error ports:
  - Added `FlowError` with typed `FlowNodeId`, message, optional exception, timestamp, and context.
  - Added plain numeric `FlowErrorCodes` constants for stable error routing.
  - Added `Errors` output to `IFlowNode`.
  - Topic filter predicate failures now publish error events and continue processing later messages.
  - Mapper/filter error ports remain open until pending work drains during completion.

## 2026-05-09

- Added a static documentation site under `docs-site/` using VitePress for user-facing GitHub Pages documentation.
- Kept `docs/` as developer and future Wiki-oriented documentation.
- Added `docs/documentation-strategy.md` to define the split between developer docs and user docs.
- Added a GitHub Pages workflow for the docs site and a Dependabot configuration for docs-site packages and GitHub Actions.
- Updated the Pages workflow to `actions/configure-pages@v6` and ignored local VitePress cache output.
- Started the next Fork Flow component after replay and publish support: live MQTT intake from an active session.
- Added an MQTT intake prototype to bridge `IMqttSession.Messages` into Dataflow-backed Fork Flow graphs.
- Added tests for message order, reader completion, reader failure conversion to `FlowError`, clean completion, and explicit fault behavior.
- Added `MqttConditionRouterComponent` to route `MqttEnvelope` values into true/false branches.
- Added tests for topic-prefix routing, predicate failure conversion to `FlowError`, pending-error completion, and explicit fault behavior.
- Added `MqttRecorderComponent` in `FluxMq.Replay` so recording can remain a flow component without making `FluxMq.Pipeline` depend on storage.
- Added tests for recording order, repository failure conversion to `FlowError`, continued processing after failed writes, and explicit fault behavior.
- Added `MqttMetricsComponent` and `MqttMetricsSnapshot` in `FluxMq.Pipeline` for observability projections.
- Added tests for snapshot updates, empty metrics, processing failure conversion to `FlowError`, and explicit fault behavior.
- Recorded OpenTelemetry as a planned observability export layer, separate from local flow metrics and UI projections.
- Added the initial config-first Fork Flow application definition model with object-shaped workflows, shared resources, typed node types, typed port names, string/object link parsing, default link conditions, JSON serialization options, and validation for missing graph references.
- Recorded the future flow application runtime as a host-independent class-library boundary responsible for loading definitions, owning resources, controlling workflow lifecycle, coordinating reloads, and supervising component errors.
- Added the first cold-start runtime builder slice:
  - factory registry for runtime node creation
  - typed input/output runtime port adapters
  - application definition validation before build
  - workflow and shared resource linking
  - structured build errors for missing factories, missing ports, type mismatches, and link failures
  - entry-node completion so Dataflow graphs drain in link order
- Removed the early Blazor shell from the current solution. `FluxMq.App` is now reserved for a later workflow application host and builder, after the runtime host boundary is clearer.
- Added the first concrete pipeline component factory registrations:
  - `mqtt.payload-inspector`
  - `mqtt.metrics`
- Added runtime tests proving registered components can be linked through flow definitions and that invalid component configuration becomes a structured build error.
- Reintroduced `FluxMq.App` as a class-library workflow application host boundary.
- Added `FlowApplicationConfigurationLoader` to load `FluxMq:FlowApplication` through .NET configuration.
- Added `FlowApplicationHost` with build, start, stop, state, and structured host build errors.
- Added `FluxMq.App.Tests` covering configuration loading, runtime build, stop completion, missing configuration, and invalid component configuration.
- Added `FluxMq.Cli` as the first thin command-line host over `FluxMq.App`.
- Added `validate --config <path>` to validate a flow application configuration through the application host.
- Added `--output json` for machine-readable validation results while keeping text output as the default.
- Added `samples/flow-applications/metrics-only.json` as the first alpha validation sample.
- Added `run --config <path>` as the first command-line host lifecycle path with cancellation, optional bounded duration, text output, and JSON output.
- Added `FlowRuntimeNodeFactoryContext` so node factories can distinguish shared resources from workflow nodes.
- Added `IFlowStartable` and runtime start ordering so shared resources start before workflow nodes.
- Converted startup failures into structured host errors.
- Updated runtime disposal ordering so workflow nodes are disposed before shared resources.
- Added service-backed MQTT intake runtime registrations.
- Added `MqttSubscription` and config parsing for profile/subscriptions/qos in flow definitions.
- Added runtime tests proving resource startup, subscription, and downstream delivery through workflow links.
- Added runtime validation tests for missing subscriptions.
- Replaced CLI hand-rolled argument parsing with `Spectre.Console.Cli` command/settings handlers.
- Removed parser-only CLI code and parser-specific tests.
- Kept CLI execution contracts stable by routing Spectre command handlers into the existing runner and result renderers.
- Added a lightweight DI-backed type registrar for command activation.
- Verified with `dotnet test tests/FluxMq.Cli.Tests/FluxMq.Cli.Tests.csproj` and `dotnet test FluxMq.sln`.

## Current Next Step

Build the first usable MAUI Blazor Hybrid desktop alpha in `FluxMq.UI`.


## 2026-05-10

- Closed the rejected separate desktop host PR and returned to `main`.
- Confirmed the existing solution direction: `FluxMq.App` remains the host-independent workflow application boundary.
- Converted `FluxMq.UI` into the Windows-first MAUI Blazor Hybrid desktop app surface.
- Added MudBlazor shell, light/dark/system theme selection, app icon, splash asset, and BlazorWebView host.
- Added Blazor.Diagrams as the Fork Flow visual canvas dependency.
- Added the alpha workspace:
  - broker profile editor for local MQTT connection settings
  - connection test, connect, disconnect, subscribe, and publish actions
  - live topic tree and recent message list
  - payload inspector panel wired to live MQTT messages
  - LiteDB-backed recording controls for live traffic
  - component catalog for registered runtime node types
  - visual diagram projection from the flow definition JSON
  - file path based definition load/save
  - validate, run, and stop controls through `FluxMq.App`
- Added focused UI service tests for definition generation, host validation, file round-trip, and invalid JSON diagnostics.
- Replaced blank default diagram nodes with FluxMQ flow node widgets that show name, type, category, ports, and collapsible details.
- Added project/session selection over LiteDB recording sessions so recorded traffic can be grouped and loaded back into the workspace.
- Renamed the UI definition helper to `FlowDefinitionComposer` to make its goal clearer: compose valid definition JSON from UI actions, not build runtime nodes.
- Reworked the desktop workspace layout so the left and right columns collapse like side panels and can be resized with dedicated desktop splitters.
- Moved the raw definition editor behind a collapsed panel so the visual workspace remains primary.
- Wired the topic tree to the same live or selected-session message collection used by the message table, with branch selection filtering descendant topics.
- Added diagram helper widgets from Blazor.Diagrams, including grid and navigator views, and surfaced live message/runtime activity on diagram nodes.
- Added a Windows packaging workflow that produces a portable `win-x64` zip and MSI installer from the MAUI desktop app publish output.
- Declared `win-x64` as the desktop app runtime so clean CI restores include the Windows runtime pack needed by MAUI test and package builds.
- Disabled ReadyToRun for the alpha desktop app to keep clean hosted test/package builds reliable while installer signing and release optimization are still future work.
- Added WiX installer authoring and a reusable local packaging script.
- Updated the workspace splitters to use pointer-captured mouse dragging for reliable column width resizing.
- Stopped non-definition workspace changes from rebuilding the diagram; live traffic and runtime status now update node activity in place.
- Updated diagram payload inspector activity to show the latest inspected message payload instead of stale selected-message state.
- Split live MQTT intake into explicit `mqtt.connection` and `mqtt.trigger` runtime registrations.
- Added `MqttConnectionComponent` as a shared resource that owns the session and `MqttTriggerComponent` as the workflow node that subscribes and emits matching envelopes.
- Added MQTT topic-filter matching tests, connection/trigger component tests, and runtime factory tests for shared connection startup.
- Updated the desktop definition composer so the alpha workspace produces `resources.broker` plus `workflows.inspectPayloads.trigger`.
- Added component-specific diagram node widgets for connection, trigger, payload inspector, metrics, and fallback nodes.
- Kept diagram node state stable during live updates and moved diagram-specific styles into `FlowDesigner.razor.css`.
- Made the flow designer fill the available workspace container height so the diagram canvas is usable inside the resizable layout.
- Updated developer docs and the user documentation site for the connection/trigger model.

## Current Next Step

Harden the alpha desktop workspace by exercising it against Mosquitto, then add publish support that reuses the shared MQTT connection resource.

## 2026-05-14

- Introduced phase-based lifecycle management for the pipeline runtime:
  - Added `int Phase` (default `0`) to `NodeDefinition` and `RuntimeNode`.
  - Builder stamps each `RuntimeNode.Phase` from `NodeDefinition.Phase` after the factory runs; factory code is unaffected.
  - `ApplicationRuntime.StartAsync` and `Workflow.StartAsync` now iterate all nodes grouped by `Phase` ascending, awaiting each group before the next.
  - Resources and workflow nodes are unified in the startup loop so a workflow node at a lower phase starts before a resource at a higher phase.
  - Startup ordering is entirely a runtime concern; components do not declare their own phase.
- Removed `IFlowStartable`:
  - `StartAsync(CancellationToken = default) => Task.CompletedTask` moved to `IFlowNode` as a default interface method.
  - `MqttConnectionComponent`, `MqttTriggerComponent`, and `ReplaySourceComponent` dropped `IFlowStartable` from their declarations.
- Deleted `IPreExecutionProcessor` and `IPostExecutionProcessor` — marker-interface-on-component approach was tried and rejected in favour of config-driven phase ordering.
- Committed as `850bc8b` on branch `feature/pipeline-runtime-model`.

- Added workflow and application state tracking:
  - `WorkflowState` enum: `Idle, Starting, Running, Stopping, Stopped, Faulted`.
  - `ApplicationState` enum: same values.
  - `WorkflowStateChanged` and `ApplicationStateChanged` sealed records carrying previous/current state, optional exception, and timestamp.
  - `Workflow.StateChanges` and `ApplicationRuntime.StateChanges` exposed as `ISourceBlock<T>` backed by `BroadcastBlock<T>` — consistent with `IFlowNode.Errors`; consumers use `LinkTo` to subscribe.
  - `Workflow.State` and `ApplicationRuntime.State` properties expose the current state.
  - Transitions driven by `StartAsync` (Idle→Starting→Running), `Complete` (→Stopping), `Fault` (→Faulted); a `Completion` continuation fires Stopped or Faulted when the dataflow graph drains naturally.
  - State transitions are lock-guarded to prevent races between concurrent callers.
  - Committed as `15ea5b5` on branch `feature/pipeline-runtime-model`.

- Added tests for workflow and application state tracking (`WorkflowStateTests`, `ApplicationRuntimeStateTests`):
  - 17 new tests covering all state transitions (Idle→Starting→Running→Stopping→Stopped, Faulted).
  - Verifies `ISourceBlock<T>` state streams deliver correct previous/current values and exceptions.
  - Verifies per-workflow state is updated when the runtime drives phase-based startup.
  - Fixed `ApplicationRuntime.StartAsync` to call `workflow.BeginStartup()` / `workflow.CompleteStartup()` (internal helpers on `Workflow`) so workflow state is advanced correctly even when the runtime bypasses `Workflow.StartAsync()` for phase ordering.
  - 102/102 tests passing.
  - Committed as `9da8eb4` on branch `feature/pipeline-runtime-model`.

- Migrated all test projects from FluentAssertions to Shouldly:
  - FluentAssertions v8 moved to a commercial license; replaced with Shouldly (MIT) across all 7 test projects.
  - Updated all test `.csproj` files: removed `FluentAssertions` package reference, added `Shouldly 4.2.1`.
  - Rewrote assertions in 33 test files to use Shouldly APIs (`ShouldBe`, `ShouldBeTrue`, `ShouldContain`, `ShouldHaveSingleItem`, `Should.ThrowAsync<T>`, etc.).
  - Three Shouldly API differences required specific fixes:
    - `ShouldContainKey`/`ShouldNotContainKey` do not overload `IReadOnlyDictionary<K,V>` → replaced with `ContainsKey().ShouldBeTrue()/ShouldBeFalse()`.
    - `Search(null).Count` → `Search(null).Count()` (Count is a LINQ method, not a property).
    - `ShouldHaveSingleItem(predicate)` takes no args → replaced with `ShouldContain(predicate)`.
  - 192/192 tests passing across all projects.

- Fixed two classes of flaky tests:
  - **`Complete_TransitionsToStopping` race** (`WorkflowStateTests` and `ApplicationRuntimeStateTests`): `TestNode.Complete()` fires synchronously, allowing the Stopped continuation to race past the Stopping state assertion. Fixed by subscribing to the `StateChanges` stream before calling `Complete()` and draining until the Stopping event arrives, which preserves event order regardless of scheduling.
  - **`MqttConditionRouterComponent.Fault_PublishesErrorAndFaultsCompletion` race**: Two issues:
    1. `_errors` was a `BroadcastBlock<FlowError>` which offers messages asynchronously; `ExecuteSynchronously` continuation could call `_errors.Complete()` before delivery completed. Changed to `BufferBlock<FlowError>` which guarantees all queued messages drain before completion.
    2. `component.Completion` was `Task.WhenAll(_block, _whenTrue, _whenFalse)`. When `_block` faults it propagates to `_whenTrue` and `_whenFalse` via `PropagateCompletion`, so WhenAll had three faulted tasks with multiple inner exceptions — `await` throws `AggregateException` instead of the unwrapped `InvalidOperationException`. Fixed by changing `Completion` to `_block.Completion` only; output port completions propagate naturally through linked targets.
  - 192/192 tests passing, stable across 3 consecutive full-suite runs under parallel test load.

## 2026-05-15

- Moved the current in-progress component-boundary work onto `feature/components-boundary` from the latest `origin/main`.
- Introduced `FluxMq.Components` as the home for concrete flow components, replay orchestration, LiteDB storage, and component-level tests.
- Kept `FluxMq.Pipeline` focused on definitions, runtime graph construction, typed runtime ports, lifecycle state, and flow error primitives.
- Moved runtime component registration into `FluxMq.App` so the application host remains the composition boundary for production components.
- Updated `FluxMq.UI` to consume concrete components and storage services from `FluxMq.Components`.
- Added `tests/FluxMq.Components.Tests` and moved concrete component, replay, and storage tests there.
- Updated developer docs and memory notes to describe the new runtime/component boundary.
- Recorded the next refactoring direction: live broker data and stored/offline data must enter the runtime through the same source model.
- Agreed that runtime/projection/dashboard update contracts should be Dataflow-native, with channels kept as internal producer details and `EventHandler` avoided as an architectural contract.
- Implemented the first source-agnostic runtime slice:
  - Added explicit source node types for live MQTT, stored-session, and generated source modes.
  - Added live, stored-session, and generated source components that expose `MqttEnvelope` through Dataflow output ports and `FlowError` through error ports.
  - Added streaming stored-session reads through `IMessageRepository.ReadBySessionAsync` and `ReadEnvelopesBySessionAsync`.
  - Added per-session stored-message sequence numbers and sequence-aware ordering for deterministic replay when timestamps match.
  - Updated the desktop definition composer so the default inspect-payloads flow links inspector and metrics nodes to `traffic.Output`.
  - Added a Traffic Source diagram node widget and catalog entry.
  - Moved live and stored workspace message updates behind `WorkspaceMessageProjection`, keeping durable state plus Dataflow input/update surfaces.
  - Updated `FlowApplicationHost` so hosts can pass the message repository required by stored-session sources.
  - Verified the solution with `dotnet build FluxMq.sln --no-restore` and `dotnet test FluxMq.sln --no-build`; 204 tests passing.

## 2026-05-22

- Created a feature branch from the dirty UI refactor state so the redesign work is isolated from `feature/live-inspector-and-json-viewer`.
- Treated all existing uncommitted UI changes as part of the redesign refactor.
- Used `memory/fluxmq-redesign.html` as the visual target for the desktop shell: compact dark operational UI, 48px top bar, 52px rail, left explorer, canvas, right inspector, and 28px status bar.
- Replaced the MudBlazor drawer/appbar workspace frame in `MainLayout` with an explicit CSS grid shell:
  - branded top bar with breadcrumb, file actions, theme toggle, inspector toggle, and run state pill
  - fixed icon rail with explorer/components/sessions tabs
  - compact left panel with section header
  - full-height canvas area
  - right inspector panel
  - bottom status bar for connection state, file path, and message count
- Reworked global design tokens in `wwwroot/app.css` around the redesign palette (`#0a0d12`, `#11151c`, `#2dd4bf`, restrained blue/yellow/success/error accents), compact radii, shadows, scrollbars, table density, MudBlazor tabs, buttons, inputs, chips, and TreeView internals.
- Rebuilt `AppTreePanel` as a compact custom explorer surface instead of a MudTreeView/expansion-panel hybrid:
  - app cards with active state, connection and pipeline sections
  - reserved hover action space so connection buttons no longer depend on overflow hacks
  - stable row heights and tighter section labels
- Tuned `WorkspacePage`, `LiveInspectorPanel`, `FlowDesigner`, `ComponentCatalogPanel`, `TopicTree`, `PayloadInspectorPanel`, and `AppJsonPanel` CSS to match the redesign spacing, border, surface, and elevation system.
- Updated diagram colors and navigator chrome to match the redesigned teal/yellow operational palette.
- Fixed the Light/Dark/System theme regression introduced by the first redesign pass:
  - `MainLayout` now adds `flux-theme-light` or `flux-theme-dark` to the shell based on `AppThemeService.IsDarkMode`.
  - `--flux-*` design tokens are defined under those shell theme classes instead of globally on `:root`.
  - Heavy MudBlazor overrides are scoped under `.flux-shell` so dialogs/popovers can keep following MudBlazor theme variables.
- Corrected the right inspector spacing after visual review:
  - Removed the extra `Runtime / Live Inspector` header so the panel starts with tabs like `fluxmq-redesign.html`.
  - Reduced MudTabs minimum width/height and added component-local tab overrides for compact, mockup-like tab spacing.
  - Changed the publish pane to 14px panel padding, 10px field gaps, tighter input text spacing, and a fixed action row.
- Reworked the right inspector again after the MudTabs approach made spacing worse:
  - Replaced MudTabs with a purpose-built custom tab strip matching the mockup's right panel rhythm.
  - Rebuilt the Publish surface with custom topic/payload controls, dropdown-style `QoS`, `Retain` toggle, and smaller 28px publish button.
  - Added the missing Publish-tab lower sections from `fluxmq-redesign.html`: live topic activity rows, recording label, active topic highlight, topic rates, and a compact last-payload footer.
  - Fixed the last-payload age label so it refreshes every second and uses lowercase units such as `12s ago`.
  - Extended `LiveMqttWorkspaceService.PublishAsync` so QoS and retain are real publish options, not decorative UI.
- Verified:
  - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore -p:UseAppHost=false` passes with 0 errors and 0 warnings.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-build` passes with 26 tests.

## 2026-05-16

- UI redesign of `FluxMq.UI` — `AppTreePanel`, `NewAppDialog`, `MainLayout`, `WorkspacePage`:
  - Rewrote `AppTreePanel` as a data-driven `MudTreeView` using `Items` + `ItemTemplate` + `BodyContent` (not `Content`, which replaces the entire item including the expand toggle).
  - Used sealed records as discriminated union node types (`AppNode`, `ConnGroupNode`, `ConnNode`, `NoConnNode`, `PipeGroupNode`, `PipeNode`, `NodeItem`) with `ITreeItemData<object>` in all helpers.
  - `_expanded` `HashSet<string>` tracks expand state across rebuilds; new apps auto-expand to their connection and pipeline groups.
  - Hover-only action buttons via `.tree-row-actions { opacity:0 }` / `.tree-row:hover .tree-row-actions { opacity:1 }` in isolated `AppTreePanel.razor.css`.
  - Added full MQTT connection fields to `NewAppDialog` (Name, Host, Port, Client ID, Subscription, TLS, Username, Password); `NewAppResult` record updated accordingly; `MainLayout.NewProject` and `WorkspacePage.NewAppAsync` both wire the connection.
  - Increased left drawer width to 400px and removed `pe-4` padding class.
  - Added global MudBlazor treeview overrides in `app.css`: `width:100%`, `.mud-treeview-group padding-left`, `.mud-treeview-item-arrow width`, `.mud-treeview-item-icon width`, `.mud-treeview-item-content` height/padding, `.mud-treeview-item-label flex:1`.

- **Key CSS lesson confirmed**: `::deep` in component-isolated `.razor.css` does not reach MudBlazor internal elements — the Blazor CSS scope attribute is only applied to HTML elements the component authors directly, not to child component internals. All MudBlazor class overrides must go in `app.css`.

- **MudBlazor treeview class names confirmed** from `MudBlazor.min.css`:
  - Children container is `.mud-treeview-group` (a `<ul>`), NOT `.mud-treeview-item-children` (does not exist).
  - Indentation is browser-default `padding-left` on `<ul>` — MudBlazor resets `margin` but not `padding`.

- **Paused**: action button clipping on hover not fully resolved. The buttons (`conn-actions`) are not fully visible despite `overflow:visible` on `.mud-treeview-item-label`. Root cause not confirmed — user reverted `AppTreePanel` to last stable state to resume later.

## 2026-05-22 - UI component/catalog cleanup continuation

- Expanded the visual component catalog to expose additional source, mapper, actor, observer, and connection-state components with visible ports.
- Added a generic diagram node widget so catalog entries without specialized editors can still render on the canvas.
- Continued moving new UI/default naming toward explicit live, stored-session, replay, and generated source concepts.
- Registered explicit runtime source node types for live MQTT, stored session, and generated traffic as aliases over the existing source component creation path.
- Updated diagram link creation so newly drawn links use the same teal/arrowed styling as links loaded from definitions, with CSS fallback styling for rendered SVG links.
- Simplified the publish QoS native select styling and removed the extra visual arrow treatment from the markup to avoid the odd white dropdown appearance on Windows/WebView.
- Removed the extra active/accent panel styling from the app card in the left explorer so the selected app remains visually neutral.
- Verification so far: targeted `git diff --check` on the touched UI/runtime files passes. Compile/test verification is blocked in this WSL session because Linux `dotnet` is unavailable and Windows `.exe` interop returns `cannot execute binary file: Exec format error`.

## 2026-05-22 - Standalone component refactor kickoff

- Read the source-agnostic runtime memory and confirmed the next architectural cleanup: components should be standalone actors, mappers, routers, or observers with clear typed ports.
- Added `memory/07-standalone-component-refactor-plan.md`.
- Changed MQTT publishing from raw `MqttEnvelope` input to explicit `MqttPublishRequest` input.
- Changed MQTT recording from raw `MqttEnvelope` input with constructor-bound `SessionId` to explicit `MqttRecordingRequest` input carrying both `SessionId` and envelope.
- Added `MqttPublishRequestMapperComponent` and `MqttRecordingRequestMapperComponent` as runtime adapters, then corrected the product surface so flows read as `source -> filter/router -> dynamic mapper -> actor`.
- Added runtime node types, app factory registrations, and actor catalog entries. Request-specific mapper node types are compatibility/internal details, not user-facing catalog components.
- Verified:
  - `dotnet test tests\FluxMq.Components.Tests\FluxMq.Components.Tests.csproj --no-restore -p:UseSharedCompilation=false` passes with 87 tests.
  - `dotnet build src\FluxMq.App\FluxMq.App.csproj --no-restore -p:UseSharedCompilation=false` passes with 0 errors and 0 warnings.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false` passes: 218 tests.

## 2026-05-22 - Dynamic mapper and ops vision correction

- Removed the stale `rtk` shell-command requirement; normal shell commands should be used directly.
- Added `memory/08-dynamic-mapping-and-ops-vision.md`.
- Corrected the component plan: dynamic mappers are a core FluxMQ capability, not incidental glue.
- Recorded Dynamic Expresso for C#-style filters/mappers and JSONata for JSON query/mapping as explicit runtime directions.
- Recorded that user-facing side-effect components should use actor names such as MQTT Publisher, File Writer, Recorder, HTTP Sender, and Email Sender instead of generic "sink" names.
- Recorded the two-era product direction:
  - developer ELT/integration flows first
  - ops/QA assertions, counters, summaries, schema validation, message-rate measurements, and response expectations later

## 2026-05-22 - Dynamic mapper runtime slice

- Added runtime-level mapping abstractions in `FluxMq.Pipeline.Mapping`:
  - `IFlowMapper<TInput,TOutput>`
  - `IFlowPredicate<TInput>`
  - `IFlowExpressionEngine`
  - `FlowExpressionContext`
- Added `DynamicExpresso.Core 2.19.3` to `FluxMq.Components` and implemented `DynamicExpressoFlowExpressionEngine`.
- Added `MqttEnvelopeExpressionContextFactory` so expressions get stable variables such as `envelope`, `topic`, `payload`, `payloadText`, `qos`, `retain`, and `receivedAt`.
- Changed `MessageFilterComponent` to accept an `IFlowPredicate<MqttEnvelope>` while preserving the delegate constructor.
- Added `MqttEnvelopeExpressionPredicate` and wired `mqtt.message-filter` factory config `expression` to Dynamic Expresso.
- Added `MqttPublishRequestExpressionMapper` and `MqttPublishRequestMapDefinition`; mapper configuration can now map topic, payload, QoS, and retain through configurable expressions.
- Added `MqttPublisherComponent` and `mqtt.publisher` as the MQTT publish actor node type.
- Added focused tests for:
  - Dynamic Expresso filtering with `qos >= 1`.
  - Dynamic MQTT publish request mapping.
  - Runtime flow: generated MQTT envelopes -> expression filter -> dynamic mapper -> MQTT publisher using `broker2`.
- Verified targeted tests:
  - `dotnet test tests\FluxMq.Components.Tests\FluxMq.Components.Tests.csproj --no-restore -p:UseSharedCompilation=false` passes with 89 tests.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore -p:UseSharedCompilation=false` passes with 19 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false` passes with 221 tests.

## 2026-05-22 - Actor naming and File Writer runtime slice

- Removed the old MQTT publish and recording compatibility node aliases; app definitions should now use `mqtt.publisher` and `mqtt.recorder`.
- Removed the old generic source compatibility node alias; app definitions should now use `mqtt.trigger`, `session.source`, `replay.source`, or `generated.source`.
- Renamed the publish namespace to `FluxMq.Components.MqttPublisher`.
- Renamed the metrics observer to `MqttMetricsComponent` / `mqtt.metrics`.
- Added `FileWriteRequest`, Dynamic Expresso-backed file-write request mapping, and `FileWriterComponent`.
- Registered file write request mapping in the runtime factory registry and exposed `file.writer` as the user-facing actor.
- Updated docs and memory to describe flows as `source -> filter/router -> dynamic mapper -> actor/observer`.

## 2026-05-23 - Dynamic mapper product-surface correction

- Corrected the mapper/request-model misunderstanding:
  - Request models such as `MqttPublishRequest` and `FileWriteRequest` are actor input contracts.
  - They are not separate user-facing UI components.
  - The visible graph component is now `flow.mapper`.
- Added `flow.mapper` as the user-facing mapper node with explicit `inputType`, `outputType`, `engine`, and mapper configuration.
- Kept request-specific mapper node types as hidden compatibility/runtime aliases for old definitions.
- Updated the desktop component catalog so it exposes Dynamic Mapper plus actors, not `Publish Request`, `Recording Request`, or `File Write Request` pseudo-components.
- Stopped the UI composer from wiring actors directly to envelope sources. If a user adds `mqtt.publisher`, it only links to an existing mapper output; otherwise the missing type bridge is explicit.
- Added a mapper node editor for engine, input type, output type, and field expressions.
- Added `jsonata` as a mapper expression engine alongside `dynamic-expresso`.
- Verified:
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -m:1` passes with 225 tests after the actor/observer rename, explicit source cleanup, and File Writer slice.

## 2026-05-23 - Feature backlog and living development plan

- Mirrored the OpenGarden memory workflow for FluxMQ:
  - added `memory/09-feature-list.md` as the followable feature backlog with feature IDs, priorities, UI/data/acceptance notes, MVP cut line, and suggested implementation order.
  - added `memory/10-development-plan.md` as the active step-by-step development plan with operating rules, current target, phased status, quality gates, and progress log.
- Set the current target to `F-014 - JSON Schema Validator`, following the completed Dynamic Mapper and actor cleanup.
- Updated `memory/00-index.md` so future work can start from the new planning docs.

## 2026-05-23 - OPC Router UI inspiration and JSONata mapper workbench

- Added `memory/11-opc-router-ui-inspiration.md` as a reference note for industrial ETL/integration UX patterns.
- Recorded useful OPC Router-inspired concepts for FluxMQ:
  - plug-ins as component/module packs
  - transfer objects as typed nodes with visible input/output ports
  - triggers as explicit workflow starters
  - JSON tools that expose input structures, schemas, and selectable fields
- Added `F-015 - JSONata Mapper Workbench UI` to the feature list.
- Updated the active development target to `F-015`, so the next mapper UI slice should provide input tree, output request shape, per-field expressions, preview, and validation instead of only a raw text editor.
- Added the first JSONata mapper workbench implementation slice:
  - `MqttEnvelopeExpressionContextFactory` now exposes parsed `payloadJson` when payload text is valid JSON.
  - Added `DynamicMapperWorkbenchPreview` to derive input variables, output request fields, engine-aware examples, and preview results through the same mapper engines used at runtime.
  - Reworked `DynamicMapperNodeWidget` into a three-pane editor: selected/live/session/sample input, expression editor, output shape, and preview/errors.
  - Added tests for payload JSON context variables, JSONata publish preview, Dynamic Expresso file-write preview, engine-aware field examples, and recording-request validation.
- Corrected the mapper model to use one `expression` that returns the whole command/request object instead of separate expressions per property. JSONata expressions return JSON objects; Dynamic Expresso expressions can return typed request objects.
- Added BlazorMonaco 3.4.0 and replaced the mapper expression textarea with a Monaco `StandaloneCodeEditor`.
- Added FluxMQ light/dark Monaco themes and a JSONata editor language definition for the mapper workbench.
- Reworked mapper samples/results toward the JSONata Exerciser model: editable Monaco JSON input on the left, expression in the middle, and read-only live JSON result on the right.
- Fixed mapper live preview by preventing parent redraws from reloading the draft editor state and explicitly rendering after preview recomputation.
- Updated the active development target to `F-014 - JSON Schema Validator` as the next Phase 4 slice.

## 2026-05-23 - Mapper output contracts and JSON Schema validator slice

- Removed the editable mapper input type field from the node editor; the current mapper UI is explicitly `MqttEnvelope` input.
- Reworked mapper output selection into a result contract model:
  - `typed` for known actor request contracts.
  - `any` for unvalidated arbitrary expression output preview.
  - `json-schema-file` for schema-backed output contracts.
- Kept the runtime typed mapper path intact because `flow.mapper` still uses `outputType` to build concrete actor request ports.
- Added JsonSchema.Net 9.2.1 and implemented `JsonSchemaValidatorComponent` with:
  - `Input: MqttEnvelope`
  - `Output: JsonSchemaValidationResult`
  - `Errors: FlowError`
  - inline schema JSON and schema file path runtime configuration.
- Registered `json.schema-validator` in runtime factories and the UI catalog.
- Added a focused JSON Schema Validator node editor with Monaco JSON schema editing, schema id, and schema file mode.
- Updated docs and memory to record that validation is a reusable runtime/component capability, not mapper-only UI behavior.
- Verified:
  - `dotnet test tests\FluxMq.Components.Tests\FluxMq.Components.Tests.csproj --no-restore` passes with 96 tests.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore` passes with 21 tests.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore` passes with 43 tests.
  - `dotnet test FluxMq.sln --no-restore -m:1` passes with 247 tests.

## 2026-05-23 - Actor node editor hardening slice

- Started the `F-022` actor editor hardening slice.
- Added typed designer models/widgets for MQTT Publisher, MQTT Recorder, and File Writer so they no longer use the generic node body/editor.
- MQTT Publisher now exposes broker resource selection and input buffer settings; publish topic, payload, QoS, and retain remain explicit `MqttPublishRequest` fields.
- MQTT Publisher keeps broker selection as node configuration and does not expose a `Connection` canvas port.
- MQTT Recorder and File Writer now show their command input fields and expose only input-buffer configuration on the actor node.
- Updated actor catalog add behavior to write default actor configuration.
- Updated the runtime recorder factory to honor configured buffer capacity.

## 2026-05-23 - Source node editor slice

- Added typed designer models/widgets for Generated MQTT Source and Replay Source.
- Removed the separate live broker source path; live broker input remains `mqtt.connection` plus `mqtt.trigger`.
- Generated MQTT Source now edits a fixed message list with topic, payload, QoS, retain, optional timestamp, and output buffer.
- Replay Source now selects a recorded session, playback speed, and output buffer.
- Registered `replay.source` in runtime factories and added runtime coverage for replaying stored messages into metrics.
- Updated the composer so generated and replay sources get default configuration when added from the catalog; live flows continue to use the broker resource plus trigger.
- Verified:
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore -p:UseSharedCompilation=false` passes with 23 tests.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false` passes with 61 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -m:1` passes with 267 tests.

## 2026-05-23 - Node diagnostics slice

- Added workflow/node/port scope to definition validation errors and runtime build errors.
- Added node startup failure wrapping so run-start failures keep the failing node address.
- Extended workspace diagnostics with optional workflow, node, and port metadata.
- Diagram nodes now show node-scoped diagnostics through status border styling and a header tooltip.
- Build validation diagnostics no longer duplicate the same validation failure as both definition and runtime-build diagnostics in the workspace list.
- Added visible top-bar actions for the active app: `Validate`, `Run`, and `Stop`.
- Added an app runtime state pill so app validation/start state is separate from live broker connection state.
- Verified:
  - `dotnet test tests\FluxMq.Pipeline.Tests\FluxMq.Pipeline.Tests.csproj --no-restore -p:UseSharedCompilation=false` passes with 45 tests.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore -p:UseSharedCompilation=false` passes with 22 tests.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false` passes with 61 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -m:1` passes with 266 tests.
  - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore -p:UseSharedCompilation=false` passes with 0 warnings.

## 2026-05-23 - Runtime logger slice

- Added `flow.logger` as a standalone observer component:
  - `Input: MqttEnvelope`
  - `FlowErrors: FlowError`
  - `Entries: FlowLogEntry`
  - `Errors: FlowError`
- The logger keeps bounded recent history so the UI can collect entries even when a generated source completes quickly.
- Registered the logger in the runtime factory registry, UI catalog, composer defaults, and generic node icon mapping.
- Added workspace log history beside existing diagnostics:
  - validation, run, and stop diagnostics append to history
  - runtime logger entries append while an app is running
  - history is bounded and can be cleared per project
- Added a right-inspector `Logs` tab with filtering, severity styling, scope, and context.
- Improved the `Logs` tab presentation so entries show an explicit level label, severity-specific icons, row treatment, source/code, scope, and context instead of reading like raw strings.
- Fixed scoped node editor saves when multiple pipelines contain the same node name:
  - node configuration updates now target the active workflow first
  - broker-backed node saves now update the selected workflow node instead of the first matching node name
  - this prevents `pip2.trigger` from visually showing a broker while its JSON still lacks `configuration.connection`
- Fixed MQTT runtime startup ordering:
  - `mqtt.connection` now awaits the broker connection before reporting itself started
  - `mqtt.trigger` no longer races ahead and tries to subscribe while the underlying MQTT client is still disconnected
  - connection failures now surface at the broker resource instead of as misleading trigger subscription failures
- Tightened multi-broker desktop behavior:
  - `Run` now ensures app broker resources are registered and connected in the desktop live workspace before starting runtime execution
  - live workspace connections now retain the app broker resource name, such as `broker1` or `broker2`
  - the right-side Publish panel can target a specific connected broker instead of always using the first connected session
  - MQTT Trigger, Connection State Trigger, and MQTT Publisher editors now select broker resources by app resource name with endpoint labels
  - desktop live sessions use a separate workspace client id so the live tools do not collide with runtime client ids
- Fixed app-run broker ownership:
  - live broker sessions started automatically by `Run` are tracked as app-started sessions
  - `Stop` now disconnects those app-started sessions while leaving manually connected broker sessions alone
  - workspace stop has a bounded timeout so a stalled runtime stop cannot keep the top toolbar busy indefinitely
- Improved runtime controls and publisher defaults:
  - the top-bar `Stop` action now shows a compact spinner beside the `Stop` label while the async stop operation is in progress
  - the right-side Publish panel selects the first broker resource from the active app by default
  - newly added MQTT Publisher nodes default their broker configuration to the first app broker resource instead of the generic fallback
  - the MQTT Publisher editor resolves that same first app broker when opening a node that still has the generic fallback value
- Cleaned up the Live MQTT Trigger node contract:
  - the trigger no longer exposes a canvas `Connection` input port
  - broker selection remains a configuration/editor concern through the broker resource dropdown
- Verified:
  - `dotnet test tests\FluxMq.Components.Tests\FluxMq.Components.Tests.csproj --no-restore -p:UseSharedCompilation=false` passes with 100 tests.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore -p:UseSharedCompilation=false` passes with 23 tests.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutDir="$env:TEMP\FluxMqVerifyUiTests\"` passes with 75 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -m:1` passes with 285 tests.
  - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore -p:UseSharedCompilation=false` passes with 0 warnings.

## 2026-05-23 - Default runtime log collection slice

- Made workspace `Logs` collect runtime component errors by default:
  - every runtime output port carrying `FlowError` is observed by the workspace while the app runs
  - errors appear in the right-inspector `Logs` tab with workflow, node, and port scope
  - no hidden logger node or generated `FlowErrors` links are written into app definitions
  - `Flow Logger` remains an explicit observer component for flows that need log entries as stream data
- Hardened runtime multi-source input behavior:
  - multiple links into the same input no longer propagate completion independently
  - the input completes after every linked source completes, so a logger can safely collect several error streams
- Verified:
  - `dotnet test tests\FluxMq.Pipeline.Tests\FluxMq.Pipeline.Tests.csproj --no-restore -p:UseSharedCompilation=false` passes with 46 tests.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore -p:UseSharedCompilation=false` passes with 23 tests.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false` passes with 78 tests.

## 2026-05-24 - Input-driven trigger activity slice

- Added runtime projection state for MQTT trigger activity in the workspace service.
- `mqtt.trigger` diagram activity now counts envelopes emitted by that trigger's own `Output` stream instead of using broker-wide live monitor message counts.
- Non-matching broker messages do not increment the trigger card activity because the projection attaches after the trigger subscription filter.
- Broker-wide live message counts remain in the Live Inspector panel.
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false` passes with 99 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -m:1` passes with 327 tests.

## 2026-05-24 - Metrics message-rate slice

- Extended `MqttMetricsSnapshot` with current and average message-rate fields:
  - total messages in the current rolling window
  - current messages per second
  - average messages per second since the metrics component started
  - per-topic message rates
- Added `rateWindowSeconds` runtime configuration for `mqtt.metrics`, defaulting to 60 seconds.
- Metrics rates are based on observer input time, so they measure the stream feeding the metrics component rather than broker-wide monitor traffic.
- Current snapshots prune the rolling window before reporting, so current rate decays when traffic stops while average rate remains since-start.
- Updated the metrics node face and activity text to show current and average rates.
- Restored payload size as a default metrics card and made visible metric cards configurable per node.
- Added metrics edit settings for input buffer, current-rate rolling window seconds, displayed metric cards, and card column count.
- The metrics card selector no longer caps selected cards at four; the node grid calculates rows from selected cards and columns.
- Recorded a future refactor note: separate generic stream metrics from MQTT-specific topic metrics so mapper outputs and future protocol streams can use metrics observers cleanly.
- Verified:
  - `dotnet test tests\FluxMq.Components.Tests\FluxMq.Components.Tests.csproj --no-restore -p:UseSharedCompilation=false` passes with 105 tests.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false` passes with 101 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -m:1` passes with 331 tests.

## 2026-05-24 - MQTT assertion slice

- Started the assertions/expectations feature with a standalone `flow.assertion` component.
- The assertion component:
  - accepts a configured input type
  - evaluates a configurable expression such as `qos >= 1`
  - emits `FlowAssertionResult` on `Result`
  - routes the original value to `Passed` or `Failed` using the same configured input type
  - emits pass/fail log entries on `Entries`
  - emits expression/runtime failures on `Errors`
- Added the assertion node to the runtime factory registry, UI catalog, definition composer, node model factory, and node widget registry.
- Added a node editor for assertion name, input type, expression, failure message, and input buffer.
- Recorded a future object-stream architecture note: dynamic mappers mean the runtime should eventually use object streams underneath with typed port contracts as schema/validation metadata.
- Recorded a future logging architecture note: use standard `Microsoft.Extensions.Logging` as the main component logging path and bridge it into the workspace Logs view through a provider/sink, while retaining `FlowLogEntry` streams for graph-visible log data.
- Verified:
  - `dotnet test tests\FluxMq.Components.Tests\FluxMq.Components.Tests.csproj --no-restore -p:UseSharedCompilation=false` passes with 109 tests.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore -p:UseSharedCompilation=false` passes with 29 tests.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false` passes with 103 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -m:1` passes with 337 tests.

## 2026-05-24 - Dashboard layout designer slice

- Moved app structure navigation into top-bar menus for connections, pipelines, dashboards, and tests so the side panel can stay focused on the active artifact's tools.
- Added a dashboard layout editor surface for dashboard tabs:
  - editable WPF-like column and row tracks such as `320`, `25%`, `*`, and `2*`
  - grid preview using the dashboard definition's rows, columns, cells, and spans
  - cell add/remove commands that round-trip through the existing `dashboards` JSON model
- Enriched the dashboard grid interaction after reviewing the OpenGarden surface editor:
  - row/column layout picker
  - row and column add/remove commands
  - selectable virtual and explicit cells
  - merge, split, split-rows, split-columns, and 2 x 2 subdivision commands
  - topology updates that split track sizes and preserve neighboring cell spans
- Replaced raw row/column track text fields with per-track editing from the row and column handles:
  - each track can choose fixed, percent, or star sizing
  - each row or column owns its padding value
  - padding is stored in the dashboard JSON and applied visually to cells
  - the dashboard designer fills the available tab body instead of rendering as a fixed preview
- Added focused UI tests for dashboard layout reading, grid track updates, invalid track handling, cell add/remove behavior, grid resize, merge/split/subdivision, and workspace-service diagnostics.
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -m:1 --filter "FullyQualifiedName~FlowDefinitionComposerTests|FullyQualifiedName~FlowWorkspaceServiceTests"` passes with 81 focused tests.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -m:1` passes with 128 tests.
  - `dotnet test tests\FluxMq.Pipeline.Tests\FluxMq.Pipeline.Tests.csproj --no-restore -p:UseSharedCompilation=false -m:1 --filter FullyQualifiedName~Definitions` passes with 24 tests.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore -p:UseSharedCompilation=false -m:1` passes with 30 tests.
  - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore -p:UseSharedCompilation=false` passes with 0 warnings.
  - `dotnet run --project src\FluxMq.Cli\FluxMq.Cli.csproj --no-restore -- validate --config C:\Users\meisa\OneDrive\Documents\FluxMQ\app1.json` reports the app is valid.
  - `dotnet run --project src\FluxMq.Cli\FluxMq.Cli.csproj --no-restore -- run --config C:\Users\meisa\OneDrive\Documents\FluxMQ\app1.json --duration-ms 10` starts and stops successfully.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -m:1` passes with 381 tests. The solution pass still prints existing WinAppSDK PRI qualifier warnings during UI test build.
- Hardened definition collection properties so empty/null dashboard widgets, dashboard cells, scenario steps, node configuration, and other definition collections stay usable after configuration loading.
- Added loader and JSON regression coverage for empty dashboard/test sections to prevent app-open/run crashes when dashboards or tests are present but have no widgets/cells/steps yet.
- Simplified the dashboard layout toolbar around the cell-slot model:
  - removed visible add/remove row, column, and cell commands
  - kept Ctrl/Shift multi-select and merge for creating larger slots
  - replaced separate split commands with one split grid picker for the selected cell
  - added row/column guide lines over the dashboard surface so track boundaries remain visible through merged or empty areas
- Refined the dashboard designer visual model:
  - the dashboard now renders as a single framed board inside a grid-backed surface
  - row and column track controls render as centered pills outside the board
  - cells show layout coordinates instead of internal cell keys
  - merged cells cover their full slot without guide lines cutting through the merged area
- Added dashboard tab modes:
  - Edit mode keeps the layout designer controls and selectable cells
  - Live mode renders the same dashboard layout as a read-only board
  - Live mode already respects row/column tracks, spans, padding, and assigned widget slots, ready for real widget renderers
  - the dashboard widget panel is visible only in Edit mode; Live mode uses the full tab width for viewing
- Verified:
  - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false` passes with 0 warnings.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1` passes with 128 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1` passes with 381 tests. The solution pass still prints existing WinAppSDK PRI qualifier warnings during UI test build.

## 2026-05-24 - Dashboard live widget slice

- Started the monitoring/dashboard plane on top of runtime events instead of pipeline links:
  - dashboard cells still only reference widget keys
  - dashboard widgets live under `dashboards.widgets`
  - Live mode reads the app runtime event stream collected from `ApplicationRuntime.Events`
- Added a dashboard widget catalog with first-class monitoring widgets:
  - `event.counter` counts runtime events with optional `eventType`, `topicStartsWith`, and `status` filters
  - `event.latest` renders the latest matching event with topic, status, payload size, time, and payload preview
- The dashboard component panel now shows dashboard widgets when a dashboard is in Edit mode.
- Adding a widget places it in the selected dashboard cell, or the next available cell when nothing is selected.
- Runtime events are kept as bounded workspace state and cleared with runtime projection state when the app definition changes or a new run starts.
- Dashboard widget palette items now support pointer drag/drop onto dashboard cells, while click-add still places a widget in the selected or next available cell.
- The shared palette drag state now carries a target artifact kind, so pipeline component drops and dashboard widget drops do not cross wires.
- Dashboard Live mode now uses the full tab body with the grid flush to the container and removes redundant outer grid/cell borders around widget cards.
- Dashboard Live mode now keeps a small overall padding and uses neutral app border tones for widget cards and empty cells instead of type-colored outlines.
- Verified:
  - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false` passes; it still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 --filter "FullyQualifiedName~DragStateServiceTests|FullyQualifiedName~FlowDefinitionComposerTests|FullyQualifiedName~FlowWorkspaceServiceTests"` passes with 86 focused tests.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1` passes with 133 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1` passes with 386 tests.

## 2026-05-25 - Dashboard widget settings slice

- Added a dashboard widget settings dialog for assigned dashboard cells:
  - title
  - event type
  - topic prefix
  - status
- Dashboard edit cells now show the widget display title and a compact settings action instead of only the raw widget key.
- The dashboard JSON composer can update a named widget's configuration without changing grid cells, spans, or layout tracks.
- The workspace service now exposes dashboard widget configuration updates as a dashboard designer command.
- New dashboard widgets now include an explicit empty `status` filter so the persisted shape matches the runtime filtering model.
- Added regression coverage for widget configuration round-tripping and workspace-service updates.
- Refined event filters so dashboard widget settings are event-type aware:
  - MQTT, schema validation, and assertion events can filter by topic when they carry one
  - file-write events expose a path prefix filter through event subject matching
  - assertion events can also filter by assertion name through subject matching
  - status choices now narrow to the selected event type
- Replaced the dialog-local event switch logic with `DashboardEventFilterCatalog` metadata:
  - event type descriptors declare their display label, filter fields, field readers, and status options
  - the widget settings dialog renders fields from descriptors
  - runtime dashboard matching evaluates the same descriptors instead of keeping a separate set of conditions
  - `Any event` intentionally has no event-specific filter fields; choosing it clears topic/subject filters and leaves only status filtering
- Verified:
  - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutDir=%TEMP%\FluxMqVerifyUiBuild` passes. The build still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutDir=%TEMP%\FluxMqVerifyUiTests -m:1 --filter "FullyQualifiedName~DashboardEventFilterCatalogTests|FullyQualifiedName~FlowDefinitionComposerTests|FullyQualifiedName~FlowWorkspaceServiceTests"` passes with 89 focused tests.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutDir=%TEMP%\FluxMqVerifyUiTestsFull -m:1` passes with 138 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=%TEMP%\FluxMqVerifySolution -m:1` passes with 391 tests. The solution pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-25 - Scenario event expectation foundation slice

- Started the test/scenario plane under the UI:
  - added `ScenarioRunner`, scenario run/step result models, and a step-runner registry
  - added `expect.event` as the first scenario step runner over the external runtime event stream
  - event expectations can match event type, topic prefix, subject prefix, status, source, payload preview text, and string attributes
  - matched events advance an event offset so later expectations do not reuse the same event
  - unknown scenario step types fail with a clear step result instead of throwing through the runner
- Changed `ApplicationRuntime.Events` collection to broadcast runtime events to multiple observers, so dashboards and scenarios can both listen to the same app run.
- Kept this slice below the desktop UI; the next UI slice can expose test/scenario authoring against this foundation.
- Verified:
  - `dotnet test tests\FluxMq.Pipeline.Tests\FluxMq.Pipeline.Tests.csproj --no-restore -p:UseSharedCompilation=false -m:1` passes with 66 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1` passes with 392 tests. The solution pass still prints existing WinAppSDK PRI qualifier warnings during UI test build.

## 2026-05-25 - Scenario host runner slice

- Wired test/scenario execution into `FlowApplicationHost`:
  - the host keeps the loaded `ApplicationDefinition`
  - `RunScenarioAsync` runs a named scenario against `ApplicationRuntime.Events`
  - the host starts the runtime when a scenario is requested before the app is already running
  - missing scenario names fail with a clear message before runtime startup
- Added app-host regression tests for running a scenario against a live runtime event source and for missing scenario names.
- Verified:
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore -p:UseSharedCompilation=false -m:1` passes with 32 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1` passes with 399 tests.

## 2026-05-25 - Scenario action service and MQTT publish step

- Added a generic `ScenarioStepServices` service bag and passed it through `ScenarioRunner`.
- Added an app-level `mqtt.publish` scenario step runner that:
  - reads `connection`, `topic`, `payload`, `payloadEncoding`, `qos`, and `retain`
  - resolves the named MQTT connection from the running app runtime
  - publishes through that connection without adding any workflow nodes
- Added host services so future app-level scenario steps can depend on runtime capabilities without adding pipeline-specific switches.
- Added regression tests for:
  - scenario services reaching a step runner
  - publishing a numeric payload through a named MQTT connection
  - reporting a missing MQTT connection as a failed scenario step
- Verified:
  - `dotnet test tests\FluxMq.Pipeline.Tests\FluxMq.Pipeline.Tests.csproj --no-restore -p:UseSharedCompilation=false -m:1` passes with 67 tests.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore -p:UseSharedCompilation=false -m:1` passes with 34 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1` passes with 401 tests.

## 2026-05-25 - CLI scenario runner slice

- Added a `scenario` CLI command that runs a named test/scenario from an app configuration file.
- The command uses the same `FlowApplicationHost.RunScenarioAsync` boundary as the desktop/runtime path.
- Added text and JSON result output for scenario runs, including step status/message and matched event details.
- Added a distinct `ScenarioFailed` exit code for scenarios that run but fail their steps.
- Added CLI tests for:
  - successful scenario execution by name
  - failed scenario steps
  - JSON output shape
  - missing scenario names
- Verified:
  - `dotnet test tests\FluxMq.Cli.Tests\FluxMq.Cli.Tests.csproj --no-restore -p:UseSharedCompilation=false -m:1` passes with 12 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1` passes with 406 tests.

## 2026-05-25 - Test scenario visibility slice

- Added a sample `t1` scenario to `C:\Users\meisa\OneDrive\Documents\FluxMQ\app1.json`:
  - `publishSampleRequest` publishes a JSON MQTT message to `fluxmq/sample/request`
  - `expectTriggerReceive` waits for the trigger receive event on `fluxmq/sample/`
  - `expectMappedPublish` waits for the mapped MQTT publish event on topic `test`
- Fixed the test tab placeholder so loaded scenario steps are projected from the app definition and rendered as ordered step cards.
- Added a `Run test` command to the test tab:
  - the active test scenario runs through the same app host/runtime path as command-line scenario execution
  - the test UI shows sequential step relation with arrows
  - completed runs show scenario status and per-step status/message/matched event details
- Added first-step scenario editing:
  - test scenarios can add MQTT publish or expect-event steps from the test tab
  - existing test steps can be edited through a settings dialog
  - test steps can be deleted without touching raw JSON
- Added a test-step palette beside the test tab, matching the pipeline/dashboard tab structure:
  - `MQTT publish` and `Expect event` are now palette entries
  - test steps can be clicked or dragged into the test scenario designer
- Adjusted the test scenario designer so the step row fills the available tab height and long scenarios scroll horizontally at the bottom of the test surface.
- Refined the expect-event editor so event type controls the visible filter fields:
  - `Any event` shows no topic/subject filter
  - MQTT/schema events show topic filtering
  - file-write events show path/subject filtering
  - assertion events show topic and assertion-name filtering
- Fixed dashboard editing so assigned widgets can be deleted from a dashboard cell; deleting a widget also clears the cell reference.
- Added workspace/composer snapshots for test scenarios and step configurations.
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -m:1` passes with 147 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1` passes with 415 tests.

## 2026-05-25 - Scenario step ordering slice

- Started ordered scenario editing in the test tab:
  - scenario step cards now expose earlier/later controls
  - moving a step rewrites the ordered `tests.<name>.steps` object without changing step names or configurations
  - the workspace service clears the previous scenario result when step order changes
- Adjusted the test scenario designer layout so the test surface can fill available height while individual step cards keep their natural content height.
- Added focused workspace/composer tests for persisted scenario step ordering.
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$out -m:1 --filter "FullyQualifiedName~FlowDefinitionComposerTests|FullyQualifiedName~FlowWorkspaceServiceTests"` passes with 97 tests.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$out -m:1` passes with 149 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$out -m:1` passes with 417 tests.

## 2026-05-25 - Event attribute filter slice

- Started event-specific attribute filtering for dashboard widgets and test expectations:
  - event filter descriptors can now define fields backed by `FlowEvent.Attributes`
  - JSON schema validation events expose a `Schema id` field from the `schemaId` attribute
  - dashboard event matching can filter by event attribute values without special-case UI branches
  - scenario expectation editing persists attribute filters into the nested `attributes` step configuration
  - scenario cards render only non-empty configuration values and show attribute filters with clean labels
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$out -m:1 --filter "FullyQualifiedName~DashboardEventFilterCatalogTests|FullyQualifiedName~FlowDefinitionComposerTests"` passes with 62 tests.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$out -m:1` passes with 151 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$out -m:1` passes with 419 tests.

## 2026-05-25 - Scenario step catalog slice

- Started catalog-driven test step metadata:
  - step ids now have a shared `ScenarioStepTypes` contract
  - `ScenarioStepCatalog` owns test-step display names, categories, icons, name prefixes, and editor kind
  - the component palette, test scenario cards, step editor dialog titles, and composer step naming now read from the catalog
  - hard-coded publish/expect string checks are now limited to the shared contract and catalog definitions
- Added focused catalog tests covering known step descriptors and unknown-step fallback behavior.
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$out -m:1 --filter "FullyQualifiedName~ScenarioStepCatalogTests|FullyQualifiedName~FlowDefinitionComposerTests|FullyQualifiedName~FlowWorkspaceServiceTests"` passes with 100 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$out -m:1` passes with 421 tests.
