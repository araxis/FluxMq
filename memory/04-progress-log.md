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

- Created local branch `codex/fluxmq-redesign-ui` from the dirty UI refactor state so the redesign work is isolated from `feature/live-inspector-and-json-viewer`.
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
- Added `MqttPublishRequestMapperComponent` and `MqttRecordingRequestMapperComponent` so a flow can read clearly as `source -> filter/router -> request mapper -> actor`.
- Added runtime node types, app factory registrations, and UI catalog entries for publish/recording request mappers and actors.
- Verified:
  - `dotnet test tests\FluxMq.Components.Tests\FluxMq.Components.Tests.csproj --no-restore -p:UseSharedCompilation=false` passes with 87 tests.
  - `dotnet build src\FluxMq.App\FluxMq.App.csproj --no-restore -p:UseSharedCompilation=false` passes with 0 errors and 0 warnings.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false` passes: 218 tests.

## 2026-05-22 - Dynamic mapper and ops vision correction

- Removed the stale `rtk` shell-command requirement from `C:\Users\meisa\.codex\RTK.md`; normal shell commands should be used directly.
- Added `memory/08-dynamic-mapping-and-ops-vision.md`.
- Corrected the component plan: dynamic mappers are a core FluxMQ capability, not incidental glue.
- Recorded Dynamic Expresso for C#-style filters/mappers and Jsonata or equivalent for JSON query/mapping as explicit runtime directions.
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
- Added `MqttPublishRequestExpressionMapper` and `MqttPublishRequestMapDefinition`; `mqtt.publish-request` can now map topic, payload, QoS, and retain through configurable expressions.
- Added `MqttPublisherComponent` and `mqtt.publisher` as the MQTT publish actor node type.
- Added focused tests for:
  - Dynamic Expresso filtering with `qos >= 1`.
  - Dynamic MQTT publish request mapping.
  - Runtime flow: generated MQTT envelopes -> expression filter -> dynamic publish-request mapper -> MQTT publisher using `broker2`.
- Verified targeted tests:
  - `dotnet test tests\FluxMq.Components.Tests\FluxMq.Components.Tests.csproj --no-restore -p:UseSharedCompilation=false` passes with 89 tests.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore -p:UseSharedCompilation=false` passes with 19 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false` passes with 221 tests.

## 2026-05-22 - Actor naming and File Writer runtime slice

- Removed the old MQTT publish and recording compatibility node aliases; app definitions should now use `mqtt.publisher` and `mqtt.recorder`.
- Removed the old generic source compatibility node alias; app definitions should now use `mqtt.live-source`, `session.source`, or `generated.source`.
- Renamed the publish namespace to `FluxMq.Components.MqttPublisher`.
- Renamed the metrics observer to `MqttMetricsComponent` / `mqtt.metrics`.
- Added `FileWriteRequest`, Dynamic Expresso-backed `FileWriteRequestExpressionMapper`, `FileWriteRequestMapperComponent`, and `FileWriterComponent`.
- Registered `file.write-request` and `file.writer` in the runtime factory registry, UI catalog, node widgets, and definition composer.
- Updated docs and memory to describe flows as `source -> filter/router -> dynamic mapper -> actor/observer`.
- Verified:
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -m:1` passes with 225 tests after the actor/observer rename, explicit source cleanup, and File Writer slice.
