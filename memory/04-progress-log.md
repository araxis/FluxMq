# FluxMQ Progress Log

Chronological progress record.

## 2026-06-05 - V1 candidate QA blocker slice

- Started from `main` on `work/v1-candidate-qa` and kept FluxFlow read-only.
- Re-ran the V1 candidate command gates, release-shaped tests, operations sample validation, docs-site build, and Windows package build.
- Fixed generated-source dashboard projection so generated/runtime source envelopes emit dashboard-visible `mqtt.message.received` events without duplicating trigger-owned flow events.
- Added focused UI test coverage proving generated source messages drive dashboard event snapshots, topic counts, payload totals, retain counts, and workspace logs.
- Fixed packaged Dashboard Live layout blockers:
  - compacted payload distribution and QoS/retain rows so small cards do not clip horizontally
  - rendered medium/narrow Live dashboards as a scrollable feed while preserving the saved grid as the source of truth
  - hid auxiliary side panels on narrow shells so the active workspace stays usable
- Fixed Test Studio artifact layout so Runner Console uses the full workspace region instead of the empty tool-column width.
- Corrected docs-site sample wording from the old test name to `operationsSmoke`.
- Packaged desktop QA verified:
  - delete confirmations for pipeline, dashboard, and test artifacts
  - generated traffic driving Dashboard Live widgets
  - Dashboard Live at 1366x900, 900x900, and 480x900 without widget horizontal overflow
  - Scenario Designer and Runner Console for `operationsSmoke`
  - Runner Console preflight, timeline, live events, diagnosis, logs, report/history actions, and passed run state
  - Logs scope/level/search controls and runner logs not incrementing dashboard metrics
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --nologo -p:UseSharedCompilation=false -p:UseAppHost=false --verbosity minimal` passed with 303 tests
  - `dotnet test FluxMq.sln --no-restore --nologo -m:1 -p:UseSharedCompilation=false -p:UseAppHost=false --verbosity minimal` passed with 615 tests
  - `.\eng\verify-samples.ps1` passed
  - `dotnet restore .\FluxMq.sln -p:RuntimeIdentifierOverride=win-x64 -p:RuntimeIdentifier=win-x64 --nologo` passed
  - `dotnet test .\FluxMq.sln --configuration Release --no-restore --verbosity minimal -m:1 -p:RuntimeIdentifierOverride=win-x64 -p:RuntimeIdentifier=win-x64 -p:UseSharedCompilation=false --nologo` passed with 615 tests
  - `.\eng\package-windows.ps1 -Configuration Release -Version 0.1.0` produced the portable zip and MSI
  - `dotnet run --project src\FluxMq.Cli\FluxMq.Cli.csproj -- validate --config samples\flow-applications\operations-dashboard-test-studio.json --output json` returned `isValid: true`
  - `npm run build` in `docs-site` passed

## 2026-06-05 - Dashboard/test studio stabilization slice

- Hardened dashboard Design/Live surfaces around the existing V2 schema without adding widget types or changing saved JSON shape.
- Tightened responsive containment for dashboard widget cards, event tables, gauges, topic panels, payload distribution, QoS/retain breakdown, grid handles, and dense toolbars.
- Kept Scenario Designer as the authoring surface and added Runner Console report/history actions so execution review supports preview, copy, save, and selected historical runs.
- Improved scenario step editor title/context and narrow-window editor wrapping for the existing step pack.
- Added focused component catalog coverage proving package-provider fallback still exposes serialization transforms and FluxMQ aliases.
- Verified:
  - `npm run build` in `docs-site`
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore`
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj -p:UseFluxFlowSourceReferences=false -p:FluxFlowSourceRoot=D:\NoFluxFlow\`
  - `dotnet restore FluxMq.sln` after the package-fallback restore, then `dotnet test FluxMq.sln --no-restore`
  - `dotnet run --project src\FluxMq.Cli\FluxMq.Cli.csproj -- validate --config samples\flow-applications\operations-dashboard-test-studio.json --output json`
  - `git diff --check`
  - Desktop smoke: launched `samples\flow-applications\operations-dashboard-test-studio.json` in the app and confirmed the main window opened.

## 2026-06-05 - Component boundary refactor slice

- Added FluxMQ catalog adapter interfaces/classes so the UI catalog can be
  backed by FluxFlow component design metadata contracts.
- Changed `FlowComponentCatalog` and `FlowDefinitionComposer` to read component
  descriptors/defaults through the adapter.
- Split runtime registration into package component registration first and
  FluxMQ runtime adapter/compatibility registration second.
- Added component catalog adapter tests covering design metadata, descriptor
  mapping, and default configuration compatibility.
- Added docs and memory for the new component boundary.
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore`
  - `dotnet test FluxMq.sln --no-restore`

## 2026-06-05 - Dashboard and test studio V2 slice

- Added V2 dashboard artifact support with `version`, responsive metadata, reusable metrics, widget bindings, and view metadata.
- Added V2 test artifact support with ordered phases, run profile, run-history references, and report-snapshot metadata.
- Added automatic JSON migration on designer load/parse. V1 flat tests are preserved in an imported phase, and a flat `steps` mirror remains for compatibility.
- Added dashboard widget and metric registries plus a neutral FluxMQ chart adapter boundary.
- Expanded the dashboard widget pack with KPI tile, status strip, rate tile, dedicated chart aliases, payload distribution, and QoS/retain breakdown widgets.
- Expanded the scenario step registry and runner bindings with wait/conditional event steps, payload/schema assertions, metric threshold assertions, delay, and cleanup action.
- Reworked the test UI into phase lanes and added a separate Runner Console for preflight, controls, timeline, live events, diagnosis, and runner logs.
- Added LiteDB-backed scenario run history storage with report JSON/text snapshots and log excerpts.
- Added `samples/flow-applications/operations-dashboard-test-studio.json` as the repo-contained V2 operations dashboard/test sample.
- Added `docs/dashboard-test-studio-v2.md`.
- Verified:
  - `dotnet build FluxMq.sln --no-restore`
  - `dotnet test tests/FluxMq.UI.Tests/FluxMq.UI.Tests.csproj`
  - `dotnet test tests/FluxMq.App.Tests/FluxMq.App.Tests.csproj`
  - `dotnet test tests/FluxMq.Components.Tests/FluxMq.Components.Tests.csproj`
  - `dotnet test tests/FluxMq.Scenarios.Tests/FluxMq.Scenarios.Tests.csproj`
  - `dotnet run --project src/FluxMq.Cli -- validate --config samples/flow-applications/operations-dashboard-test-studio.json`
  - `dotnet test FluxMq.sln --no-restore`

## 2026-06-04

- Added a repo-contained `operations-monitor` sample app for documentation and manual UI checks instead of using a personal workspace file.
- The sample covers app-owned broker resources, two pipelines, a dashboard, and a test scenario with saved designer positions.
- Added current desktop screenshots from the sample workspace and removed stale captures from the documentation site.
- Added startup file-open support to the desktop workspace so a sample can be opened with `--open <path>` through the normal project loader.
- Validated the sample through the CLI.

## 2026-06-03

- Recorded operations-team product feedback: the MQTT topic tree is a critical visual object for ops users and currently does not have enough space or importance in the desktop UI.
- Added an accepted direction to treat the topic tree as a first-class workspace visualization and to make an explicit layout decision before the next major desktop layout/design pass.
- Added a first-class `Topics` workspace page backed by the existing live/stored message projection, with a full-height topic tree, message table, and payload inspector so topic structure is no longer confined to the right inspector.
- Tightened the live tools shell policy so the right inspector/publisher panel is available only on pipeline and dashboard surfaces, keeping test, logs, and topic explorer pages focused on their own workspace content.
- Improved Logs page filtering interaction with clearable search and a reset-filters action for scope, level, and search filters.
- Wired the dashboard designer to the active workspace change signal so live dashboard widgets repaint from runtime event snapshots instead of waiting for definition edits.
- Added a tested catalog search helper and compact result count in the component/widget/test-step panel so large component lists are easier to scan during authoring.
- Reworked the New Connection dialog into a wider two-section MudBlazor form with broker and security fields arranged for faster scanning and basic disabled-state validation.
- Aligned the New App dialog with the wider responsive connection form so initial app and broker setup is less cramped.
- Reworked the dashboard widget settings dialog into a wider responsive form and changed MQTT QoS/retain filters to finite toggle controls.
- Resynced the public documentation site with the current app model, including app-owned resources, pipelines, dashboards, tests, logs, topics, live tools, and current workflow component ids.

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

- Implemented Stage 1 — core MQTT client and pipeline foundation (PR #4):
  - `FluxMq.Core`: `MqttConnectionProfile`, `MqttEnvelope`, `MqttClientState`, `IFluxMqttClient`, `FluxMqttClient` (MQTTnet wrapper, messages → bounded `Channel<MqttEnvelope>`).
  - `FluxMq.Pipeline`: initial `IMessageProcessor` + `MessagePipeline` (sequential fan-out).
  - 13 tests passing.
- Replaced sequential pipeline with TPL Dataflow (PR #5):
  - Removed `IMessageProcessor` and `MessagePipeline`.
  - Added `MqttPipeline`: `BufferBlock` → `BroadcastBlock` → consumer `ActionBlock`s.
  - `pipeline.LinkTo(block)` for simple sinks; `pipeline.Output` for filtered linking.
  - 15 tests passing (8 core, 6 pipeline, 1 storage placeholder).

- Added connection state management:
  - `IFluxMqttClient.StateChanged` event — fires on every state transition.
  - `FluxMqttClient` wires `IMqttClient.DisconnectedAsync` to detect unexpected drops; sets `Faulted` if exception present, `Disconnected` otherwise.
  - `SetState` helper centralises all state writes and event firing.
  - `MqttClientStateChangedEventArgs` — carries session ID, profile, and new state.
  - `IMqttConnectionManager` / `MqttConnectionManager` — creates, tracks, and disposes sessions; forwards `StateChanged`; uses injected factory for testability.
  - Reconnect hook (Polly) left as a comment in `OnClientStateChanged`.
  - 6 new connection manager tests using `FakeFluxMqttClient` (no broker required). 21 tests total passing.

- Decided on visual pipeline editor direction (Stage 8):
  - Blazor.Diagrams for drag-and-drop topology editing.
  - Flow application definition JSON model persisted in LiteDB.
  - Flow application runtime with cold `Build` and hot `Patch` modes.
  - Hot-reload requirement: config changes and link changes apply in-place without stopping unaffected blocks or dropping in-flight messages.
  - Module contracts from Stage 2 onwards must be designed with node metadata (ports, configurable properties) in mind.

- Implemented Polly reconnect in `MqttConnectionManager`:
  - Added `MqttClientState.Reconnecting` — surfaced to UI on each retry attempt.
  - `FluxMqttClient.OnClientDisconnectedAsync` no longer completes the channel on unexpected drops — channel stays open so reconnect resumes message flow seamlessly.
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
- Added an MQTT intake prototype to bridge `IFluxMqttClient.Messages` into Dataflow-backed Fork Flow graphs.
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
  - `MqttConnectionComponent`, `MqttTriggerComponent`, and the replay source dropped `IFlowStartable` from their declarations.
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
- Updated the active development target to `F-015`, so the next mapper UI slice should provide input tree, output request shape, whole-request expression editing, preview, and validation instead of only a raw text editor.
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
  - desktop live clients use a separate workspace client id so the live tools do not collide with runtime client ids
- Fixed app-run broker ownership:
  - live broker clients started automatically by `Run` are tracked as app-started clients
  - `Stop` now disconnects those app-started clients while leaving manually connected broker clients alone
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

## 2026-05-25 - Scenario step field descriptor slice

- Extended `ScenarioStepCatalog` from display metadata into editable field metadata:
  - `mqtt.publish` owns broker, topic, payload, payload encoding, QoS, and retain field descriptors
  - select options and default values now live in the catalog instead of the dialog and composer
  - the publish step editor renders from field descriptors while keeping the same visible controls
  - composer defaults for new publish scenario steps use the same descriptor defaults as the editor
  - field descriptors normalize missing option lists to an empty list, preventing editor crashes while rendering descriptor-driven selects
  - initially tried dialog-safe MudSelect popover settings, and app dialogs no longer close on backdrop click
  - the lower Payload encoding and QoS popover approach was superseded on 2026-05-26 by MudBlazor segmented toggle groups
  - the publish step editor now caches descriptor fields and option lists during initialization, so Payload encoding and QoS bind to stable concrete collections
- Added regression coverage for publish field descriptors, selectable values, generated publish-step defaults, and missing field options.
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyStepFieldsFocused6\ -m:1 --filter "FullyQualifiedName~ScenarioStepCatalogTests|FullyQualifiedName~FlowDefinitionComposerTests|FullyQualifiedName~FlowWorkspaceServiceTests"` passes with 102 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyStepFieldsFull6\ -m:1` passes with 423 tests. The solution pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet restore FluxMq.sln -p:RuntimeIdentifierOverride=win-x64 -p:RuntimeIdentifier=win-x64` restores the Release-style target assets.
  - `dotnet test FluxMq.sln --configuration Release --no-restore --verbosity minimal -p:RuntimeIdentifierOverride=win-x64 -p:RuntimeIdentifier=win-x64 -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyStepFieldsRelease6\ -m:1` passes with 423 tests. The solution pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyStepFieldsFocused9\ -m:1 --filter "FullyQualifiedName~ScenarioStepCatalogTests|FullyQualifiedName~FlowDefinitionComposerTests|FullyQualifiedName~FlowWorkspaceServiceTests"` passes with 102 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyStepFieldsFull9\ -m:1` passes with 423 tests. The solution pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet restore FluxMq.sln -p:RuntimeIdentifierOverride=win-x64 -p:RuntimeIdentifier=win-x64` restores the Release-style target assets.
  - `dotnet test FluxMq.sln --configuration Release --no-restore --verbosity minimal -p:RuntimeIdentifierOverride=win-x64 -p:RuntimeIdentifier=win-x64 -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyStepFieldsRelease9\ -m:1` passes with 423 tests. The solution pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyStepFieldsFocused11\ -m:1 --filter "FullyQualifiedName~ScenarioStepCatalogTests|FullyQualifiedName~FlowDefinitionComposerTests|FullyQualifiedName~FlowWorkspaceServiceTests"` passes with 102 tests.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyStepFieldsFocused12\ -m:1 --filter "FullyQualifiedName~ScenarioStepCatalogTests|FullyQualifiedName~FlowDefinitionComposerTests|FullyQualifiedName~FlowWorkspaceServiceTests"` passes with 102 tests.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyStepFieldsFocused13\ -m:1 --filter "FullyQualifiedName~ScenarioStepCatalogTests|FullyQualifiedName~FlowDefinitionComposerTests|FullyQualifiedName~FlowWorkspaceServiceTests"` passes with 102 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test tests\FluxMq.Core.Tests\FluxMq.Core.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyCoreReconnect13\ -m:1 --filter "FullyQualifiedName~MqttConnectionManagerTests.Reconnect_TriggersReconnecting_State_OnFault"` passes with 1 test after the first full run exposed a transient reconnect-disposal race.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyStepFieldsFull14\ -m:1` passes with 423 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-26 - Scenario step lower control cleanup

- Removed the remaining scenario-step editor dropdown problem by replacing the lower MQTT publish dropdowns with MudBlazor single-selection toggle groups:
  - Payload encoding and QoS now use `MudToggleGroup` plus `MudToggleItem` with `SelectionMode.SingleSelection`, `Outlined`, and `Delimiters`.
  - Toggle item labels are rendered through the documented `Text` parameter; the earlier empty-rail failure came from using child content, which suppresses `Text` and receives a special selected-state parameter.
  - Failed `MudSelect`, radio, and button-row attempts were rejected because they either depended on dialog popovers, looked too much like generic form controls, or could still leave blank labels if option metadata arrived empty.
  - The editor now falls back to built-in MQTT publish options if descriptor options are ever empty, so Payload encoding and QoS cannot render as labels without choices.
  - Broker, event type, and status use ordinary MudBlazor `MudSelect` behavior without scenario-specific popover classes.
  - Removed the scenario-step popover/dialog override block from `wwwroot/app.css`, shrinking the global CSS and avoiding more z-index/overflow customization.
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioChoiceButtonsFocused\ -m:1 --filter "FullyQualifiedName~ScenarioStepCatalogTests|FullyQualifiedName~FlowDefinitionComposerTests|FullyQualifiedName~FlowWorkspaceServiceTests"` passes with 102 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioChoiceButtonsFull\ -m:1` passes with 423 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioRadioFocused\ -m:1 --filter "FullyQualifiedName~ScenarioStepCatalogTests|FullyQualifiedName~FlowDefinitionComposerTests|FullyQualifiedName~FlowWorkspaceServiceTests"` passes with 102 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioRadioFull\ -m:1` passes with 423 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioToggleFocused\ -m:1 --filter "FullyQualifiedName~ScenarioStepCatalogTests|FullyQualifiedName~FlowDefinitionComposerTests|FullyQualifiedName~FlowWorkspaceServiceTests"` passes with 102 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioToggleFull\ -m:1` passes with 423 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-26 - Scenario run history slice

- Added bounded in-memory scenario run history to the workspace service:
  - completed scenario runs are kept newest-first in `ScenarioRunHistory`
  - the history is limited to the latest 20 results and cleared when the app definition changes
  - switching the active test now restores that test's latest result instead of showing stale status from a different test
- Added a compact MudBlazor `MudMenu` history action to the test scenario header:
  - the menu lists the latest five runs for the selected test with status icon, finish time, step count, and duration
  - the existing latest-result badges and per-step result display remain unchanged
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioHistoryFocused\ -m:1 --filter "FullyQualifiedName~FlowWorkspaceServiceTests|FullyQualifiedName~FlowDefinitionComposerTests|FullyQualifiedName~ScenarioStepCatalogTests"` passes with 103 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioHistoryFull\ -m:1` passes with 424 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-26 - Scenario report copy slice

- Added a stable scenario run report formatter for desktop reporting:
  - JSON reports include scenario status, start/finish time, duration, step status/message, and matched event details
  - text reports summarize the same result in a CLI-like readable form for future reuse
  - report DTOs avoid copying raw runtime objects directly into UI/export surfaces
- Added a MudBlazor tooltip/icon action to the test scenario header for copying the latest scenario report JSON to the clipboard.
- Added formatter tests for JSON shape and text output.
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioReportFocused\ -m:1 --filter "FullyQualifiedName~FlowWorkspaceServiceTests|FullyQualifiedName~FlowDefinitionComposerTests|FullyQualifiedName~ScenarioStepCatalogTests|FullyQualifiedName~ScenarioRunReportFormatterTests"` passes with 105 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioReportFull\ -m:1` passes with 426 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-26 - Scenario report file export slice

- Added a second MudBlazor tooltip/icon action to the test scenario header for saving the latest scenario report JSON to disk.
- Reused the existing `SaveAsDialog` instead of adding a new custom picker surface:
  - the dialog now accepts title/helper/action text parameters while preserving the existing save-as defaults
  - report saves suggest a file beside the current project when available, otherwise under `Documents\FluxMQ`
  - generated report file names include the scenario name and local finish timestamp with invalid filename characters sanitized
- Added `FlowWorkspaceService.SaveScenarioReportAsync` so report file writing stays behind the workspace service rather than in the Razor component.
- Added regression coverage proving the workspace service writes the stable report JSON shape.
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioReportExportFocused\ -m:1 --filter "FullyQualifiedName~FlowWorkspaceServiceTests|FullyQualifiedName~FlowDefinitionComposerTests|FullyQualifiedName~ScenarioStepCatalogTests|FullyQualifiedName~ScenarioRunReportFormatterTests"` passes with 106 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioReportExportFull\ -m:1` passes with 427 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-26 - Scenario history selection slice

- Made the test scenario run history menu actionable:
  - recent run rows are now clickable MudBlazor `MudMenuItem` commands instead of disabled status-only rows
  - selecting a run restores it as `LastScenarioRunResult`, so the existing status, step result display, copy report, and save report actions target that historical run
  - selection is guarded in `FlowWorkspaceService` so only runs from the currently active test can be restored
- Added regression coverage for restoring an older run and rejecting a run from another active test.
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioHistorySelectionFocused\ -m:1 --filter "FullyQualifiedName~FlowWorkspaceServiceTests|FullyQualifiedName~FlowDefinitionComposerTests|FullyQualifiedName~ScenarioStepCatalogTests|FullyQualifiedName~ScenarioRunReportFormatterTests"` passes with 107 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioHistorySelectionFull\ -m:1` passes with 428 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-26 - Scenario report input capture slice

- Enriched copied and saved scenario reports with each step's configured inputs:
  - `ScenarioRunReportFormatter` now accepts an optional `TestScenarioSnapshot`
  - per-step report DTOs include a stable `configuration` object
  - readable text reports include a compact `config:` line for configured steps
  - the test scenario copy action passes the active scenario snapshot
  - file saves resolve the active scenario through `FlowWorkspaceService`
- Updated report formatter and save-to-file tests so JSON reports prove step input/configuration capture.
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioReportConfigFocused\ -m:1 --filter "FullyQualifiedName~FlowWorkspaceServiceTests|FullyQualifiedName~FlowDefinitionComposerTests|FullyQualifiedName~ScenarioStepCatalogTests|FullyQualifiedName~ScenarioRunReportFormatterTests"` passes with 107 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioReportConfigFull\ -m:1` passes with 428 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-26 - Scenario report preview dialog slice

- Added a MudBlazor-only report preview dialog for the selected scenario run:
  - the test scenario header now has a report view icon next to copy/save
  - the dialog uses `MudTabs` for Summary and JSON views
  - both views use read-only multiline `MudTextField` content so users can inspect or select report text without a custom viewer
  - the preview uses the same selected/latest historical run and scenario snapshot as copy/save
- No CSS was added or changed for the dialog.
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioReportDialogFocused\ -m:1 --filter "FullyQualifiedName~FlowWorkspaceServiceTests|FullyQualifiedName~FlowDefinitionComposerTests|FullyQualifiedName~ScenarioStepCatalogTests|FullyQualifiedName~ScenarioRunReportFormatterTests"` passes with 107 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioReportDialogFull\ -m:1` passes with 428 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-26 - Scenario report preview copy actions slice

- Added MudBlazor copy actions inside the scenario report preview dialog:
  - Summary and JSON tabs now have adjacent dialog action buttons for copying the displayed report text
  - copy feedback uses the existing MudBlazor snackbar path
  - no CSS was added or changed
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioReportDialogCopyFocused\ -m:1 --filter "FullyQualifiedName~FlowWorkspaceServiceTests|FullyQualifiedName~FlowDefinitionComposerTests|FullyQualifiedName~ScenarioStepCatalogTests|FullyQualifiedName~ScenarioRunReportFormatterTests"` passes with 107 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioReportDialogCopyFull\ -m:1` passes with 428 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-26 - Scenario history active-run indicator slice

- Clarified which scenario run is currently displayed after history selection:
  - the test scenario header now shows a small MudBlazor chip for the active run, either `Latest HH:mm:ss` or `History HH:mm:ss`
  - report action tooltips now say `latest` or `selected history` to match the run they will use
  - history menu rows are labeled as `Viewing`, `Latest`, or `History`, and the active row uses a visibility icon
  - no CSS was added or changed
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioHistoryActiveRunFocused\ -m:1 --filter "FullyQualifiedName~FlowWorkspaceServiceTests|FullyQualifiedName~FlowDefinitionComposerTests|FullyQualifiedName~ScenarioStepCatalogTests|FullyQualifiedName~ScenarioRunReportFormatterTests"` passes with 107 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioHistoryActiveRunFull\ -m:1` passes with 428 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-26 - Scenario history return-to-latest slice

- Added a direct MudBlazor history-menu command for leaving a selected historical run:
  - when a historical run is active, the history menu shows `Show latest run`
  - choosing it restores the newest run and shows `Showing latest scenario run`
  - clicking the already-active history row is now a no-op, avoiding misleading snackbar feedback
  - no CSS was added or changed
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioHistoryReturnLatestFocused\ -m:1 --filter "FullyQualifiedName~FlowWorkspaceServiceTests|FullyQualifiedName~FlowDefinitionComposerTests|FullyQualifiedName~ScenarioStepCatalogTests|FullyQualifiedName~ScenarioRunReportFormatterTests"` passes with 107 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioHistoryReturnLatestFull\ -m:1` passes with 428 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-26 - Scenario report dialog metadata slice

- Added selected-run metadata to the MudBlazor report preview dialog:
  - the dialog title area now shows chips for latest/historical scope, scenario status, finish timestamp, and duration
  - the metadata comes from the same selected run used by preview/copy/save, so restored history is visible inside the report dialog too
  - no CSS was added or changed
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioReportDialogMetadataFocused\ -m:1 --filter "FullyQualifiedName~FlowWorkspaceServiceTests|FullyQualifiedName~FlowDefinitionComposerTests|FullyQualifiedName~ScenarioStepCatalogTests|FullyQualifiedName~ScenarioRunReportFormatterTests"` passes with 107 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioReportDialogMetadataFull\ -m:1` passes with 428 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-26 - Scenario report dialog save action slice

- Added a MudBlazor `Save JSON` action inside the report preview dialog:
  - the dialog returns a typed save action result instead of writing files itself
  - the parent component reuses the same SaveAs/workspace report save path as the header save action
  - report file writing remains in `FlowWorkspaceService.SaveScenarioReportAsync`
  - no CSS was added or changed
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioReportDialogSaveFocused\ -m:1 --filter "FullyQualifiedName~FlowWorkspaceServiceTests|FullyQualifiedName~FlowDefinitionComposerTests|FullyQualifiedName~ScenarioStepCatalogTests|FullyQualifiedName~ScenarioRunReportFormatterTests"` passes with 107 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioReportDialogSaveFull\ -m:1` passes with 428 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-26 - Scenario report step summary slice

- Added aggregate step status counts to scenario reports:
  - report JSON now includes `stepSummary` with total, passed, failed, timed out, and canceled counts
  - readable text reports include the same summary in the first line
  - the MudBlazor report preview dialog title area shows a step-count chip for the selected run
  - no CSS was added or changed
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioReportStepSummaryFocused\ -m:1 --filter "FullyQualifiedName~FlowWorkspaceServiceTests|FullyQualifiedName~FlowDefinitionComposerTests|FullyQualifiedName~ScenarioStepCatalogTests|FullyQualifiedName~ScenarioRunReportFormatterTests"` passes with 107 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioReportStepSummaryFull\ -m:1` passes with 428 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-26 - Scenario report first-issue slice

- Added first-issue reporting for non-passing scenario runs:
  - report JSON now includes `firstIssue` with step name, step type, status, and message for the first non-passing step
  - passing reports keep `firstIssue` as `null`
  - readable text reports show a `First issue:` line before the step list when a run has a failed, timed-out, or canceled step
  - the MudBlazor report preview dialog shows an issue chip for non-passing runs
  - no CSS was added or changed
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioReportFirstIssueFocused\ -m:1 --filter "FullyQualifiedName~FlowWorkspaceServiceTests|FullyQualifiedName~FlowDefinitionComposerTests|FullyQualifiedName~ScenarioStepCatalogTests|FullyQualifiedName~ScenarioRunReportFormatterTests"` passes with 107 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioReportFirstIssueFull\ -m:1` passes with 428 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-26 - Scenario report timing slice

- Added per-step timing metadata to scenario reports:
  - report JSON now includes each step's `sequence`, `startedOffsetMilliseconds`, `finishedOffsetMilliseconds`, and `durationMilliseconds`
  - readable text reports include a `timing:` line per step so report exports can be used to line up slow or failing steps without opening raw timestamps
  - the change stays inside report formatting and tests; no UI layout or CSS was changed
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioReportTimingFocused\ -m:1 --filter "FullyQualifiedName~FlowWorkspaceServiceTests|FullyQualifiedName~FlowDefinitionComposerTests|FullyQualifiedName~ScenarioStepCatalogTests|FullyQualifiedName~ScenarioRunReportFormatterTests"` passes with 107 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioReportTimingFull\ -m:1` passes with 428 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-26 - Scenario report matched-event offset slice

- Added matched-event relative timing to scenario reports:
  - matched event JSON now includes `scenarioOffsetMilliseconds` and `stepOffsetMilliseconds`
  - readable text reports show matched event offsets beside the matched event type/topic/status
  - the change stays inside report formatting and tests; no UI layout or CSS was changed
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioReportEventOffsetsFocused2\ -m:1 --filter "FullyQualifiedName~FlowWorkspaceServiceTests|FullyQualifiedName~FlowDefinitionComposerTests|FullyQualifiedName~ScenarioStepCatalogTests|FullyQualifiedName~ScenarioRunReportFormatterTests"` passes with 107 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioReportEventOffsetsFull2\ -m:1` passes with 428 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-26 - Scenario report metadata slice

- Added root metadata to scenario reports:
  - JSON reports now include `schemaVersion` with the stable `scenario-run-report.v1` value
  - JSON reports now include `generatedAt`, defaulting to the UTC export time
  - readable text reports start with a report metadata line before the scenario summary
  - tests can pass an explicit generation timestamp to keep report shape assertions deterministic
  - the change stays inside report formatting and tests; no UI layout or CSS was changed
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioReportMetadataFocused\ -m:1 --filter "FullyQualifiedName~FlowWorkspaceServiceTests|FullyQualifiedName~FlowDefinitionComposerTests|FullyQualifiedName~ScenarioStepCatalogTests|FullyQualifiedName~ScenarioRunReportFormatterTests"` passes with 107 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioReportMetadataFull\ -m:1` passes with 428 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-26 - Scenario report issue list slice

- Added a full issue list to scenario reports:
  - report JSON now includes root `issues` with every failed, timed-out, or canceled step
  - each issue includes step sequence, name, type, status, and message
  - passing reports serialize `issues` as an empty array while keeping `firstIssue` as `null`
  - readable text reports include an `Issues:` section before the step details for non-passing runs
  - the change stays inside report formatting and tests; no UI layout or CSS was changed
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioReportIssuesFocused\ -m:1 --filter "FullyQualifiedName~FlowWorkspaceServiceTests|FullyQualifiedName~FlowDefinitionComposerTests|FullyQualifiedName~ScenarioStepCatalogTests|FullyQualifiedName~ScenarioRunReportFormatterTests"` passes with 107 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioReportIssuesFull\ -m:1` passes with 428 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-26 - Scenario report run identity slice

- Added stable run identity to scenario reports:
  - report JSON now includes root `runId`
  - `runId` is derived from the scenario name plus UTC run start timestamp, so copy/save/export of the same selected historical run can be correlated even when `generatedAt` changes
  - readable text reports include the run id in the metadata line
  - the change stays inside report formatting and tests; no UI layout or CSS was changed
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioReportRunIdFocused\ -m:1 --filter "FullyQualifiedName~FlowWorkspaceServiceTests|FullyQualifiedName~FlowDefinitionComposerTests|FullyQualifiedName~ScenarioStepCatalogTests|FullyQualifiedName~ScenarioRunReportFormatterTests"` passes with 107 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioReportRunIdFull\ -m:1` passes with 428 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-26 - Scenario report snapshot consistency slice

- Made previewed/saved scenario reports use one report snapshot:
  - `ScenarioRunReportFormatter` can now render JSON and text from an already-created `ScenarioRunReport`
  - the preview dialog creates one report snapshot, renders Summary and JSON from it, and shows the run id in a native MudBlazor chip
  - Save JSON from the preview writes the exact JSON shown in the preview instead of regenerating `generatedAt`
  - header save/copy paths still create a single report JSON for the selected latest or historical run
  - `FlowWorkspaceService.SaveScenarioReportJsonAsync` writes pre-rendered report JSON while the existing `SaveScenarioReportAsync` remains available
  - no CSS was added or changed
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioReportSnapshotFocused\ -m:1 --filter "FullyQualifiedName~FlowWorkspaceServiceTests|FullyQualifiedName~FlowDefinitionComposerTests|FullyQualifiedName~ScenarioStepCatalogTests|FullyQualifiedName~ScenarioRunReportFormatterTests"` passes with 108 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioReportSnapshotFull\ -m:1` passes with 429 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-26 - Scenario report text save slice

- Added readable text report export from the preview dialog:
  - the MudBlazor report dialog now has a `Save summary` action beside `Save JSON`
  - `Save summary` opens the existing `SaveAsDialog` with a `.scenario-report.txt` suggestion
  - the saved summary uses the exact text shown in the Summary tab for the selected latest or historical run
  - `FlowWorkspaceService.SaveScenarioReportTextAsync` writes pre-rendered text report content
  - no CSS was added or changed
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioReportTextSaveFocused\ -m:1 --filter "FullyQualifiedName~FlowWorkspaceServiceTests|FullyQualifiedName~FlowDefinitionComposerTests|FullyQualifiedName~ScenarioStepCatalogTests|FullyQualifiedName~ScenarioRunReportFormatterTests"` passes with 109 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioReportTextSaveFull\ -m:1` passes with 430 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-26 - Scenario report text event detail slice

- Enriched readable text reports with matched-event details:
  - matched event text now includes a separate `event:` line for source, subject, payload byte count, and payload preview when present
  - matched event attributes are rendered on a separate `attributes:` line when present
  - configuration and attribute key/value output is ordered by key for stable text reports
  - the change stays inside report formatting and tests; no UI layout or CSS was changed
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioReportEventDetailsFocused\ -m:1 --filter "FullyQualifiedName~FlowWorkspaceServiceTests|FullyQualifiedName~FlowDefinitionComposerTests|FullyQualifiedName~ScenarioStepCatalogTests|FullyQualifiedName~ScenarioRunReportFormatterTests"` passes with 109 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioReportEventDetailsFull\ -m:1` passes with 430 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-26 - Scenario report not-run steps slice

- Added planned-but-not-run steps to scenario reports:
  - report JSON now includes root `notRunSteps`
  - each not-run step includes planned sequence, name, type, and captured configuration
  - passing reports or reports without a matching scenario snapshot serialize `notRunSteps` as an empty array
  - readable text reports include a `Not run:` section before executed step details when a scenario stopped before later planned steps
  - the change stays inside report formatting and tests; no UI layout or CSS was changed
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioReportNotRunFocused\ -m:1 --filter "FullyQualifiedName~FlowWorkspaceServiceTests|FullyQualifiedName~FlowDefinitionComposerTests|FullyQualifiedName~ScenarioStepCatalogTests|FullyQualifiedName~ScenarioRunReportFormatterTests"` passes with 109 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioReportNotRunFull\ -m:1` passes with 430 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-26 - Scenario report planned summary slice

- Made scenario report step summaries aware of planned versus executed steps:
  - `stepSummary` now keeps the existing executed `total` and adds explicit `planned`, `executed`, and `notRun` counts
  - readable text reports say, for example, `3 planned, 2 run, 1 not run` when a scenario stops early
  - the MudBlazor report preview step-count chip uses the same planned/executed summary, so failed-early reports no longer look like only the executed steps existed
  - no CSS was added or changed
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioReportPlannedSummaryFocused\ -m:1 --filter "FullyQualifiedName~FlowWorkspaceServiceTests|FullyQualifiedName~FlowDefinitionComposerTests|FullyQualifiedName~ScenarioStepCatalogTests|FullyQualifiedName~ScenarioRunReportFormatterTests"` passes with 109 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioReportPlannedSummaryFull\ -m:1` passes with 430 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-26 - Scenario report planned-step snapshot slice

- Added the full planned scenario step snapshot to JSON reports:
  - report JSON now includes root `plannedSteps` with planned sequence, step name, step type, and captured configuration
  - `notRunSteps` is derived from the same planned-step snapshot, keeping planned and skipped-step export data consistent
  - readable Summary output remains compact and continues to use the planned/executed/not-run counts plus the `Not run:` section
  - no CSS was added or changed
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioReportPlannedStepsFocused\ -m:1 --filter "FullyQualifiedName~FlowWorkspaceServiceTests|FullyQualifiedName~FlowDefinitionComposerTests|FullyQualifiedName~ScenarioStepCatalogTests|FullyQualifiedName~ScenarioRunReportFormatterTests"` passes with 109 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioReportPlannedStepsFull\ -m:1` passes with 430 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-26 - Scenario expectation timeout diagnostics slice

- Clarified scenario expectation timeout messages:
  - timed-out `expect.event` steps now say whether any scenario/app events were observed while the step was waiting
  - observed non-matching events are summarized by type, topic, status, source, and payload preview
  - `mqtt.message.published` expectations now explicitly say that the matching event can come from a scenario `mqtt.publisher` event or a running app MQTT publisher node event
  - timeout text now reminds users that finished scenario runs do not keep listening and must be rerun to match later events
  - no CSS was added or changed
- Verified:
  - `dotnet test tests\FluxMq.Pipeline.Tests\FluxMq.Pipeline.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioExpectationDiagnosticsFocused2\ -m:1 --filter "FullyQualifiedName~ScenarioRunnerTests"` passes with 7 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyScenarioExpectationDiagnosticsFull\ -m:1` passes with 431 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-26 - Scenario expectation event ordering fix

- Fixed the `t1` false timeout path where a downstream `mqtt.message.published` event could be recorded before the earlier `mqtt.message.received` expectation matched:
  - `ScenarioRunner` now tracks consumed matched event indexes instead of advancing the expectation cursor past every earlier non-matching event
  - later expectations can still match an already-observed but previously-unmatched event, while identical later expectations cannot reuse the same matched event
  - timeout diagnostics now omit already-consumed matched events from the observed-event snapshot
  - `MqttTriggerComponent` now accepts the `mqtt.message.received` event before forwarding the envelope downstream, improving causal event ordering
  - confirmed `C:\Users\meisa\OneDrive\Documents\FluxMQ\app1.json` has the right `t1` shape: step 1 publishes `fluxmq/sample/request`, the trigger subscribes to `fluxmq/#`, the mapper publishes to `test`, and step 3 expects the app-emitted `mqtt.message.published` event on `test`
  - no UI CSS or `app.css` changes were made
- Verified:
  - `dotnet test tests\FluxMq.Pipeline.Tests\FluxMq.Pipeline.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyPipelineOrdering3\ -m:1 --filter "FullyQualifiedName~ScenarioRunnerTests"` passes with 8 tests.
  - `dotnet test tests\FluxMq.Components.Tests\FluxMq.Components.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyComponentOrdering4\ -m:1 --filter "FullyQualifiedName~MqttTriggerComponentTests"` passes with 5 tests.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyAppOrdering\ -m:1 --filter "FullyQualifiedName~LiveTriggerAndJsonataMapper_CanPublishMappedRequestsToConnection|FullyQualifiedName~MqttPublishScenario"` passes with 1 test.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyAppScenarioHost\ -m:1 --filter "FullyQualifiedName~FlowApplicationHostTests.RunScenarioAsync"` passes with 4 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyFullOrdering2\ -m:1` passes with 432 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-26 - Runtime events mirrored into workspace logs

- Fixed the too-empty Logs tab while scenarios and dashboards were seeing runtime events:
  - `FlowWorkspaceService` now projects every app `FlowEvent` into `Logs` as an info entry while still storing it in `RuntimeEvents`
  - runtime event log entries include event type, source, workflow/node scope when the source node is known, topic, status, payload byte count, sorted attributes, and payload preview
  - this makes `mqtt.message.received` and `mqtt.message.published` visible in Logs even when no explicit `FlowLoggerComponent` is present
  - added coverage for a scenario publish/receive flow and for publisher runtime-event log entries
  - no UI CSS or `app.css` changes were made
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyRuntimeEventsLogsFocused2\ -m:1 --filter "FullyQualifiedName~FlowWorkspaceServiceTests"` passes with 45 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyRuntimeEventsLogsUiBroad\ -m:1 --filter "FullyQualifiedName~FlowWorkspaceServiceTests|FullyQualifiedName~ScenarioRunReportFormatterTests|FullyQualifiedName~ScenarioStepCatalogTests|FullyQualifiedName~FlowDefinitionComposerTests"` passes with 110 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyRuntimeEventsLogsFull\ -m:1` passes with 433 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `git diff --check` passes; Git reports only existing LF-to-CRLF working-copy warnings.
  - `git diff -- src\FluxMq.UI\wwwroot\app.css` is empty.

## 2026-05-26 - Scenario run event start boundary and MQTT retain expectations

- Tightened scenario event isolation and made MQTT delivery flags first-class expectation filters:
  - `ScenarioRunner` now creates its event journal with the scenario start timestamp, so broadcast-block replay or retained/stale runtime events emitted before the run cannot satisfy or pollute a fresh test run
  - `expect.event` attribute matching now treats boolean strings case-insensitively, so config `retain=false` matches MQTT event attribute `retain=False`
  - scenario attribute configuration now accepts string, boolean, and number JSON values, so `attributes: { "retain": false, "qos": 1 }` is valid
  - MQTT received/published/recorded event filter descriptors now expose `QoS` and `Retain`
  - the MudBlazor scenario step editor renders `QoS` and `Retain` expectation filters with toggle groups, using existing MudBlazor components and no CSS
  - test step summaries show `QoS` and retain/no-retain expectation filters
  - no UI CSS or `app.css` changes were made
- Verified:
  - `dotnet test tests\FluxMq.Pipeline.Tests\FluxMq.Pipeline.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyMqttRetainExpectationPipeline\ -m:1 --filter "FullyQualifiedName~ScenarioRunnerTests"` passes with 10 tests.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyMqttRetainExpectationUi\ -m:1 --filter "FullyQualifiedName~DashboardEventFilterCatalogTests|FullyQualifiedName~FlowDefinitionComposerTests|FullyQualifiedName~ScenarioStepCatalogTests|FullyQualifiedName~FlowWorkspaceServiceTests"` passes with 112 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyMqttRetainExpectationFull\ -m:1` passes with 436 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `git diff --check` passes; Git reports only existing LF-to-CRLF working-copy warnings.
  - `git diff -- src\FluxMq.UI\wwwroot\app.css` is empty.

## 2026-05-26 - App1 test runner publish boundary and saved JSON order fix

- Fixed the remaining `app1.json` `t1` false timeout where the first step looked correct in the UI but the downstream publish expectation still failed:
  - confirmed the saved file at `C:\Users\meisa\OneDrive\Documents\FluxMQ\app1.json` has the correct `t1` test order and expected shape
  - `RuntimeMqttScenarioPublisher` now publishes test MQTT messages through a separate short-lived scenario MQTT client cloned from the selected connection profile, instead of publishing through the running app MQTT client
  - this makes the scenario publish step behave like an external MQTT client, so `pip1.trigger` can receive `fluxmq/sample/request` and the app can route/map/publish to `test`
  - `FlowWorkspaceService` now creates `FlowApplicationHost` from a raw `ApplicationDefinition` parsed with `System.Text.Json`, preserving saved JSON object order for test steps instead of going through `IConfiguration`
  - this prevents visual step order from being scrambled before execution
  - added a regression test using an `app1`-shaped flow and a shared fake MQTT broker: scenario publishes `fluxmq/sample/request`, `pip1` receives it, mapper/publisher emits `mqtt.message.published` on `test`, and QoS/retain expectations match
  - no UI CSS or `app.css` changes were made
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyApp1Scenario\ -m:1 --filter "FullyQualifiedName~RunActiveTestScenarioAsync_CanObserveMappedPublisherEventFromAppFlow"` passes with 1 test.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyUiScenarioSlice\ -m:1 --filter "FullyQualifiedName~FlowWorkspaceServiceTests|FullyQualifiedName~FlowDefinitionComposerTests|FullyQualifiedName~ScenarioStepCatalogTests|FullyQualifiedName~DashboardEventFilterCatalogTests"` passes with 113 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test tests\FluxMq.Pipeline.Tests\FluxMq.Pipeline.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyPipelineScenarios\ -m:1 --filter "FullyQualifiedName~Scenario"` passes with 11 tests.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyAppScenarios\ -m:1 --filter "FullyQualifiedName~FlowApplicationHostTests"` passes with 10 tests.
  - `dotnet test tests\FluxMq.Components.Tests\FluxMq.Components.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyComponentTriggers2\ -m:1 --filter "FullyQualifiedName~MqttTriggerComponentTests"` passes with 5 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyFullScenarioClient\ -m:1` passes with 437 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `git diff --check` passes; Git reports only existing LF-to-CRLF working-copy warnings.
  - `git diff -- src\FluxMq.UI\wwwroot\app.css` is empty.

## 2026-05-26 - Scenario runner no longer auto-starts app runtime

- Split the first test-runner boundary away from the app runtime lifecycle:
  - `FlowWorkspaceService.RunActiveTestScenarioAsync` no longer builds or starts the current app runtime when the user clicks Run test
  - the CLI `scenario` command also runs through the isolated scenario runner instead of host-bound auto-start behavior
  - scenario MQTT publish steps now resolve connection profiles directly from the saved `ApplicationDefinition` through `ApplicationDefinitionMqttScenarioPublisher`, so publish-only scenarios can run as an isolated test runner without an app host
  - `FlowApplicationHost.RunScenarioAsync` now requires the host runtime to already be running and reports a clear error if callers try to use host-bound scenario execution before explicit app start
  - existing `expect.event` can still observe the explicitly running local app target for now; the next integration-test slice should move toward composing the test runner from normal pipeline blocks (`mqtt.publisher`, `mqtt.trigger`, condition/router behavior) plus narrow test-specific `expect`/`when` blocks, rather than adding a separate broker-probe engine
  - added coverage proving publish-only scenario execution does not start the UI app runtime, and updated host tests to start runtime explicitly when testing local-runtime events
  - no UI CSS or `app.css` changes were made
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyIsolationUiFocused2\ -m:1 --filter "FullyQualifiedName~RunActiveTestScenarioAsync"` passes with 5 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyIsolationAppTests2\ -m:1 --filter "FullyQualifiedName~FlowApplicationHostTests"` passes with 11 tests.
  - `dotnet test tests\FluxMq.Cli.Tests\FluxMq.Cli.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyIsolationCli\ -m:1` passes with 12 tests.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyIsolationUiSlice\ -m:1 --filter "FullyQualifiedName~FlowWorkspaceServiceTests|FullyQualifiedName~FlowDefinitionComposerTests|FullyQualifiedName~ScenarioStepCatalogTests|FullyQualifiedName~DashboardEventFilterCatalogTests"` passes with 114 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test tests\FluxMq.Pipeline.Tests\FluxMq.Pipeline.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyIsolationPipeline\ -m:1 --filter "FullyQualifiedName~Scenario"` passes with 11 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqVerifyFullIsolationBoundary2\ -m:1` passes with 439 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `git diff --check` passes; Git reports only existing LF-to-CRLF working-copy warnings.
  - `git diff -- src\FluxMq.UI\wwwroot\app.css` is empty.

## 2026-05-26 - Scoped workspace logs page

- Promoted logs from a right-inspector-only list into an app-level Logs tab:
  - `WorkspaceLogEntry` now carries explicit `Scope`, `ArtifactKind`, and `ArtifactName` metadata
  - app runtime events, flow log entries, and component errors are scoped as `App` and tagged with their pipeline artifact when known
  - scenario pass/fail diagnostics are scoped as `Test runner` and tagged with the active test artifact
  - validation/designer/file diagnostics infer a `System` scope unless they are tied to a pipeline artifact
  - added a shared `WorkspaceLogFilter`/`WorkspaceLogQuery` helper for scope, level, and search filtering
  - added a MudBlazor-first `WorkspaceLogPanel` using `MudSelect`, `MudTextField`, `MudTable`, chips, tooltips, and icon buttons without custom CSS
  - corrected the log filter toolbar so Scope, Level, and Search use compact fixed working widths instead of flex-growing across the whole logs page
  - the workspace now has a first-level `Logs` tab beside pipelines, dashboards, and tests; the duplicate right-inspector Logs tab was removed
  - no UI CSS or `app.css` changes were made
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqUiLogsScope\ -m:1` passes with 170 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqFullLogsScope\ -m:1` passes with 443 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `git diff --check` passes; Git reports only existing LF-to-CRLF working-copy warnings.
  - `git diff -- src\FluxMq.UI\wwwroot\app.css` is empty.

## 2026-05-26 - Test runner pipeline direction correction

- Corrected the next integration-test direction after design review:
  - do not add a special `mqtt.expect` broker-probe side channel
  - do not reimplement a test runner runtime from the ground up
  - keep the current scenario runner as a thin orchestrator for now
  - evolve scenario/test execution toward a normal pipeline composition that can reuse existing runtime components
  - test-runner pipelines should use normal MQTT blocks where possible, especially `mqtt.publisher`, `mqtt.trigger`, and existing condition/router behavior
  - reserve test-specific nodes for assertion/control semantics such as `expect`, `when`, and reporting
  - this keeps integration tests aligned with app/runtime architecture and avoids a parallel MQTT/test engine
- Cleaned up the abandoned `mqtt.expect` probe attempt before it became part of the codebase.
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqPivotScenarioRunner\ -m:1 --filter "FullyQualifiedName~ScenarioStepCatalogTests|FullyQualifiedName~FlowWorkspaceServiceTests|FullyQualifiedName~FlowDefinitionComposerTests"` passes with 111 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-26 - Scenario runner MQTT client factory boundary

- Refactored the scenario/test runner MQTT boundary before adding more test-specific components:
  - added `IMqttScenarioClientFactory` as the reusable contract for creating runner-owned MQTT clients from shared app-level connection resources
  - added saved-definition and running-runtime factory implementations so UI scenario runs, CLI scenario runs, and host-bound scenario runs use the same app-resource-to-MQTT-client boundary
  - `ApplicationDefinitionMqttScenarioPublisher` and `RuntimeMqttScenarioPublisher` now only publish through the factory instead of owning resource lookup and profile cloning themselves
  - scenario services now register both `IMqttScenarioClientFactory` and `IMqttScenarioPublisher`, preparing future normal test-runner `mqtt.trigger`/`expect`/`when` components to use the same app resource names without coupling to app runtime MQTT clients
  - shared app resources remain the source of truth; each scenario/test runner MQTT client still clones the MQTT profile with its own short-lived profile id, MQTT client id, and `CleanStart = true`
  - no UI CSS or `app.css` changes were made
- Verified:
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqScenarioClientFactory2\ -m:1 --filter "FullyQualifiedName~MqttScenarioClientFactoryTests|FullyQualifiedName~FlowApplicationHostTests"` passes with 14 tests.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqScenarioClientFactoryUi\ -m:1 --filter "FullyQualifiedName~FlowWorkspaceServiceTests|FullyQualifiedName~ScenarioStepCatalogTests|FullyQualifiedName~FlowDefinitionComposerTests"` passes with 111 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test tests\FluxMq.Cli.Tests\FluxMq.Cli.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqScenarioClientFactoryCli\ -m:1 --filter "FullyQualifiedName~CliRunnerTests"` passes with 12 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqScenarioClientFactoryFull2\ -m:1` passes with 446 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `git diff --check` passes; Git reports only existing LF-to-CRLF working-copy warnings.
  - `git diff -- src\FluxMq.UI\wwwroot\app.css` is empty.

## 2026-05-26 - App1 sample aligned with isolated test runner

- Updated `C:\Users\meisa\OneDrive\Documents\FluxMQ\app1.json` to better match the current test-runner behavior:
  - `pip2.trigger` now subscribes to `$SYS/#` instead of broad `#`, so the sample `t1` receive expectation cannot be masked by a second pipeline listening to all application topics
  - `t1` expectations use explicit `source` filters: `MqttTrigger` for the received event and `MqttPublisher` for the published event
  - `t1` expectation attributes now use typed JSON values, `qos: 1` and `retain: false`, matching the new scenario attribute reader support
- Verified:
  - `dotnet run --project src\FluxMq.Cli\FluxMq.Cli.csproj --no-restore -- validate --config C:\Users\meisa\OneDrive\Documents\FluxMQ\app1.json` reports `Flow application is valid. Workflows: 2. Resources: 2.`

## 2026-05-26 - MQTT client naming alignment

- Tightened the new scenario/test runner code to use the agreed domain wording:
  - `IMqttScenarioClientFactory` now exposes `CreateClient` instead of `CreateSession`
  - scenario MQTT factories and publishers now use `clientFactory` and `client` names at the test-runner boundary
  - `FlowWorkspaceService` now takes `runtimeClientFactory` instead of `runtimeSessionFactory`
  - memory notes for the new resource boundary now describe live MQTT clients rather than sessions
- Deferred a broader core rename of the older `IFluxMqttClient` type because it is spread across the existing app and should be a dedicated mechanical rename slice.
- Verified:
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqClientNamingApp\ -m:1 --filter "FullyQualifiedName~MqttScenarioClientFactoryTests|FullyQualifiedName~FlowApplicationHostTests"` passes with 14 tests.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqClientNamingUi\ -m:1 --filter "FullyQualifiedName~FlowWorkspaceServiceTests"` passes with 48 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqClientNamingFull\ -m:1` passes with 446 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-26 - App1 published MQTT dashboard counter

- Investigated the MQTT published-message dashboard counter path:
  - `MqttPublisherComponent` already emits `mqtt.message.published` runtime events with topic, status, payload preview, QoS, retain, source, and source node id.
  - `DashboardEventFilterCatalog` already matches published MQTT event type, topic prefix, status, QoS, and retain filters.
  - The saved sample dashboard counter in `C:\Users\meisa\OneDrive\Documents\FluxMQ\app1.json` was still configured as an any-event counter with `status: received`, so it could not count mapped publish events.
- Updated `app1.json` `d1.eventCounter` to count publishes to `test`:
  - `eventType: mqtt.message.published`
  - `topicStartsWith: test`
  - `status: published`
  - title changed to `Published to test`
  - no QoS or retain filter is applied, so both the pipeline's QoS 1 publish and the right-inspector manual QoS 0 publish can count.
- Added a regression assertion to `RunActiveTestScenarioAsync_CanObserveMappedPublisherEventFromAppFlow` so the UI workspace test now verifies that a dashboard `event.counter` configured for `mqtt.message.published` counts the app-emitted mapped publish event.
- Added manual publish projection for the right-inspector Publish panel:
  - `LiveMqttWorkspaceService.PublishAsync` now returns `true` only after the MQTT client publish succeeds, and `false` for no connection or publish failure
  - after a successful manual publish, `LiveInspectorPanel` records a `LivePublisher` `mqtt.message.published` event on the active app workspace
  - manual publish events include topic, status `published`, payload preview, QoS, retain, and connection name attributes, so dashboard counters and Logs can see the action
- No UI CSS or `app.css` changes were made.
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~RunActiveTestScenarioAsync_CanObserveMappedPublisherEventFromAppFlow" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqDashboardPublishedCounterAfter2\ -m:1` passes. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~RecordManualMqttPublish_UpdatesPublishedDashboardCounter|FullyQualifiedName~PublishAsync_Returns" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqManualPublishCounter2\ -m:1` passes with 3 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet run --project src\FluxMq.Cli\FluxMq.Cli.csproj --no-restore -- validate --config C:\Users\meisa\OneDrive\Documents\FluxMQ\app1.json` reports `Flow application is valid. Workflows: 2. Resources: 2.`

## 2026-05-27 - Scenario review blockers addressed

- Addressed the review blockers from `report.md` before merge:
  - CLI `scenario` now rejects scenarios containing `expect.event` with a clear validation error because that command is publish-only and has no app runtime event stream
  - UI scenario runs now choose `RuntimeMqttScenarioClientFactory` plus `RuntimeMqttScenarioPublisher` when the app runtime is actually running, so scenario MQTT clients resolve connection profiles from the running runtime instead of a possibly different definition snapshot
  - publish-only UI scenario runs without a running app still use the saved-definition MQTT scenario factory
  - `FlowDefinitionComposer` readers no longer use empty `catch { }` blocks for workflow nodes, designer positions, connection resources, or artifact names; malformed JSON now throws a contextual `InvalidOperationException`, and unexpected designer shape errors surface instead of becoming empty lists
  - invalid in-progress JSON can still be placed in the workspace editor and converted to validation diagnostics; active artifact normalization skips only that invalid-json read path while validation reports the real error
- Added regressions for:
  - CLI `expect.event` rejection
  - UI running-runtime MQTT profile resolution
  - malformed JSON reader failures across the affected composer readers
  - invalid designer position shapes no longer being swallowed
- Verified:
  - `dotnet test tests\FluxMq.Cli.Tests\FluxMq.Cli.Tests.csproj --no-restore --filter "FullyQualifiedName~CliRunnerTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqReviewFixCli\ -m:1` passes with 13 tests.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~FlowWorkspaceServiceTests|FullyQualifiedName~FlowDefinitionComposerTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqReviewFixUi3\ -m:1` passes with 114 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore --filter "FullyQualifiedName~FlowApplicationHostTests.RunScenarioAsync|FullyQualifiedName~MqttScenarioClientFactoryTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqReviewFixApp\ -m:1` passes with 8 tests.
  - `dotnet test tests\FluxMq.Pipeline.Tests\FluxMq.Pipeline.Tests.csproj --no-restore --filter "FullyQualifiedName~ScenarioRunnerTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqReviewFixPipeline\ -m:1` passes with 10 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqReviewFixFull\ -m:1` passes with 456 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `git diff --check` passes; Git reports only existing LF-to-CRLF working-copy warnings.
  - `git diff -- src\FluxMq.UI\wwwroot\app.css` is empty.

## 2026-05-27 - Scenario MQTT publisher naming alignment

- Aligned the test/scenario MQTT publish action with the normal pipeline component vocabulary:
  - `ScenarioStepTypes.MqttPublisher` is now the canonical scenario action id, with value `mqtt.publisher`.
  - `ScenarioStepCatalog` exposes the palette/editor descriptor as `mqtt.publisher` and labels it `MQTT publisher`.
  - The existing `MqttPublishScenarioStepRunner` keeps the implementation but now registers as `mqtt.publisher`.
  - Composer/catalog defaults use `mqtt.publisher`.
- Updated `C:\Users\meisa\OneDrive\Documents\FluxMQ\app1.json` so `t1.publishSampleRequest` now uses the canonical `mqtt.publisher` step type.
- Left `app.css` untouched.
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~ScenarioStepCatalogTests|FullyQualifiedName~FlowDefinitionComposerTests|FullyQualifiedName~FlowWorkspaceServiceTests|FullyQualifiedName~ScenarioRunReportFormatterTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqScenarioPublisherAliasUi\ -m:1` passes with 122 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test tests\FluxMq.Pipeline.Tests\FluxMq.Pipeline.Tests.csproj --no-restore --filter "FullyQualifiedName~ScenarioRunnerTests|FullyQualifiedName~FlowApplicationDefinitionJsonTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqScenarioPublisherAliasPipeline\ -m:1` passes with 22 tests.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore --filter "FullyQualifiedName~FlowApplicationHostTests|FullyQualifiedName~MqttScenarioClientFactoryTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqScenarioPublisherAliasApp2\ -m:1` passes with 14 tests.
  - `dotnet run --project src\FluxMq.Cli\FluxMq.Cli.csproj --no-restore -- validate --config C:\Users\meisa\OneDrive\Documents\FluxMQ\app1.json` reports `Flow application is valid. Workflows: 2. Resources: 2.`

## 2026-05-27 - Blank stored-session source no longer factory-fails

- Fixed the stored/replay source blank configuration path:
  - `session.source` and `replay.source` now build an empty MQTT source when `sessionId` is blank, so a newly added designer node no longer fails runtime factory creation before a session is selected.
  - Non-empty invalid session ids still fail with the explicit GUID validation error.
  - new `session.source` nodes now get the same small default configuration shape as `replay.source`: `sessionId`, timing settings, and `boundedCapacity`.
- Removed the scenario-step `mqtt.publish` compatibility alias because FluxMQ is still pre-release and new/test files should use the clean `mqtt.publisher` id only.
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~FlowDefinitionComposerTests|FullyQualifiedName~ScenarioStepCatalogTests|FullyQualifiedName~FlowWorkspaceServiceTests|FullyQualifiedName~ScenarioRunReportFormatterTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqStoredSourceFixUi\ -m:1` passes with 121 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore --filter "FullyQualifiedName~PipelineComponentFactoryTests|FullyQualifiedName~FlowApplicationHostTests|FullyQualifiedName~MqttScenarioClientFactoryTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqStoredSourceFixApp\ -m:1` passes with 37 tests.
  - `dotnet test tests\FluxMq.Pipeline.Tests\FluxMq.Pipeline.Tests.csproj --no-restore --filter "FullyQualifiedName~ScenarioRunnerTests|FullyQualifiedName~FlowApplicationDefinitionJsonTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqStoredSourceFixPipeline\ -m:1` passes with 21 tests.
  - `dotnet run --project src\FluxMq.Cli\FluxMq.Cli.csproj --no-restore -- validate --config C:\Users\meisa\OneDrive\Documents\FluxMQ\app1.json` reports `Flow application is valid. Workflows: 2. Resources: 2.`
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqStoredSourceFixAll\ -m:1` passes with 459 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-28 - FluxMQ app branding assets

- Replaced the old arrow-like shell mark and basic generated app resources with MQTT-flow themed SVG branding:
  - native MAUI app icon and foreground icon
  - native static splash SVG
  - reusable web brand mark, wordmark, animated loader SVG, and SVG favicon
- Wired the brand assets into the app:
  - top bar uses the SVG mark instead of CSS-drawn arrow geometry
  - empty workspace states use the SVG mark
  - Blazor WebView startup screen uses the animated loader and wordmark
  - `MauiIcon` and `MauiSplashScreen` use the updated resources and dark app background color
- Added a short in-app startup overlay so the splash remains visible for about 1.1 seconds, then fades out while the app shell is already rendered underneath.
- Verified:
  - all new/updated SVG files parse as XML.
  - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1` passes. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - inspected MAUI-generated app icon and splash PNG outputs from `obj\Debug\net10.0-windows10.0.19041.0\win-x64\resizetizer`.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqBrandingUi\ -m:1` passes with 180 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqSplashHoldUi\ -m:1` passes with 180 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `git diff --check` passes; Git reports only existing LF-to-CRLF working-copy warnings.

## 2026-05-28 - Live MQTT client terminology alignment

- Renamed the live MQTT runtime layer from session wording to client wording:
  - `FluxMq.Core.Session` moved to `FluxMq.Core.Mqtt`
  - `IMqttSession` -> `IFluxMqttClient`
  - `MqttSession` -> `FluxMqttClient`
  - `MqttSessionState` -> `MqttClientState`
  - `SessionStateChangedEventArgs` -> `MqttClientStateChangedEventArgs`
- Updated live MQTT owner APIs:
  - `IMqttConnectionManager.Sessions` is now `Clients`
  - `MqttConnectionComponent.Session` is now `Client`
  - runtime/UI factory parameters use `clientFactory`
- Kept stored/recorded session vocabulary intact for `StoredSession`, `SessionId`, `session.source`, replay, and recording features.
- Updated app/docs/memory wording so live broker resources are described as MQTT clients and recorded traffic remains described as sessions.
- Verified:
  - `dotnet build FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1` passes. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - focused core/components/app/UI MQTT tests pass: 8 core, 21 components, 37 app, and 60 UI tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqMqttClientNamingFull\ -m:1` passes with 459 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-28 - Scenario MQTT publish step reuses normal publisher component

- Started the next test-runner-as-pipeline refactor in the smallest useful place:
  - `mqtt.publisher` scenario/test steps now resolve a short-lived runner-owned MQTT client through `IMqttScenarioClientFactory`
  - the step publishes by sending a `MqttPublishRequest` through the normal `MqttPublisherComponent`
  - publish component errors are collected and returned as failed scenario step results
- Removed the duplicate scenario-only publishing service path:
  - `IMqttScenarioPublisher`
  - `ApplicationDefinitionMqttScenarioPublisher`
  - `RuntimeMqttScenarioPublisher`
- Scenario service setup now provides the MQTT client factory only; app, CLI, and UI scenario runs all use the same publish component path.
- This keeps the existing `expect.event` behavior intact while moving scenario execution toward normal pipeline composition with narrow test-specific blocks like `expect` and `when`.
- Verified:
  - `dotnet build FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1` passes. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore --filter "FullyQualifiedName~MqttScenarioClientFactoryTests|FullyQualifiedName~FlowApplicationHostTests.RunScenarioAsync" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqScenarioPublisherComponentApp\ -m:1` passes with 8 tests.
  - `dotnet test tests\FluxMq.Cli.Tests\FluxMq.Cli.Tests.csproj --no-restore --filter "FullyQualifiedName~CliRunnerTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqScenarioPublisherComponentCli\ -m:1` passes with 13 tests.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~FlowWorkspaceServiceTests|FullyQualifiedName~ScenarioRunReportFormatterTests|FullyQualifiedName~ScenarioStepCatalogTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqScenarioPublisherComponentUi\ -m:1` passes with 56 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqScenarioPublisherComponentFull\ -m:1` passes with 459 tests.

## 2026-05-28 - Pre-release component alias cleanup

- Cleaned project-local generated build output with `dotnet clean FluxMq.sln`.
- Removed pre-release compatibility node ids from the runtime/UI surface:
  - direct runtime registration for `mqtt.metrics-sink`
  - direct runtime registration for request mapper node ids `mqtt.publish-request`, `mqtt.recording-request`, and `file.write-request`
  - hidden UI catalog and widget/model aliases for those ids
- Kept the request mapper component classes because `flow.mapper` still uses them internally for typed outputs such as `MqttPublishRequest`, `MqttRecordingRequest`, and `FileWriteRequest`.
- Updated `C:\Users\meisa\OneDrive\Documents\FluxMQ\app1.json` so sample metrics nodes use canonical `mqtt.metrics`.
- Cleaned stale live-client wording in memory:
  - reconnect notes refer to `OnClientStateChanged` and `client.ConnectAsync`
  - the active test-runner resource-boundary note says `mqtt.publisher` uses `MqttPublisherComponent`, not the removed `IMqttScenarioPublisher`
- Verified:
  - `dotnet build FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1` passes. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore --filter "FullyQualifiedName~PipelineComponentFactoryTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqAliasCleanupApp\ -m:1` passes with 23 tests.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~FlowDefinitionComposerTests|FullyQualifiedName~FlowDiagramNodeModelTests|FullyQualifiedName~FlowWorkspaceServiceTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqAliasCleanupUi\ -m:1` passes with 129 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet run --project src\FluxMq.Cli\FluxMq.Cli.csproj --no-restore -- validate --config C:\Users\meisa\OneDrive\Documents\FluxMQ\app1.json` reports `Flow application is valid. Workflows: 2. Resources: 2.`
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqAliasCleanupFull\ -m:1` passes with 459 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-28 - Scenario/report review robustness follow-up

- Addressed remaining small robustness items from `report.md`:
  - `ScenarioEventJournal.WaitForMatchAsync` now cancels its internal timeout delay when a match, completion, or cancellation ends the wait early.
  - scenario waits explicitly check cancellation each loop iteration and still return a canceled step result through `ScenarioRunner`.
  - runtime projection refresh notifications now catch subscriber failures inside the fire-and-forget task, keeping the notification throttle healthy.
  - test-runner MQTT client ids now keep the full `fluxmq-test-{Guid:N}` value instead of truncating to 23 chars.
- Verified:
  - `dotnet test tests\FluxMq.Pipeline.Tests\FluxMq.Pipeline.Tests.csproj --no-restore --filter "FullyQualifiedName~ScenarioRunnerTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqScenarioJournalRobustnessPipeline\ -m:1` passes with 11 tests.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore --filter "FullyQualifiedName~MqttScenarioClientFactoryTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqScenarioClientIdApp\ -m:1` passes with 3 tests.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~FlowWorkspaceServiceTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqProjectionNotifyUi\ -m:1` passes with 50 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqReviewRobustnessFull\ -m:1` passes with 460 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-28 - Connection state trigger is buildable

- Fixed the dangling `mqtt.connection-state-trigger` surface:
  - the UI catalog exposed the component, but the runtime factory registry did not build it.
  - the node now binds to an app-level `mqtt.connection` resource through `configuration.connection`, matching the app resource ownership direction.
  - the UI catalog no longer advertises a graph `Connection` input port for it; broker choice is configuration/resource based.
- Extended `ConnectionStateTriggerComponent` so it can listen to either an `IMqttConnectionManager` or a specific `IFluxMqttClient`.
- Updated the composer so newly added connection-state trigger nodes get a default connection resource reference.
- Verified:
  - `dotnet test tests\FluxMq.Components.Tests\FluxMq.Components.Tests.csproj --no-restore --filter "FullyQualifiedName~ConnectionStateTriggerComponentTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqConnectionStateTriggerComponents\ -m:1` passes with 3 tests.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore --filter "FullyQualifiedName~PipelineComponentFactoryTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqConnectionStateTriggerApp\ -m:1` passes with 24 tests.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~FlowDefinitionComposerTests|FullyQualifiedName~FlowDiagramNodeModelTests|FullyQualifiedName~SourceNodeModelTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqConnectionStateTriggerUi\ -m:1` passes with 86 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqConnectionStateTriggerFull\ -m:1` passes with 463 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-28 - Designer catalog/runtime guard

- Added a UI regression test that checks every designer catalog component type has a registered runtime factory.
- This protects against the same class of failure where a node is draggable in the designer but cannot be built or run.
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~FlowDefinitionComposerTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqCatalogRuntimeGuardUi\ -m:1` passes with 67 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-28 - Scenario step catalog/runner guard

- Exposed the app default scenario-step runner registry from `FlowApplicationHost`.
- Added a UI regression test that checks every scenario step shown in the test designer palette has an application runner.
- This protects against showing test steps such as `mqtt.publisher` or `expect.event` that the app scenario runner cannot execute.
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~ScenarioStepCatalogTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqScenarioStepCatalogRunnerGuardUi\ -m:1` passes with 5 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore --filter "FullyQualifiedName~FlowApplicationHostTests|FullyQualifiedName~MqttScenarioClientFactoryTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqScenarioStepCatalogRunnerGuardApp\ -m:1` passes with 14 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqScenarioStepCatalogRunnerGuardFull\ -m:1` passes with 465 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-28 - Explicit scenario runner registries

- Removed the hidden event-expectation-only default from `ScenarioRunner`; callers must pass the runner registry they intend to use.
- Renamed the pipeline helper registry to `CreateEventExpectationOnly()` so it does not compete with the app default scenario runner registry.
- App/UI/CLI paths still use `FlowApplicationHost.CreateDefaultScenarioRunner()`, which includes both `expect.event` and `mqtt.publisher`.
- Verified:
  - `dotnet test tests\FluxMq.Pipeline.Tests\FluxMq.Pipeline.Tests.csproj --no-restore --filter "FullyQualifiedName~ScenarioRunnerTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqExplicitScenarioRunnerPipeline\ -m:1` passes with 11 tests.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore --filter "FullyQualifiedName~FlowApplicationHostTests|FullyQualifiedName~MqttScenarioClientFactoryTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqExplicitScenarioRunnerApp\ -m:1` passes with 14 tests.
  - `dotnet test tests\FluxMq.Cli.Tests\FluxMq.Cli.Tests.csproj --no-restore --filter "FullyQualifiedName~CliRunnerTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqExplicitScenarioRunnerCli\ -m:1` passes with 13 tests.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~ScenarioStepCatalogTests|FullyQualifiedName~FlowWorkspaceServiceTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqExplicitScenarioRunnerUi\ -m:1` passes with 55 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqExplicitScenarioRunnerFull\ -m:1` passes with 465 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-28 - Scenario step type validation

- Added the canonical known scenario step type set to `ScenarioStepTypes`.
- Application validation now rejects unknown test step types with `UnknownScenarioStepType` instead of allowing them to fail later at scenario execution time.
- Extended the scenario step catalog guard so the validation known-step list, app runner registry, and UI test-step palette must stay aligned.
- This keeps saved JSON honest while the test runner moves toward normal pipeline-style composition with a small set of explicit test blocks.
- Verified:
  - `dotnet test tests\FluxMq.Pipeline.Tests\FluxMq.Pipeline.Tests.csproj --no-restore --filter "FullyQualifiedName~FlowApplicationDefinitionValidatorTests|FullyQualifiedName~ScenarioRunnerTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqScenarioStepTypeValidationPipeline\ -m:1` passes with 25 tests.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore --filter "FullyQualifiedName~FlowApplicationConfigurationLoaderTests|FullyQualifiedName~FlowApplicationHostTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqScenarioStepTypeValidationApp\ -m:1` passes with 14 tests.
  - `dotnet test tests\FluxMq.Cli.Tests\FluxMq.Cli.Tests.csproj --no-restore --filter "FullyQualifiedName~CliRunnerTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqScenarioStepTypeValidationCli\ -m:1` passes with 13 tests.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~ScenarioStepCatalogTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqScenarioStepTypeAlignmentUi\ -m:1` passes with 5 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqScenarioStepTypeValidationFull\ -m:1` passes with 466 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-28 - Scenario step configuration validation

- Moved scenario/test step validation another notch earlier:
  - `mqtt.publisher` steps must now reference an app-level `mqtt.connection` resource and provide a valid topic.
  - `mqtt.publisher` QoS, retain, payload encoding, base64 payloads, and byte payloads are validated before runtime build.
  - `expect.event` string filters, timeout, and attribute filter shapes are validated before scenario execution.
- Added `ScenarioStepConfigurationKeys` so pipeline runners, app runners, validation, and UI catalog use the same saved JSON field names.
- Missing scenario MQTT resources now fail app start/build validation instead of surfacing later as a scenario-run failure.
- Verified:
  - `dotnet test tests\FluxMq.Pipeline.Tests\FluxMq.Pipeline.Tests.csproj --no-restore --filter "FullyQualifiedName~FlowApplicationDefinitionValidatorTests|FullyQualifiedName~ScenarioRunnerTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqScenarioConfigValidationPipeline2\ -m:1` passes with 29 tests.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore --filter "FullyQualifiedName~FlowApplicationConfigurationLoaderTests|FullyQualifiedName~FlowApplicationHostTests|FullyQualifiedName~MqttScenarioClientFactoryTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqScenarioConfigValidationApp4\ -m:1` passes with 17 tests.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~ScenarioStepCatalogTests|FullyQualifiedName~FlowDefinitionComposerTests|FullyQualifiedName~FlowWorkspaceServiceTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqScenarioConfigValidationUi\ -m:1` passes with 122 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet run --project src\FluxMq.Cli\FluxMq.Cli.csproj --no-restore -- validate --config C:\Users\meisa\OneDrive\Documents\FluxMQ\app1.json` reports `Flow application is valid. Workflows: 2. Resources: 2.`
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqScenarioConfigValidationFull\ -m:1` passes with 470 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-28 - Runner-owned scenario events foundation

- Added a small event-journal hook so scenario step runners can append runner-owned `FlowEvent` entries into the same stream that `expect.event` already observes.
- This is the foundation for future test-runner-as-pipeline steps such as a runner-owned `mqtt.trigger`: the trigger can reuse normal component/runtime behavior, append received events to the scenario journal, and then downstream `expect.event`/`when` steps can operate on those events.
- Recorded the future UI-portability rule: keep reusable app/test/dashboard workflow state and orchestration out of Razor-specific components so a later Avalonia/Linux UI can reuse the same logic.
- Verified:
  - `dotnet test tests\FluxMq.Pipeline.Tests\FluxMq.Pipeline.Tests.csproj --no-restore --filter "FullyQualifiedName~ScenarioRunnerTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqScenarioRunnerOwnedEventsPipeline\ -m:1` passes with 12 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqScenarioRunnerOwnedEventsFull\ -m:1` passes with 471 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-28 - Scenario run lifetime resources

- Added `ScenarioRunLifetime` and exposed it through `ScenarioStepRunContext`.
- Scenario step runners can now register `IDisposable`, `IAsyncDisposable`, or async cleanup callbacks that live beyond the current step and are disposed when the scenario ends.
- The runner disposes lifetime resources after both passed and failed runs. Cleanup failures are reported as a synthetic failed `scenario.cleanup` result instead of escaping as unstructured exceptions.
- This keeps the path clear for a future runner-owned `mqtt.trigger` step that starts a normal trigger component, keeps it alive while later `expect.event` or `when` steps run, and stops it deterministically at scenario completion.
- Verified:
  - `dotnet test tests\FluxMq.Pipeline.Tests\FluxMq.Pipeline.Tests.csproj --no-restore --filter "FullyQualifiedName~ScenarioRunnerTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqScenarioRunLifetimePipeline2\ -m:1` passes with 15 tests.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore --filter "FullyQualifiedName~FlowApplicationHostTests|FullyQualifiedName~MqttScenarioClientFactoryTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqScenarioRunLifetimeApp\ -m:1` passes with 14 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqScenarioRunLifetimeFull\ -m:1` passes with 474 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-28 - Scenario-owned MQTT trigger step

- Added `mqtt.trigger` as a first-class scenario/test step, matching the normal pipeline component vocabulary.
- The scenario trigger uses app-level `mqtt.connection` resources through the existing `IMqttScenarioClientFactory`, creates a separate runner-owned MQTT client, starts the normal `MqttConnectionComponent` plus `MqttTriggerComponent`, and registers the runtime with `ScenarioRunLifetime`.
- Trigger-emitted `mqtt.message.received` events are appended into the scenario journal, so later `expect.event` steps can observe messages received by the runner-owned trigger without adding a special MQTT expectation side channel.
- Scenario validation, the app default scenario runner registry, the UI step catalog, and the scenario step editor now understand `mqtt.trigger` configuration: connection, topic filter, QoS, receive retained, and retain-as-published.
- Verified:
  - `dotnet test tests\FluxMq.Pipeline.Tests\FluxMq.Pipeline.Tests.csproj --no-restore --filter "FullyQualifiedName~FlowApplicationDefinitionValidatorTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqScenarioMqttTriggerPipeline\ -m:1` passes with 20 tests.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore --filter "FullyQualifiedName~MqttScenarioClientFactoryTests|FullyQualifiedName~FlowApplicationHostTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqScenarioMqttTriggerApp3\ -m:1` passes with 15 tests.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~ScenarioStepCatalogTests|FullyQualifiedName~FlowDefinitionComposerTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqScenarioMqttTriggerUi\ -m:1` passes with 74 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test tests\FluxMq.Cli.Tests\FluxMq.Cli.Tests.csproj --no-restore --filter "FullyQualifiedName~CliRunnerTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqScenarioMqttTriggerCli\ -m:1` passes with 13 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqScenarioMqttTriggerFull\ -m:1` passes with 479 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-31 - CLI runner-owned scenario event source guard

- Relaxed the CLI scenario guard so `expect.event` can run when a prior scenario-owned event source has started.
- The first supported runner-owned event source is `mqtt.trigger`, so CLI scenarios can now run `mqtt.trigger` followed by `expect.event` without attaching to an app runtime event stream.
- A bare `expect.event`, or an expectation before any runner-owned event source, still fails fast with guidance to add a scenario `mqtt.trigger` or run against an app runtime through the UI/host API.
- Verified:
  - `dotnet test tests\FluxMq.Cli.Tests\FluxMq.Cli.Tests.csproj --no-restore --filter "FullyQualifiedName~CliRunnerTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqScenarioCliRunnerOwnedEvents\ -m:1` passes with 14 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqScenarioCliRunnerOwnedEventsFull\ -m:1` passes with 480 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-31 - Scenario when event condition step

- Added `when.event` as the first narrow test-specific condition step.
- `when.event` reuses the same `FlowEventExpectation` filters as `expect.event` and the same MudBlazor event-filter editor path in the test designer.
- When the configured event matches, the step passes and the scenario continues. When it does not match before timeout, the step is marked `Skipped`, the remaining flat scenario steps are not executed, and the overall run remains successful.
- Extracted shared event expectation reading and timeout/observation messages so `expect.event` and `when.event` stay aligned.
- Scenario reports now count skipped steps separately and do not treat skipped `when.event` guards as issues.
- Validation, app runner registration, the test-step catalog, and alignment guard tests now include `when.event`.
- Verified:
  - `dotnet test tests\FluxMq.Pipeline.Tests\FluxMq.Pipeline.Tests.csproj --no-restore --filter "FullyQualifiedName~ScenarioRunnerTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqWhenEventPipeline2\ -m:1` passes with 17 tests.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~ScenarioStepCatalogTests|FullyQualifiedName~ScenarioRunReportFormatterTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqWhenEventUi\ -m:1` passes with 9 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqWhenEventFull\ -m:1` passes with 483 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-31 - Scenario publisher events enter the runner journal

- `mqtt.publisher` scenario steps now append the normal `MqttPublisherComponent` `mqtt.message.published` event into the scenario event journal.
- CLI scenarios can now run `mqtt.publisher` followed by `expect.event` without a separate `mqtt.trigger`, because the publisher action is also a runner-owned event source.
- The CLI fast-fail guidance now says to add a scenario `mqtt.publisher` or `mqtt.trigger` before expectations that are not attached to an app runtime stream.
- Verified:
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore --filter "FullyQualifiedName~MqttScenarioClientFactoryTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqPublisherEventsApp\ -m:1` passes with 4 tests.
  - `dotnet test tests\FluxMq.Cli.Tests\FluxMq.Cli.Tests.csproj --no-restore --filter "FullyQualifiedName~CliRunnerTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqPublisherEventsCli3\ -m:1` passes with 15 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqPublisherEventsFull\ -m:1` passes with 484 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-31 - CLI event-observer scenario guard

- Tightened the CLI scenario fast-fail guard so both `expect.event` and `when.event` require a prior runner-owned event source.
- A CLI scenario that starts with `when.event` now fails validation with guidance to add a scenario `mqtt.publisher` or `mqtt.trigger`, instead of silently skipping because no event stream is attached.
- Added positive CLI coverage showing `mqtt.publisher` followed by `when.event` passes when the guard matches the publisher event.
- Moved the event-source requirement into a shared scenario helper and applied it to desktop test runs that are not attached to a running app runtime.
- A desktop test run with a leading `when.event` now fails immediately with a `RunFailed` diagnostic instead of waiting for the step timeout.
- Added positive desktop coverage showing an isolated `mqtt.publisher` step can still feed a following `when.event` through the scenario journal without starting the app runtime.
- Scenario setup failures in the desktop test runner no longer mark the app runtime state as faulted; they stay scoped to scenario diagnostics/logs.
- Added CLI and desktop coverage for the skipped guard path: `mqtt.publisher` followed by a non-matching `when.event` keeps the scenario successful, reports the guard as skipped, and does not run later planned steps.
- Added direct pipeline tests for the shared event-source requirement helper so the rule is owned by the scenario layer, not only by CLI/UI callers.
- Verified:
  - `dotnet test tests\FluxMq.Pipeline.Tests\FluxMq.Pipeline.Tests.csproj --no-restore --filter "FullyQualifiedName~ScenarioEventSourceRequirementsTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqScenarioEventSourceRequirementsPipeline\ -m:1` passes with 9 tests.
  - `dotnet test tests\FluxMq.Cli.Tests\FluxMq.Cli.Tests.csproj --no-restore --filter "FullyQualifiedName~CliRunnerTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqWhenSkipCli\ -m:1` passes with 18 tests.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~FlowWorkspaceServiceTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqWhenSkipUi\ -m:1` passes with 53 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqWhenSkipFull\ -m:1` passes with 498 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-31 - Scenario timeout wording follows runner-owned events

- Updated event expectation timeout wording after the runner-owned MQTT source refactor.
- Timeout diagnostics now describe observed scenario/app events generically instead of assuming every observed event came from the app runtime.
- `mqtt.message.published` timeout guidance now names both valid sources: a scenario `mqtt.publisher` event or a running app MQTT publisher node event.
- Updated scenario step catalog descriptions so `expect.event` and `when.event` describe matching scenario or app events, not only app runtime events.
- Verified:
  - `dotnet test tests\FluxMq.Pipeline.Tests\FluxMq.Pipeline.Tests.csproj --no-restore --filter "FullyQualifiedName~ScenarioRunnerTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqExpectationMessagePipeline\ -m:1` passes with 17 tests.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~ScenarioStepCatalogTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqScenarioCatalogTextUi\ -m:1` passes with 6 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqExpectationMessageFull\ -m:1` passes with 499 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-31 - Scenario-owned events mirror to scoped logs

- Added a small scenario event observer hook so runner-owned events appended directly into the scenario journal can also be observed by the desktop workspace.
- Source/app events that arrive through the scenario event stream still stay on the existing app runtime event path; only runner-owned events notify the scenario observer.
- Desktop test runs now mirror runner-owned MQTT events into Logs with `Test runner` scope and `Test` artifact metadata, without adding those events to the app/dashboard runtime event stream.
- Verified:
  - `dotnet test tests\FluxMq.Pipeline.Tests\FluxMq.Pipeline.Tests.csproj --no-restore --filter "FullyQualifiedName~ScenarioEventJournalTests|FullyQualifiedName~ScenarioRunnerTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqScenarioEventObserverPipeline\ -m:1` passes with 18 tests.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~FlowWorkspaceServiceTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqScenarioEventObserverUi\ -m:1` passes with 53 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqScenarioEventObserverFull\ -m:1` passes with 500 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-31 - MQTT client naming cleanup in tests

- Replaced remaining live MQTT fake-client variable names that used `session` in component, app-runtime, app-host, and UI workspace tests with `mqttClient`.
- Renamed the component test helper file from `TestMqttSession.cs` to `TestMqttBrokerClient.cs`.
- Left stored recording session language and `session.source` component ids unchanged because those refer to persisted message sessions, not live MQTT clients.
- Verified:
  - `dotnet test tests\FluxMq.Components.Tests\FluxMq.Components.Tests.csproj --no-restore --filter "FullyQualifiedName~MqttConnectionComponentTests|FullyQualifiedName~MqttPublisherComponentTests|FullyQualifiedName~MqttTriggerComponentTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqMqttClientNamingComponents\ -m:1` passes with 19 tests.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore --filter "FullyQualifiedName~FlowApplicationHostTests|FullyQualifiedName~PipelineComponentFactoryTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqMqttClientNamingApp\ -m:1` passes with 35 tests.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~FlowWorkspaceServiceTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqMqttClientNamingUi\ -m:1` passes with 53 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-31 - Core MQTT broker client naming

- Renamed the shared live MQTT client contract from `IFluxMqttClient` to `IMqttBrokerClient`.
- Renamed the concrete MQTTnet wrapper from `FluxMqttClient` to `MqttBrokerClient`, leaving MQTTnet's own `IMqttClient` name unshadowed.
- Updated app/runtime/scenario/UI factories, components, and tests to use the new broker-client vocabulary.
- Renamed live fake/test MQTT clients to match the same naming direction while leaving persisted stored-session types unchanged.
- Verified:
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqMqttBrokerClientRenameFull\ -m:1` passes with 500 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-31 - Scenario test designer display states

- Moved scenario step card display logic into `ScenarioStepDisplay` so summary text, CSS classes, and configuration labels are testable outside the Razor component.
- Added first-class test-card styling and summary formatting for scenario-owned `mqtt.trigger` steps.
- Added a distinct display state for `when.event` guards and for skipped guard results, keeping skipped guards visibly successful rather than generic or error-colored.
- Updated trigger configuration labels such as `topic filter`, `receive retained`, `retain as published`, and `QoS` in the test card details.
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~ScenarioStepDisplayTests|FullyQualifiedName~ScenarioStepCatalogTests|FullyQualifiedName~FlowDefinitionComposerTests|FullyQualifiedName~ScenarioRunReportFormatterTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqScenarioStepDisplayUi\ -m:1` passes with 85 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqScenarioStepDisplayUiFull\ -m:1` passes with 197 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-31 - Diagram link factory cleanup

- Removed the remaining `NotImplementedException` from the flow designer link factory.
- Unexpected Blazor.Diagrams link source shapes now fail with a clear invalid-operation diagnostic that names the unsupported source type.
- Verified:
  - `rg -n "NotImplementedException|throw new NotImplemented" src tests -g "*.cs" -g "*.razor"` finds no matches.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~FlowDiagramNodeModelTests|FullyQualifiedName~ActorNodeModelTests|FullyQualifiedName~SourceNodeModelTests" -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqDiagramPlaceholderUi\ -m:1` passes with 25 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqDiagramPlaceholderUiFull\ -m:1` passes with 197 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-31 - Active planning language sync

- Updated the active feature list and roadmap to use `Core MQTT Client`, `IMqttBrokerClient`, and `MqttBrokerClient` language after the broker-client rename.
- Updated the visual diagram acceptance note so pre-release development avoids saved-project compatibility aliases by default.

## 2026-05-31 - Dynamic mapper configuration cleanup

- Removed pre-release per-field mapper configuration from runtime dynamic mapper creation.
- `flow.mapper` now requires `expression` for `MqttPublishRequest`, `MqttRecordingRequest`, and `FileWriteRequest` outputs.
- Removed the recording mapper constructor path that hid `SessionId` in mapper configuration; recording request mappers now emit a full `MqttRecordingRequest`.
- Removed the `mapper` engine alias so saved mapper configuration uses the single `engine` key.
- Removed the publish mapper shortcut that converted an envelope directly into a publish request outside an explicit mapper.
- Updated definition validator tests so MQTT app nodes use normal workflow inputs rather than old resource-port examples.
- Removed the unused required-session-id runtime helper left after the mapper cleanup.
- Verified:
  - `dotnet test FluxMq.sln --no-restore /p:UseSharedCompilation=false /p:UseAppHost=false /p:BaseOutputPath="$env:TEMP\FluxMqMapperCleanFull6\" -m:1 -v minimal` passes with 511 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test tests\FluxMq.Pipeline.Tests\FluxMq.Pipeline.Tests.csproj --no-restore /p:UseSharedCompilation=false /p:UseAppHost=false /p:BaseOutputPath="$env:TEMP\FluxMqValidatorShapePipeline\" -m:1 -v minimal` passes with 95 tests.

## 2026-05-31 - Scenario event step field catalog

- Moved `when.event` and `expect.event` editable field keys/defaults into `ScenarioStepCatalog`, matching the catalog-owned shape already used by `mqtt.publisher` and `mqtt.trigger`.
- Kept the MudBlazor scenario editor behavior intact while replacing raw event-step key strings with shared scenario-step constants.
- Changed scenario-step creation so default configuration for every known test step comes from the catalog instead of a separate composer branch.
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqScenarioEventFieldsFocused2\ -m:1 --filter "FullyQualifiedName~ScenarioStepCatalogTests|FullyQualifiedName~FlowDefinitionComposerTests"` passes with 76 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:BaseOutputPath=$env:TEMP\FluxMqScenarioEventFieldsUi\ -m:1` passes with 199 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-31 - Engine package migration start

- Started `work/engine-package-migration` after locally fast-forwarding `main` with the completed scenario event-field catalog slice.
- Added the `FluxFlow.Engine` `0.3.0-alpha.1` package to `FluxMq.App` as the migration target.
- Added a FluxMQ-owned workspace definition that keeps dashboards and tests in FluxMQ while projecting only executable resources and workflows into the engine definition.
- Verified:
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 -v minimal` passes with 47 tests.

## 2026-05-31 - FluxMQ runtime constant ownership

- Added FluxMQ-owned event type and node type catalogs outside the old pipeline layer.
- Updated components, app runtime registration, UI catalogs, and affected tests to use the FluxMQ-owned constants.
- Left the old pipeline constants in place only for the old pipeline project and its own tests while the package migration is still in progress.
- Verified:
  - `dotnet test tests\FluxMq.Components.Tests\FluxMq.Components.Tests.csproj -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 -v minimal` passes with 115 tests.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 -v minimal` passes with 47 tests.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 -v minimal` passes with 199 tests. The pass still prints existing WinAppSDK PRI qualifier warnings.

## 2026-05-31 - Workspace dashboard and test definitions

- Added app-owned dashboard and test definition records for the FluxMQ workspace document.
- Removed old pipeline definition imports from the package-backed workspace definition and serializer options.
- Kept the old pipeline definition records untouched for the old runtime and scenario runner until those paths migrate.
- Verified:
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore --filter "FullyQualifiedName~FluxMqApplicationDefinitionTests" -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 -v minimal` passes with 2 tests.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 -v minimal` passes with 47 tests.

## 2026-05-31 - Engine package mapping boundary

- Switched app, component, UI preview, and component test mapping consumers from the old pipeline mapping namespace to the engine package mapping namespace.
- Added the engine package reference directly to `FluxMq.Components` because components now compile against package mapping contracts.
- Left old pipeline mapping code in place only for the old pipeline project and its own tests during migration.
- Verified:
  - `dotnet test tests\FluxMq.Components.Tests\FluxMq.Components.Tests.csproj -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 -v minimal` passes with 115 tests.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 -v minimal` passes with 47 tests.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~DynamicMapperWorkbenchPreview|FullyQualifiedName~FlowWorkspaceServiceTests" -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 -v minimal` passes with 63 tests.

## 2026-05-31 - Engine package flow event boundary

- Switched app, component, CLI, UI, scenario, and old runtime consumers from the old pipeline flow component namespace to the engine package flow component namespace.
- Mapped the package event `Channel` field into existing MQTT topic UI/log/report surfaces while keeping user-facing labels as MQTT topic.
- Added engine package references and aliases where needed so live flow node ids now come from the package event/component contract.
- Left the old pipeline component files in place for now; they are no longer imported by FluxMQ consumers.
- Verified:
  - `dotnet test FluxMq.sln -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 -v minimal` passes with 515 tests.

## 2026-05-31 - Remove duplicate pipeline component and mapping code

- Deleted the old pipeline `Components` and `Mapping` folders after moving FluxMQ consumers to the engine package contracts.
- Removed old mapping package references from `FluxMq.Pipeline`.
- Removed the `FluxMq.Components` project dependency on `FluxMq.Pipeline`.
- Kept a small scenario-owned event type catalog for scenario definitions that still live in the pipeline project.
- Verified:
  - `dotnet test FluxMq.sln -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 -v minimal` passes with 515 tests.

## 2026-05-31 - Engine package runtime cleanup

- Moved FluxMQ workspace validation into `FluxMq.App`, keeping dashboards and tests as app-owned artifacts while projecting only resources and workflows into the package engine definition.
- Replaced the local pipeline runtime/definition copy with the `FluxFlow.Engine` package runtime and definition contracts across app, CLI, UI, components, and tests.
- Moved scenario/test primitives into the dedicated `FluxMq.Scenarios` project so tests stay outside the engine package and outside production workflow runtime contracts.
- Removed the unused local `MqttPipeline` prototype and stale scaffold test after confirming nothing outside its own tests referenced it.
- Preserved app workspace JSON and validation coverage under `FluxMq.App.Tests`; removed local engine-level tests now owned by the package project.
- Net cleanup from the runtime-copy removal and prototype cleanup removed 3,881 lines before the scenario-project rename.
- Verified:
  - `dotnet build FluxMq.sln -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 -v minimal` passes. The pass still prints existing WinAppSDK PRI qualifier warnings.
  - `dotnet test FluxMq.sln -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 -v minimal` passes with 478 tests.

## 2026-05-31 - Engine package conditional links

- Upgraded `FluxFlow.Engine` references in app, component, and scenario projects to `0.4.0-alpha.1`.
- Added a FluxMQ runtime regression test proving package-owned conditional links route one MQTT source to separate sinks through per-link `when` expressions.
- Kept conditional routing at the engine link level, so FluxMQ can remove bespoke router pressure over time instead of duplicating graph semantics.
- Verified:
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore --filter "FullyQualifiedName~ApplicationRuntimeBuilder_RoutesConditionalLinks" -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 -v minimal` passes with 1 test.
  - `dotnet test FluxMq.sln -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 -v minimal` passes with 479 tests.

## 2026-05-31 - Package migration documentation cleanup

- Removed stale local build-output folders for the retired pipeline/replay/storage test projects.
- Updated README and docs to describe the current package-backed runtime boundary, FluxMQ-owned app document shape, scenario project, and active solution layout.
- Replaced stale MQTT client and local pipeline references in documentation with the current `IMqttBrokerClient`, `FluxFlow.Engine`, and `FluxMq.Scenarios` names.

## 2026-05-31 - CI runtime event projection wait

- Tightened the mapped-publisher scenario UI test to wait for the asynchronous runtime event projection before asserting the runtime event list.
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~RunActiveTestScenarioAsync_CanObserveMappedPublisherEventFromAppFlow" -p:RuntimeIdentifierOverride=win-x64 -p:RuntimeIdentifier=win-x64 -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 -v minimal` passes with 1 test.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --configuration Release --no-restore -p:RuntimeIdentifierOverride=win-x64 -p:RuntimeIdentifier=win-x64 -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 -v minimal` passes with 199 tests.

## 2026-05-31 - Engine package 0.5 update

- Upgraded `FluxFlow.Engine` references in app, component, and scenario projects to `0.5.0-alpha.1`.
- Verified:
  - `dotnet build FluxMq.sln -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 -v minimal` passes.
  - `dotnet test FluxMq.sln --no-build -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 -v minimal` passes with 479 tests.
  - `dotnet test FluxMq.sln --configuration Release --no-restore --verbosity minimal -p:RuntimeIdentifierOverride=win-x64 -p:RuntimeIdentifier=win-x64 -p:UseSharedCompilation=false -p:UseAppHost=false -m:1` passes with 479 tests.

## 2026-05-31 - Conditional link designer support

- Added workspace/composer support for setting and clearing per-link `when` expressions without rewriting unrelated links.
- The flow designer now preserves conditional link objects when rebuilding the canvas and highlights conditional links with the existing warning color treatment.
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~FlowDefinitionComposerTests" -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 -v minimal` passes with 71 tests.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 -v minimal` passes with 202 tests.

## 2026-05-31 - Conditional link designer editor

- Added a selected-link editor on the workflow canvas for reading, applying, and clearing per-link `when` expressions.
- Kept the UI to MudBlazor field and icon controls with small local layout CSS only.
- Added composer coverage for reading conditional and unconditional link conditions.
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~FlowDefinitionComposerTests" -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 -v minimal` passes with 73 tests.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 -v minimal` passes with 204 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 -v minimal` passes with 484 tests.

## 2026-05-31 - Scenario editor catalog option cleanup

- Removed duplicate finite option lists from the scenario step editor dialog.
- The editor now reads publish, trigger, and event expectation options from `ScenarioStepCatalog` field descriptors.
- Added catalog coverage for event expectation QoS and retain option values.
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~ScenarioStepCatalogTests" -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 -v minimal` passes with 8 tests.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 -v minimal` passes with 204 tests.

## 2026-05-31 - Scenario card catalog display cleanup

- Scenario test cards now read configuration labels from `ScenarioStepCatalog` field descriptors instead of a local label switch.
- Visible configuration rows follow catalog field order first, with unknown custom fields appended after known fields.
- Added coverage for catalog-owned labels, event attribute labels, unknown attribute fallbacks, and display ordering.
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~ScenarioStepDisplayTests" -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 -v minimal` passes with 11 tests.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 -v minimal` passes with 207 tests.

## 2026-05-31 - Scenario report text catalog labels

- Readable scenario report text now formats step configuration through `ScenarioStepCatalog`, so Summary exports use the same labels and field order as test cards.
- Report JSON remains machine-readable with saved configuration keys unchanged.
- The report dialog passes the injected scenario catalog into text generation instead of relying only on raw report data.
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~ScenarioRunReportFormatterTests|FullyQualifiedName~ScenarioStepDisplayTests" -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 -v minimal` passes with 14 tests.

## 2026-05-31 - Scenario field metadata layer split

- Moved neutral scenario step field metadata types into `FluxMq.Scenarios`.
- Kept UI-only scenario descriptor concerns, such as icons and editor kind, in `FluxMq.UI`.
- Moved the field option normalization test to the scenario test project so the lower layer owns its own contract.
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~ScenarioStepCatalogTests|FullyQualifiedName~ScenarioStepDisplayTests|FullyQualifiedName~ScenarioRunReportFormatterTests" -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 -v minimal` passes with 22 tests.

## 2026-05-31 - Scenario definition catalog layer split

- Moved scenario step definitions, field defaults, finite options, and attribute filter key helpers into `FluxMq.Scenarios`.
- The UI scenario catalog now wraps the lower definition catalog and only adds UI metadata such as icons and editor kind.
- Dashboard event filter attribute helpers now delegate to the shared scenario configuration key helpers, keeping attribute filter keys consistent across dashboards, reports, and tests.
- Added scenario-layer coverage for known definitions, defaults, option values, fallback definitions, and attribute filter keys.
- Verified:
  - `dotnet test tests\FluxMq.Scenarios.Tests\FluxMq.Scenarios.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 -v minimal` passes with 35 tests.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 -v minimal` passes with 206 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 -v minimal` passes with 494 tests.

## 2026-05-31 - Scenario validation catalog alignment

- App scenario validation now uses `ScenarioStepDefinitionCatalog` to resolve supported step types.
- Payload encoding and QoS validation now read finite allowed values from the scenario definition catalog instead of maintaining separate validator lists/ranges.
- Added validator coverage for rejecting a payload encoding outside the catalog option set.
- Hardened LiteDB mapper registration so parallel test/application contexts do not re-register shared custom id mappings while another context is serializing storage entities.
- Verified:
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore --filter "FullyQualifiedName~FluxMqApplicationDefinitionValidatorTests" -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 -v minimal` passes with 21 tests.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 -v minimal` passes with 80 tests.
  - `dotnet test tests\FluxMq.Components.Tests\FluxMq.Components.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 -v minimal` passes with 115 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 -v minimal` passes with 495 tests.

## 2026-05-31 - Scenario runner catalog guard split

- Moved the app default scenario runner coverage guard from UI tests into `FluxMq.App.Tests`.
- The app-layer guard now compares `FlowApplicationHost.CreateDefaultScenarioStepRunnerRegistry()` with `ScenarioStepDefinitionCatalog.Shared`, so executable scenario support is checked where runner ownership lives.
- The UI scenario catalog test now stays focused on palette and editor descriptors.
- Verified:
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore --filter "FullyQualifiedName~CreateDefaultScenarioStepRunnerRegistry_CoversKnownScenarioDefinitions" -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 -v minimal` passes with 1 test.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~ScenarioStepCatalogTests" -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 -v minimal` passes with 6 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 -v minimal` passes with 495 tests.

## 2026-05-31 - Scenario step type set cleanup

- Removed the unused `ScenarioStepTypes.All` aggregate after runner, validation, and UI checks moved to `ScenarioStepDefinitionCatalog`.
- Scenario step type constants remain the stable identifiers; the catalog is now the single source for the supported step list.
- Verified:
  - `dotnet test tests\FluxMq.Scenarios.Tests\FluxMq.Scenarios.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 -v minimal` passes with 35 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 -v minimal` passes with 495 tests.

## 2026-05-31 - Scenario event source wording cleanup

- Reworded the isolated scenario event-stream diagnostic from internal runner ownership language to `scenario event source`.
- Renamed the shared requirement helper's private terms and affected tests to the same language.
- Verified:
  - `dotnet test tests\FluxMq.Scenarios.Tests\FluxMq.Scenarios.Tests.csproj --no-restore --filter "FullyQualifiedName~ScenarioEventSourceRequirementsTests" -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 -v minimal` passes with 9 tests.
  - `dotnet test tests\FluxMq.Cli.Tests\FluxMq.Cli.Tests.csproj --no-restore --filter "FullyQualifiedName~RunAsync_ReturnsValidationErrorWhenCliScenario" -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 -v minimal` passes with 2 tests.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~RunActiveTestScenarioAsync_FailsFastWhenEventObserverHasNoEventSource" -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 -v minimal` passes with 1 test.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 -v minimal` passes with 495 tests.

## 2026-05-31 - MQTT component package migration

- Added the `FluxFlow.Components.Mqtt` `0.1.0-alpha.1` package to `FluxMq.Components`.
- Reworked `MqttPublisherComponent` to delegate publish execution to the package `mqtt.publish` node while preserving FluxMQ's existing `mqtt.publisher` input, log entry, event, and dashboard-facing state.
- Reworked `MqttTriggerComponent` to delegate subscriptions to package `mqtt.subscribe` nodes while preserving FluxMQ's app-level connection resource, `MqttEnvelope` output, receive events, and retained-message options.
- Added a small adapter from `IMqttBrokerClient` and shared connection streams into the package MQTT contracts, keeping app files and UI node names stable while moving the duplicated MQTT node execution logic out of FluxMQ.
- Updated durable architecture/component docs to describe the package-backed MQTT component boundary.
- Verified:
  - `dotnet test tests\FluxMq.Components.Tests\FluxMq.Components.Tests.csproj --no-restore` passes with 115 tests.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore` passes with 81 tests.
  - `dotnet test tests\FluxMq.Scenarios.Tests\FluxMq.Scenarios.Tests.csproj --no-restore` passes with 35 tests.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore` passes with 205 tests.
  - `dotnet test tests\FluxMq.Cli.Tests\FluxMq.Cli.Tests.csproj --no-restore` passes with 18 tests.
  - `dotnet test tests\FluxMq.Core.Tests\FluxMq.Core.Tests.csproj --no-restore` passes with 41 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 -v minimal` passes with 495 tests.

## 2026-05-31 - MQTT component package 0.2 update

- Updated `FluxFlow.Components.Mqtt` to `0.2.0-alpha.1`.
- Switched the FluxMQ adapter to the package's factory context and explicit `MqttClientLease.Shared` ownership model.
- Moved retained subscription options into the package `mqtt.subscribe` configuration instead of resolving them back from FluxMQ subscription records inside the adapter.
- Used package publish-result payload previews, removing the temporary pending-request dictionary from the FluxMQ publisher wrapper.
- Verified:
  - `dotnet test tests\FluxMq.Components.Tests\FluxMq.Components.Tests.csproj --no-restore` passes with 115 tests.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore` passes with 81 tests.
  - `dotnet test tests\FluxMq.Scenarios.Tests\FluxMq.Scenarios.Tests.csproj --no-restore` passes with 35 tests.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore` passes with 205 tests.
  - `dotnet test tests\FluxMq.Cli.Tests\FluxMq.Cli.Tests.csproj --no-restore` passes with 18 tests.
  - `dotnet test tests\FluxMq.Core.Tests\FluxMq.Core.Tests.csproj --no-restore` passes with 41 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 -v minimal` passes with 495 tests.

## 2026-05-31 - MQTT 0.2.1 and mapping package migration

- Updated `FluxFlow.Components.Mqtt` to `0.2.1-alpha.1`.
- Added `FluxFlow.Components.Mapping` `0.1.0-alpha.1` and registered the package-backed `flow.mapper` runtime node.
- Added a FluxMQ request mapping expression adapter so JSONata and Dynamic Expresso mapper expressions still coerce to `MqttPublishRequest`, `MqttRecordingRequest`, and `FileWriteRequest` with FluxMQ-friendly fields such as `qos`.
- Removed the old request-specific mapper Dataflow nodes, mapper definitions, and component tests from FluxMQ.
- Updated the mapper workbench preview to use the same FluxMQ request mapping adapter as runtime execution.
- Verified:
  - `dotnet test FluxMq.sln -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 --nologo` passes with 494 tests.

## 2026-06-01 - Control component package migration

- Added `FluxFlow.Components.Control` `0.1.0-alpha.1` to the app/runtime component projects.
- Shifted the pre-release control node vocabulary to package ids: `flow.filter`, `flow.when`, and `flow.assert`.
- Reworked FluxMQ control wrappers so package nodes own expression evaluation while FluxMQ keeps product-specific pass-count activity, route log entries, assertion log entries, and assertion events.
- Removed the old local predicate, filter, router, and assertion expression implementations from FluxMQ.
- Updated docs, current memory, designer catalogs, node models, and tests to the new control ids.
- Updated the local sample app file at `C:\Users\meisa\OneDrive\Documents\FluxMQ\app1.json` from `mqtt.condition-router` to `flow.when`.
- Verified:
  - `dotnet test FluxMq.sln -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 --nologo -v minimal` passes with 488 tests.

## 2026-06-01 - Validation component package migration

- Added `FluxFlow.Components.Validation` `0.1.0-alpha.1` to `FluxMq.Components`.
- Reworked `json.schema-validator` so package code owns schema loading, schema-path handling, value selection, and validation evaluation.
- Kept a small FluxMQ adapter for MQTT payload text selection, `JsonSchemaValidationResult` projection, invalid-payload issue wording, and `json.schema.validated` events.
- Removed FluxMQ's direct `JsonSchema.Net` dependency from the component project.
- Updated docs and memory notes to describe the package-backed validation boundary.
- Verified:
  - `dotnet test tests\FluxMq.Components.Tests\FluxMq.Components.Tests.csproj --no-restore --filter "FullyQualifiedName~JsonSchemaValidatorComponentTests" -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 -v normal` passes with 4 tests.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore --filter "FullyQualifiedName~JsonSchemaValidatorFactory" -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 -v minimal` passes with 2 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 --nologo -v minimal` passes with 488 tests.

## 2026-06-01 - File system component package migration

- Added `FluxFlow.Components.FileSystem` `0.1.0-alpha.1` to `FluxMq.Components`; later updated it to `0.4.0-alpha.1`.
- Reworked `file.writer` so package code owns file path validation, directory creation, write modes, byte writing, and package error codes.
- Kept a small FluxMQ adapter for the existing `FileWriteRequest` mapper target, `file.writer` actor id, and `file.written` event projection.
- Updated docs and memory notes to describe the package-backed file writer boundary.
- Verified:
  - `dotnet test tests\FluxMq.Components.Tests\FluxMq.Components.Tests.csproj -p:UseSharedCompilation=false -p:UseAppHost=false --nologo -v minimal --filter FileWriterComponentTests` passes with 3 tests.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false --nologo -v minimal` passes with 81 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 --nologo -v minimal` passes with 488 tests.

## 2026-06-01 - Component package update pass

- Checked prerelease updates from the package feed for the FluxFlow package references.
- Updated `FluxFlow.Components.FileSystem` from `0.1.0-alpha.1` to `0.4.0-alpha.1`.
- Confirmed the existing `file.writer` adapter remains compatible with the package-owned `file.write` API.
- Verified:
  - `dotnet build .\FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 --nologo -v minimal` passes.
  - `dotnet test tests\FluxMq.Components.Tests\FluxMq.Components.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false --nologo -v minimal --filter FileWriterComponentTests` passes with 3 tests.
  - `dotnet test .\FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 --nologo -v minimal` passes with 488 tests.

## 2026-06-01 - Observability component package migration

- Added `FluxFlow.Components.Observability` `0.1.0-alpha.1` to `FluxMq.Components`.
- Reworked `flow.logger` so package code owns neutral log-entry creation and selector handling.
- Kept a small FluxMQ adapter for the existing MQTT message log shape, `FlowError` log shape, recent-entry buffer, and workspace-facing `FlowLogEntry` contract.
- Left `mqtt.metrics` in FluxMQ for now because it still owns MQTT-specific topic counts, retained-message counts, payload-byte summaries, and rolling-window rate projections.
- Verified:
  - `dotnet test tests\FluxMq.Components.Tests\FluxMq.Components.Tests.csproj -p:UseSharedCompilation=false -p:UseAppHost=false --nologo -v minimal --filter FlowLoggerComponentTests` passes with 4 tests.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj -p:UseSharedCompilation=false -p:UseAppHost=false --nologo -v minimal --filter "FullyQualifiedName~FlowLogger"` passes with 1 test.
  - `dotnet test tests\FluxMq.Components.Tests\FluxMq.Components.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false --nologo -v minimal` passes with 108 tests.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false --nologo -v minimal` passes with 81 tests.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj -p:UseSharedCompilation=false -p:UseAppHost=false --nologo -v minimal` passes with 205 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 --nologo -v minimal` passes with 488 tests.

## 2026-06-01 - Conditional link designer visuals

- Added a small `FlowLinkVisuals` helper for workflow link colors, selected color, conditional width, and compact condition labels.
- Pipeline designer links with per-link `when` expressions now render a compact `when: ...` label directly on the link.
- Selected links now use an explicit selected color plus stronger SVG styling so selected state is visible even when the link already has a condition color.
- Wired the generated Windows `appicon.ico` into the MAUI Windows lifecycle so the native window uses the FluxMQ app icon instead of the generic host icon.
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter FlowDiagramNodeModelTests -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 --nologo -v minimal` passes with 19 tests.
  - `dotnet build FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 --nologo -v minimal` passes.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 --nologo -v minimal` passes with 237 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 --nologo -v minimal` passes with 534 tests.
  - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 --nologo -v minimal` passes.

## 2026-06-01 - Sessions component package migration

- Added `FluxFlow.Components.Sessions` `0.1.0-alpha.1` to `FluxMq.Components`.
- Added `FluxMqSessionStore` as the adapter between local stored MQTT messages and the shared session contracts.
- Reworked `MqttRecorderComponent` and `StoredSessionSourceComponent` to depend on `ISessionStore`; the existing `IMessageRepository` constructor path now wraps the repository in `FluxMqSessionStore`.
- Collapsed `replay.source` onto the stored-session source path with timing preservation enabled, then removed the old local replay source class, replay factory, replay options, and their duplicate tests.
- Updated replay docs and active dashboard memory so future dashboard/source work follows the shared session-store path.
- Verified:
  - `dotnet test tests\FluxMq.Components.Tests\FluxMq.Components.Tests.csproj --filter "FluxMqSessionStoreTests|MqttRecorderComponentTests|MqttSourceComponentTests" -p:UseSharedCompilation=false -p:UseAppHost=false --nologo -v minimal` passes with 12 tests.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --filter "StoredSessionSourceFactory|ReplaySourceFactory|StoredSourceFactories" -p:UseSharedCompilation=false -p:UseAppHost=false --nologo -v minimal` passes with 6 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 --nologo -v minimal` passes with 485 tests.

## 2026-06-01 - Metrics component package migration

- Added `FluxFlow.Components.Metrics` `0.1.0-alpha.1` to `FluxMq.Components`.
- Reworked `mqtt.metrics` so the FluxMQ component adapts `MqttEnvelope` values into package metric samples and projects package snapshots back into `MqttMetricsSnapshot`.
- Kept MQTT-specific retained-message counting and idle rolling-rate refresh in the wrapper because the package aggregate is transport-neutral and emits snapshots on metric sample changes.
- Verified:
  - `dotnet test tests\FluxMq.Components.Tests\FluxMq.Components.Tests.csproj --filter MqttMetricsComponentTests -p:UseSharedCompilation=false -p:UseAppHost=false --nologo -v minimal` passes with 6 tests.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --filter "FullyQualifiedName~MqttMetrics" -p:UseSharedCompilation=false -p:UseAppHost=false --nologo -v minimal` passes with 1 test.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 --nologo -v minimal` passes with 485 tests.

## 2026-06-01 - Payload component package migration

- Added `FluxFlow.Components.Payloads` `0.1.0-alpha.1` to `FluxMq.Components`.
- Reworked `mqtt.payload-inspector` so package code owns neutral payload classification, JSON/XML formatting, base64 detection, text preview, and binary detection.
- Kept a small FluxMQ adapter for the existing `MqttEnvelope` input, `InspectedMqttMessage` output, Core payload result projection, and hex dump display shape.
- Verified:
  - `dotnet test tests\FluxMq.Components.Tests\FluxMq.Components.Tests.csproj --filter PayloadInspectorMapperComponentTests -p:UseSharedCompilation=false -p:UseAppHost=false --nologo -v minimal` passes with 11 tests.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --filter "FullyQualifiedName~PayloadInspectorFactory" -p:UseSharedCompilation=false -p:UseAppHost=false --nologo -v minimal` passes with 2 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 --nologo -v minimal` passes with 493 tests.

## 2026-06-01 - Timer component package registration

- Added `FluxFlow.Components.Timers` `0.4.1-alpha.1` to the runtime/component projects.
- Registered package-backed `timer.interval`, `timer.schedule`, and `timer.delay` nodes.
- Added designer catalog entries, typed timer node models, and a native timer node editor for interval, schedule, and delay configuration.
- Added `TimerTick` and `ScheduleTick` mapper/assertion aliases plus timer-specific mapping contexts so timer ticks can drive `flow.mapper` and `mqtt.publisher` without an MQTT envelope input.
- Updated `flow.mapper` defaults to preserve timer input types and generate timer-friendly publish/file expressions.
- Verified:
  - `dotnet build .\FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 --nologo -v minimal` passes.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false --nologo -v minimal --filter "FullyQualifiedName~PipelineComponentFactoryTests"` passes with 32 tests.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false --nologo -v minimal --filter "FullyQualifiedName~FlowDefinitionComposerTests|FullyQualifiedName~TimerNodeModelTests|FullyQualifiedName~FlowDiagramNodeModelTests"` passes with 107 tests.
  - `dotnet test .\FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 --nologo -v minimal` passes with 517 tests.

## 2026-06-01 - HTTP and payload component package registration

- Added `FluxFlow.Components.Http` `0.1.0-alpha.1` and `FluxFlow.Components.Payloads` `0.1.0-alpha.1`.
- Registered package-backed `http.request` and `payload.inspect` runtime nodes.
- Added mapper aliases/coercion for `HttpRequestInput`, `HttpResponseOutput`, `HttpErrorOutput`, `PayloadInspectionRequest`, and `PayloadInspectionResult`.
- Added designer catalog entries, typed node models, and node editors for HTTP request options and payload inspection options.
- Kept the existing MQTT-specific payload inspector as a separate MQTT envelope projection while adding the generic package-backed payload node.
- Verified:
  - `dotnet build .\FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 --nologo -v minimal` passes.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore --filter "FullyQualifiedName~PipelineComponentFactoryTests" -p:UseSharedCompilation=false -p:UseAppHost=false --nologo -v minimal` passes with 35 tests.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~FlowDefinitionComposerTests|FullyQualifiedName~TimerNodeModelTests|FullyQualifiedName~FlowDiagramNodeModelTests" -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 --nologo -v minimal` passes with 110 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 --nologo -v minimal` passes with 523 tests.

## 2026-06-01 - State component package registration

- Added `FluxFlow.Components.State` `0.1.0-alpha.1` to the runtime/component projects.
- Registered package-backed `state.reducer` runtime nodes.
- Added mapper aliases/coercion for `StateReducerInput` and editor defaults so MQTT envelopes can be mapped into reducer inputs explicitly.
- Added designer catalog entries, typed state reducer node models, and a node editor for reducer options.
- Verified:
  - `dotnet build FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 --nologo -v minimal` passes.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore --filter "FullyQualifiedName~PipelineComponentFactoryTests" -p:UseSharedCompilation=false -p:UseAppHost=false --nologo -v minimal` passes with 37 tests.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~FlowDefinitionComposerTests|FullyQualifiedName~TimerNodeModelTests|FullyQualifiedName~FlowDiagramNodeModelTests" -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 --nologo -v minimal` passes with 115 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 --nologo -v minimal` passes with 531 tests.

## 2026-06-01 - Assertion component prep

- Added shared assertion input type names so runtime factory support and the assertion node editor use the same source of truth.
- Added `StateReducerResult` as a supported `flow.assert` input type so state reducer outputs can be checked without waiting for a separate assertion package migration.
- Added state reducer result variables to the control expression context: `key`, `previousState`, `stateInput`, `newState`, `version`, and `updatedAt`.
- Verified:
  - `dotnet build FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 --nologo -v minimal` passes.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore --filter "FullyQualifiedName~PipelineComponentFactoryTests" -p:UseSharedCompilation=false -p:UseAppHost=false --nologo -v minimal` passes with 38 tests.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~FlowDiagramNodeModelTests" -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 --nologo -v minimal` passes with 20 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 --nologo -v minimal` passes with 536 tests.

## 2026-06-01 - Shared component version properties

- Added repo-level FluxFlow package version properties in `Directory.Build.props`.
- Updated runtime, component, and scenario project references to use those shared properties for FluxFlow packages.
- This keeps future component update slices to a single version edit before any required code changes.
- Verified:
  - `dotnet restore FluxMq.sln --nologo` passes.
  - `dotnet build FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 --nologo -v minimal` passes with existing UI project resource qualifier warnings.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 --nologo -v minimal` passes with 536 tests.

## 2026-06-02 - Sources component package migration

- Added `FluxFlow.Components.Sources` `0.1.0-alpha.1` to the app runtime project.
- Reworked FluxMQ `generated.source` runtime creation to use package-backed `GeneratedSourceNode<MqttEnvelope>`.
- Preserved the existing FluxMQ node type, `messages` configuration, and `Output`/`Errors` ports while adding package timing and bounded-loop options.
- Removed the old local generated MQTT source implementation.
- Kept `session.source` and `replay.source` on the existing session-store path because the sources package intentionally does not own stored session replay.
- Verified:
  - `dotnet restore FluxMq.sln --nologo` passes.
  - `dotnet test tests\FluxMq.Components.Tests\FluxMq.Components.Tests.csproj --no-restore --filter "FullyQualifiedName~StoredSessionSource" -p:UseSharedCompilation=false -p:UseAppHost=false --nologo -v minimal` passes with 3 tests.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore --filter "FullyQualifiedName~GeneratedSourceFactory" -p:UseSharedCompilation=false -p:UseAppHost=false --nologo -v minimal` passes with 2 tests.
  - `dotnet build FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 --nologo -v minimal` passes with existing UI project resource qualifier warnings.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 --nologo -v minimal` passes with 536 tests.

## 2026-06-02 - Assertions component package migration

- Added `FluxFlow.Components.Assertions` `0.1.0-alpha.1`.
- Updated `FluxFlow.Components.Control` to `0.2.0-alpha.1`.
- Reworked FluxMQ `flow.assert` execution to use the package-backed assertion node while preserving FluxMQ assertion result, log entry, and runtime event surfaces.
- Reused the existing FluxMQ expression context factory for assertion expressions so MQTT, file, HTTP, payload, timer, state, and error variables still resolve the same way.
- Updated assertion expression-failure tests to assert the package-owned assertion error code.
- Verified:
  - `dotnet restore FluxMq.sln --nologo` passes.
  - `dotnet test tests\FluxMq.Components.Tests\FluxMq.Components.Tests.csproj --no-restore --filter "FullyQualifiedName~FlowAssertionComponentTests" -p:UseSharedCompilation=false -p:UseAppHost=false --nologo -v minimal` passes with 3 tests.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore --filter "FullyQualifiedName~FlowAssertionFactory" -p:UseSharedCompilation=false -p:UseAppHost=false --nologo -v minimal` passes with 2 tests.
  - `dotnet test tests\FluxMq.Components.Tests\FluxMq.Components.Tests.csproj --no-restore --filter "FullyQualifiedName~FlowFilterComponentTests|FullyQualifiedName~FlowWhenComponentTests" -p:UseSharedCompilation=false -p:UseAppHost=false --nologo -v minimal` passes with 4 tests.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore --filter "FullyQualifiedName~ConditionRouterFactory|FullyQualifiedName~MessageFilter" -p:UseSharedCompilation=false -p:UseAppHost=false --nologo -v minimal` passes with 1 test.
  - `dotnet build FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 --nologo -v minimal` passes with existing UI project resource qualifier warnings.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 --nologo -v minimal` passes with 536 tests.

## 2026-06-02 - Storage component package registration

- Added `FluxFlow.Components.Storage` and `FluxFlow.Components.Storage.Local` `0.1.0-alpha.1`.
- Registered package-backed `storage.put`, `storage.get`, and `storage.delete` runtime nodes.
- Wired storage nodes to local file-backed storage with a configurable root directory and a default per-user app data root.
- Added runtime coverage that stores a record with `storage.put` and reads it back with `storage.get` through the package-backed local store.
- Kept designer/catalog work out of this slice; storage is now executable from definitions, and UI surfacing can be layered separately.
- Verified:
  - `dotnet restore FluxMq.sln --nologo` passes.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore --filter "FullyQualifiedName~StorageComponents" -p:UseSharedCompilation=false -p:UseAppHost=false --nologo -v minimal` passes with 1 test.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore --filter "FullyQualifiedName~RegisterPipelineComponentFactories_RegistersStableComponentTypes" -p:UseSharedCompilation=false -p:UseAppHost=false --nologo -v minimal` passes with 1 test.
  - `dotnet build FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 --nologo -v minimal` passes with existing UI project resource qualifier warnings.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 --nologo -v minimal` passes with 537 tests.

## 2026-06-02 - Routing correlation and join designer support

- Added designer and runtime coverage for package-backed `flow.correlation` and `flow.join`.
- Added node models, catalog entries, composer defaults, and MudBlazor editor widgets for both routing nodes.
- `flow.correlation` pairs request/response values by key and side expressions.
- `flow.join` pairs left/right streams by key expression and intentionally does not auto-wire a default single input.
- Verified:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false --nologo -v minimal` passes with 257 tests.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore --filter "FullyQualifiedName~PipelineComponentFactoryTests" -p:UseSharedCompilation=false -p:UseAppHost=false --nologo -v minimal` passes with 46 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false --nologo -v minimal` passes with 562 tests.

## 2026-06-02 - Storage file-system adapter package rename

- Replaced the broad `FluxFlow.Components.Storage.Local` adapter package with explicit `FluxFlow.Components.Storage.FileSystem` `0.1.0-alpha.1`.
- Updated the base storage package version to `FluxFlow.Components.Storage` `0.2.0-alpha.1`, which is required by the file-system adapter package.
- Updated runtime storage registration to `UseFileSystemStorage(...)` with `FileSystemStorageStoreOptions`.
- Renamed the app runtime root-directory parameter from local-storage wording to file-system-storage wording.
- Updated runtime registry coverage for the new `storage.query` component type registered by the updated storage package.
- Verified:
  - `dotnet restore FluxMq.sln --nologo` passes.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore --filter "FullyQualifiedName~PipelineComponentFactoryTests" -p:UseSharedCompilation=false -p:UseAppHost=false --nologo -v minimal` passes with 46 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false --nologo -v minimal` passes with 562 tests.

## 2026-06-02 - Dashboard event-rate widget

- Added `event.rate` to the dashboard widget catalog.
- Reused the existing dashboard event filters so rate widgets can target the same runtime events as counter/latest widgets.
- Extended dashboard event snapshots with recent event count, one-minute rate window, and events-per-second value.
- Rendered the rate widget as a compact live dashboard card with the same settings dialog path as existing event widgets.
- Verified:
  - `dotnet test .\tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --nologo` passes with 259 tests.
  - `dotnet test .\FluxMq.sln --nologo` passes with 564 tests.

## 2026-06-02 - Reconnect cancellation robustness

- Fixed a reconnect cancellation race in `MqttConnectionManager`.
- Cancel paths no longer dispose reconnect cancellation sources while reconnect tasks may still be using them.
- Reconnect tasks now remove only their own dictionary entry, so an older completed reconnect cannot remove a newer reconnect attempt for the same profile.
- Verified:
  - `dotnet test .\tests\FluxMq.Core.Tests\FluxMq.Core.Tests.csproj --configuration Release --no-restore -p:RuntimeIdentifierOverride=win-x64 -p:RuntimeIdentifier=win-x64 --nologo` passes with 41 tests.
  - `dotnet restore .\FluxMq.sln -p:RuntimeIdentifierOverride=win-x64 -p:RuntimeIdentifier=win-x64 --nologo` passes.
  - `dotnet test .\FluxMq.sln --configuration Release --no-restore --verbosity minimal -p:RuntimeIdentifierOverride=win-x64 -p:RuntimeIdentifier=win-x64 --nologo` passes with 564 tests.

## 2026-06-02 - Artifact-aware live side panel

- Made the live MQTT inspector/publisher panel available only for active pipeline artifacts.
- Hid the inspector toggle and right side panel for no-app, dashboard, test, and logs states so those surfaces use the full workspace width.
- Verified:
  - `dotnet test .\tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --nologo` passes with 259 tests.
  - `dotnet test .\FluxMq.sln --nologo` passes with 564 tests.

## 2026-06-02 - Release sample verification script

- Added `eng/verify-samples.ps1` for repeatable local sample checks.
- The script builds the CLI once, validates `metrics-only.json`, validates `generated-traffic-inspect.json`, and runs `generated-traffic-inspect.json` for a bounded duration without requiring a broker.
- Updated README and local development docs so the generated-traffic sample is the recommended runtime smoke path.
- Cleaned the MQTT publisher docs snippet to use `mqttClient` instead of the confusing old `session` variable name.
- Verified:
  - `.\eng\verify-samples.ps1` passes.

## 2026-06-02 - Release readiness checklist

- Added `docs/release-readiness.md` as the pre-V1 release gate.
- The checklist covers local command validation, broker-free sample verification, release-shaped Windows package validation, manual desktop UI checks, and blocker rules.
- Linked the checklist from the README and documentation index.

## 2026-06-02 - Current docs cleanup

- Updated durable docs so app definitions own resources, workflows, dashboards, and tests before executable resources/workflows are projected into the package runtime.
- Removed stale `FluxMq.Cli` planned-language from architecture docs.
- Updated bounded CLI runtime examples to use `generated-traffic-inspect.json` instead of the no-source `metrics-only.json` sample.

## 2026-06-02 - Faster Windows validation workflow

- Split the Windows workflow into an automatic validation job and a manual package job.
- Pull requests and `main` now run release-shaped Windows restore/tests without building or uploading package artifacts.
- Manual workflow dispatch still builds the portable zip and MSI when package artifacts are explicitly needed.
- Updated release-readiness and development docs to use the serialized Release test command and describe manual package generation.

## 2026-06-02 - Host-owned expression engine preparation

- Moved expression engine implementations into FluxMQ-owned mapping adapters.
- Passed the FluxMQ default expression engine to runtime build paths for link `when` conditions.
- Kept the shared workflow engine package on the current component-compatible version because the currently published component adapter packages still target that API.
- Verified `dotnet restore .\FluxMq.sln --nologo` passes.
- Verified `dotnet build .\FluxMq.sln --no-restore --nologo` passes.
- The newer shared workflow engine package migration remains blocked until consumed component adapter packages are published against the same node-id namespace.

## 2026-06-02 - Stable workflow package boundary

- Updated `FluxFlow.Engine` to `1.0.0`.
- Updated every consumed `FluxFlow.Components.*` package to the compatibility rebuild version, including the currently used observability and sources packages.
- Moved all remaining `FlowNodeId` aliases to `FluxFlow.Engine.Components.FlowNodeId`.
- Replaced test and mapper-preview references to removed engine-owned concrete expression engines with FluxMQ-owned expression adapters.
- Verified:
  - `dotnet restore .\FluxMq.sln --no-cache --force-evaluate --nologo` passes.
  - `dotnet build .\FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false --nologo -v minimal` passes with the existing WinAppSDK PRI qualifier warnings.
  - `dotnet test .\FluxMq.sln --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -m:1 --nologo -v minimal` passes with 565 tests.
  - `dotnet restore .\FluxMq.sln -p:RuntimeIdentifierOverride=win-x64 -p:RuntimeIdentifier=win-x64 --nologo` passes.
  - `dotnet test .\FluxMq.sln --configuration Release --no-restore --verbosity minimal -m:1 -p:RuntimeIdentifierOverride=win-x64 -p:RuntimeIdentifier=win-x64 -p:UseSharedCompilation=false --nologo` passes with 565 tests.

## 2026-06-02 - Faster pull request validation

- Kept the existing Windows validation check name, but split the commands by event type.
- Pull requests now run the faster Debug/no-RID/no-apphost restore and test path.
- Pushes to `main`, tags, and manual workflow dispatch still run the release-shaped `win-x64` validation path.
- Package-version property changes now trigger Windows validation.
- Restore/test steps now have focused timeouts so a stuck PR run fails quickly.
- Disabled NuGet XML documentation extraction in the workflow to trim package restore overhead.

## 2026-06-02 - Release-readiness command gate

- Ran the pre-V1 local command gate:
  - `dotnet restore .\FluxMq.sln --nologo` passed.
  - `dotnet test .\FluxMq.sln --no-restore --nologo -m:1 -p:UseSharedCompilation=false -p:UseAppHost=false --verbosity minimal` passed with 565 tests.
  - `.\eng\verify-samples.ps1` passed, including CLI build, metrics sample validation, generated-traffic sample validation, and bounded generated-traffic run.
- Ran the Windows package gate:
  - `dotnet restore .\FluxMq.sln -p:RuntimeIdentifierOverride=win-x64 -p:RuntimeIdentifier=win-x64 --nologo` passed.
  - `dotnet test .\FluxMq.sln --configuration Release --no-restore --verbosity minimal -m:1 -p:RuntimeIdentifierOverride=win-x64 -p:RuntimeIdentifier=win-x64 -p:UseSharedCompilation=false --nologo` passed with 565 tests and the existing WinAppSDK PRI qualifier warnings.
  - `.\eng\package-windows.ps1 -Configuration Release -Version 0.1.0` passed and produced the portable zip and MSI under `artifacts\windows\dist`.
- Updated release-readiness and development docs to describe the current fast pull-request validation path.

## 2026-06-02 - Manual packaged desktop smoke gate

- Launched the packaged portable desktop build from `artifacts\windows\portable\FluxMQ\FluxMq.UI.exe`.
- The app opened as `FluxMQ`; the manual shell check found no obvious release-blocking issue.
- Recorded that richer component configuration/edit views remain planned designer polish under `F-022`, but they are not treated as a release-readiness blocker unless candidate testing exposes a concrete failed workflow.

## 2026-06-02 - Candidate readiness recheck

- Confirmed the release package artifacts still exist:
  - `artifacts\windows\dist\FluxMQ-0.1.0-portable-win-x64.zip`
  - `artifacts\windows\dist\FluxMQ-0.1.0-win-x64.msi`
- Verified `git diff --check` has no whitespace errors.
- Verified `dotnet test .\FluxMq.sln --no-restore --nologo -m:1 -p:UseSharedCompilation=false -p:UseAppHost=false --verbosity minimal` passes with 565 tests.
- Verified `.\eng\verify-samples.ps1` passes, including CLI build, sample validation, and bounded generated-traffic execution.
- No concrete candidate blocker was found in this recheck.

## 2026-06-02 - V1 candidate notes

- Added `docs/v1-candidate-notes.md` as the current candidate handoff.
- The notes capture candidate status, validated command gates, expected local Windows artifacts, manual candidate focus, and rules for when to rerun the Windows package gate.
- Linked the candidate notes from the docs index and release-readiness checklist.
- Updated the living development plan so the next slice is focused candidate workflow testing and concrete blocker fixes.

## 2026-06-02 - Focused candidate workflow recheck

- Validated repo sample definitions through the CLI:
  - `samples\flow-applications\metrics-only.json`
  - `samples\flow-applications\generated-traffic-inspect.json`
- Validated `C:\Users\meisa\OneDrive\Documents\FluxMQ\app1.json`; it reports two workflows and two MQTT resources.
- Confirmed a local Mosquitto broker was listening on `localhost:1883`.
- Ran `app1.json` for a bounded duration through the CLI against the local broker.
- Verified the app1 MQTT pipeline externally: publishing `{"value":12,"unit":"c","status":"ok"}` to `fluxmq/sample/request` produced a mapped MQTT publish on topic `test`.
- No runtime or broker-backed candidate blocker was found. Remaining candidate checks are desktop-visual checks for dashboard layout/widget refresh, desktop test artifact execution, and Logs filtering.

## 2026-06-02 - Artifact deletion workflow

- Added workspace deletion operations for pipeline, dashboard, and test artifacts.
- Top artifact tabs now expose compact delete actions with confirmation before removal.
- The app structure menus now expose matching delete entries for pipelines, dashboards, and tests.
- Deleting the active artifact falls back to the next available artifact, and deleting a pipeline clears stored diagram positions for that pipeline.
- Verified:
  - `dotnet test .\tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false --nologo -v minimal` passes with 268 tests.

## 2026-06-07 - Dashboard metric visualization separation

- Added the first UI-only metric visualization module, `metric.value`, to keep metric query evaluation separate from widget presentation and cell styling.
- Routed KPI, event counter, event rate, and rate tile through a shared metric-value view so editor-cell, preview, and live paths stay aligned.
- Converted `event.rate` to the metric-query builder path with Rate as the only allowed measure; widget config no longer owns event filters/window/primary metric behavior.
- Kept future gauges/meters/digital readouts as separate visualization modules, not hidden modes inside `event.rate`.
- Follow-up: made KPI the first edit-view consumer of the visualization layer by adding a catalog-backed `Visualization` row and persisting `visualization = metric.value` in KPI defaults/config.
- Verified focused dashboard/composer tests after source-mode restore:
  - `dotnet restore FluxMq.sln -p:UseFluxFlowSourceReferences=true -p:FluxFlowSourceRoot=D:\Projects\FluxFlow\`
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseFluxFlowSourceReferences=true -p:FluxFlowSourceRoot=D:\Projects\FluxFlow\ -p:UseAppHost=false --filter "FullyQualifiedName~DashboardEventFilterCatalogTests|FullyQualifiedName~FlowDefinitionComposerTests|FullyQualifiedName~FlowDefinitionComposerV2Tests"`
- Final verification:
  - Source-mode UI tests passed with 371 tests.
  - Package-only UI tests passed with 371 tests.
  - Full solution tests passed on rerun after one timing-sensitive scenario test passed independently.
  - `git diff --check` passed; FluxFlow remained clean.
- KPI visualization follow-up verification:
  - Focused dashboard/composer tests passed with 184 tests.
  - Source-mode UI tests passed with 372 tests.
- The NuGet upgrade briefly exposed stale restore-mode CLI metadata while switching between source and package FluxFlow references; a full matching restore confirmed `run`, `validate`, and `scenario` should keep the public `ExecuteAsync` override used by the committed CLI package graph.
- Final verification after the CLI restore check:
  - CLI tests passed with 18 tests.
  - Full solution tests passed with Core 44, Scenarios 36, Components 113, App 108, CLI 18, and UI 372 tests.
  - `git diff --check` passed with line-ending warnings only; FluxFlow remained clean.
- KPI visualization editing now uses the selected visualization module as the source for follow-up property rows. `metric.value` owns the title/subtitle/value colors and title/value alignment/placement rows, while KPI keeps only widget text and metric binding concerns.
- After the package upgrade, source-reference and package-reference builds require their matching restore mode before `--no-restore`; otherwise stale assets can mix FluxFlow project and package references.
- Verified after the visualization-property refactor:
  - Source-mode dashboard tests passed with 66 focused tests.
  - Source-mode UI tests passed with 372 tests.
  - Default/package-mode app build passed for `src\fluxmq.ui` with no `InitializeComponent` error.
- Next step: visually review KPI `Visualization` editing first; if accepted, reuse the pattern for the next focused metric widget before adding a new visualization module.

## 2026-06-07 - KPI digital visualization slice

- Merged PR #167 and synced `main`; post-merge Windows validation passed and FluxFlow stayed clean.
- Started `work/dashboard-metric-digital-visualization` for the next focused dashboard slice.
- Added `metric.digital` as the first second visualization module for KPI:
  - KPI now routes through a visualization host instead of directly rendering `metric.value`.
  - `metric.digital` has its own renderer, defaults, supported metric kinds, and property rows.
  - Digital-specific settings are `Digit style` and `Glow`; they are saved only when the digital visualization is selected.
- Refined `metric.digital` into a reusable Blazor component:
  - Added `DashboardDigitalReadout` with simple parameters for value, label, accent color, style, glow, and minimum digits.
  - The visualization view now passes metric values into the component instead of owning the SVG drawing.
  - The readout uses SVG seven-segment paths so the digital visual can move toward a real meter/readout look without leaking implementation details into KPI.
- Expanded the digital visual controls:
  - Added readout-specific background, segment, inactive segment, label color, and digit count settings.
  - Hid the generic `Value color` row for `metric.digital`; `Segment color` is the digital value color.
  - These settings remain visualization-owned and are saved only when `metric.digital` is selected.
- Kept the separation intact:
  - Metric query still chooses the number.
  - Visualization chooses the presentation.
  - Cell style still owns container background, border, radius, padding, and shared colors.
- Verification:
  - Source-mode restore completed after the package update.
  - Source-mode UI tests passed with 373 tests.
  - Package-mode restore completed.
  - Package-mode UI tests passed with 373 tests.
  - After the reusable readout refinement, UI build passed and package-mode UI tests passed with 374 tests.
  - After the digital-control expansion, UI build passed and package-mode UI tests passed with 374 tests.
- Digital readout control fix:
  - Removed inactive decimal-point drawing so the seven-segment readout no longer shows stray small circles under every digit.
  - Extended the property-grid color picker to support alpha colors through `#RGBA`, `#RRGGBBAA`, `transparent`, and a compact alpha-percent field.
  - Updated dashboard widget setting normalization so alpha hex values are preserved when saved.
  - Verification passed with UI build and 375 package-mode UI tests.
- Next step: visually review KPI `Value` versus `Digital`; if accepted, add the next visualization or reuse the pattern on the next metric widget one slice at a time.

## 2026-06-07 - KPI visualization ownership refactor

- Refactored KPI visualization editing so the selected visual owns all inner display settings.
- Added `DashboardMetricVisualizationSettingsDraft` as the compatibility/draft boundary for visual-owned config.
- Replaced flattened KPI-specific visual rows in the inspector with:
  - `Visualization` select
  - selected visualization module property rows rendered inline in the property grid
- Removed the separate visualization editor popup after UI review; KPI visual settings stay directly editable in the inspector.
- Replaced the custom property-grid color picker internals with the UI framework color picker so alpha-capable colors are handled by the shared component layer.
- Moved saved Value visual settings to `metric.value.*` keys and Digital visual settings to `metric.digital.*` keys.
- Kept outer dashboard cell style focused on cell/container background, border, radius, padding, and layout only.
- Kept compatibility for old KPI keys (`title`, `subtitle`, `kpi.*`) when loading existing dashboards; applying visual settings rewrites to visual-owned keys.
- Verification so far:
  - `dotnet build src\fluxmq.ui --no-restore --verbosity minimal` passed.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 375 tests.
- Follow-up verification:
  - A normal app build reached the copy-to-output step but was blocked by the already-running desktop app locking `FluxMq.UI.exe`.
  - A separate-output UI build passed with no errors.
  - Separate-output UI tests passed with 375 tests.
- Compact color-row correction:
  - Restored the property-grid color row to the compact swatch/text/button layout.
  - Kept the alpha-capable framework color picker behind a popover instead of rendering a full picker input inside each property row.
  - Fixed dashboard edit-cell metric-value previews so top/middle/bottom value placement uses the same placement variable as live mode.
  - Verified with separate-output UI build and focused UI tests for color picker and edit-preview placement.
- Follow-up compact-control fix:
  - Shrank the property-grid palette action column.
  - Added outside-click auto-close for the color picker popover.
  - Fixed duplicate edit-cell headers for event counter, event rate, and rate tile by letting metric-value visuals own their header consistently.
  - Verified with separate-output UI build and focused UI tests for color picker and metric-value widget rendering.
- Metric-query row polish:
  - Removed the live preview value from the property-grid metric-query row so it shows only the query summary and edit action.
  - Restyled the metric-query edit action as a quiet borderless property-grid icon button.
  - Verification passed with separate-output UI build and the focused metric-query row UI test.
- Value visual unit control:
  - Made the `EVENTS` unit label part of the Value visual settings instead of hard-coded renderer behavior.
  - Added `Show unit`, `Unit text`, and `Unit color` settings under the selected Value visualization.
  - Kept natural metric units as the default fallback when `Unit text` is empty.
  - Separate-output UI build and focused UI tests passed after the change.
- Empty-cell shrink polish:
  - Added a bounded empty-cell placeholder wrapper in the dashboard editor grid.
  - Anchored empty-cell labels to the cell interior so merged/large cells do not place labels on visual seams when the grid is narrow.
  - Separate-output UI build and focused UI test passed.
- Cell widget alignment:
  - Added shared cell-level `Widget fit` and `Widget align` settings so the whole widget frame can be placed inside a dashboard cell.
  - Kept the default as stretch/fill; selecting an alignment dot switches the cell to content fit so the placement is visible immediately.
  - Added a compact 3x3 property-grid alignment pad and applied the same CSS variables to edit-cell previews and live dashboard cells.
  - Separate-output UI build and focused UI tests passed after the change.
- Padding and color ownership correction:
  - Split cell padding from widget padding: `Cell style > Padding` now pads the dashboard cell wrapper, not the widget content.
  - Added `Value visual > Padding` as visualization-owned widget padding for `metric.value`; existing `style.padding` still loads as a compatibility fallback.
  - Kept Digital visual padding owned by `metric.digital.padding`.
  - Fixed property-grid color swatches to render alpha-safe `rgba(...)` colors over a checker background so selected colors are visible in the compact rows.
  - Separate-output UI build and focused UI tests passed after the change.
- Responsive dashboard grid slice:
  - Added runtime responsive cell variables for column span, row span, tablet/mobile span, track padding, and responsive minimum height without changing dashboard V2 JSON.
  - Updated edit and live dashboard grids to use container-query reflow: desktop keeps the designed grid, tablet derives two columns, and mobile derives one column.
  - Added safe track minimums so fixed/star/percent tracks stop crushing widgets and scroll the dashboard frame when the available area is too small.
  - Made Value and Digital metric visuals use container-aware sizing so titles, values, units, and digital readouts adapt to narrow or short cells in edit and live views.
  - Normal UI build was blocked by the already-running desktop app locking output DLLs; separate-output UI build passed with no warnings or errors.
  - Focused dashboard UI tests passed after the change.
- Next step: visually review wide, medium, and narrow dashboard sizes with Value and Digital visuals; if approved, continue with KPI unit placement/case or the next Value visual layout polish.

## 2026-06-07 - Metric stream framework and pipeline integration

- Added the numeric metric stream contract `FluxMetricReading<TValue>` in the shared core layer so App, Components, UI, and tests can use the same reading type without creating a project reference cycle.
- Added focused app metric runtime classes for count, rate, unique topics, payload bytes, average payload, and retained count.
- Added `FluxMetricRuntimeHost` as a lifecycle/lookup host only; each metric class owns its own runtime-event subscription, filtering, windowing, and reading emission.
- Wired metric streams into `FlowApplicationHost` startup/shutdown and made dashboard metric value lookup prefer the latest app metric reading before falling back to offline/event-query evaluation.
- Added the `metric.source` pipeline component:
  - no input port
  - `Output` as `NumberMetricReading`
  - `Errors` as `FlowError`
  - config for metric id, parameter values, latest-on-start, and buffer size
- Added pipeline/UI support so condition routing, assertions, dynamic mapper previews, log shaping, and component metadata understand `NumberMetricReading`.
- Added a dedicated Metric Source diagram node editor that selects app metrics, sets parameter values, and shows latest stream state without touching the existing MQTT metrics observer node.
- Tightened dashboard metric migration so promoted app metric ids are idempotent (`ops.metric` does not become `ops.ops.metric`) and widget metric config is updated to the promoted app-level id.
- Verification:
  - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false -p:OutDir=artifacts\build-check\ui\` passed with 0 warnings.
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 113 tests.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 383 tests.
- Next step: run full solution verification and then review the Metric Source node UX in the desktop designer before moving on to the next dashboard metric consumer.

## 2026-06-07 - Metric/dashboard boundary cleanup

- Started the post-merge cleanup branch after the metric stream framework landed.
- Centralized dashboard-scoped metric id handling in `FluxMetricNaming` so app migration and UI composition share one policy.
- Kept dashboard metric promotion idempotent: `ops.metric` remains `ops.metric` and cannot become `ops.ops.metric`.
- Updated dashboard metric cleanup so unused dashboard-promoted app metrics can be removed after local dashboard `metrics` have already migrated away.
- Added regression coverage for scoped metric naming, double-scope prevention, and removal of unused promoted metrics.
- Verification so far:
  - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 114 tests.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 384 tests.
- Full cleanup-slice verification:
  - `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false` passed.
  - `git diff --check` passed.
- Follow-up metric bridge extraction:
  - Added `DashboardMetricValueBridge` so dashboard widget binding, app metric resolution, latest metric reading lookup, and offline fallback evaluation are no longer embedded directly in `FlowWorkspaceService`.
  - Kept `FlowWorkspaceService` responsible for runtime state, active layout access, runtime event snapshots, and host access.
  - Added a source-boundary regression test so the workspace service keeps delegating dashboard metric resolution to the bridge.
  - Verification:
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 385 tests.
    - `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false` passed with 710 tests.
    - `git diff --check` passed.
- Next step: the next cleanup target is the dashboard inspector/property row split.
- Dashboard inspector row split:
  - Extracted layout property rows into `DashboardInspectorLayoutRows`.
  - Extracted cell style property rows into `DashboardInspectorCellStyleRows`.
  - Kept `DashboardInspector` responsible for state, group composition, and commands while the focused row components own their own row markup.
  - Added source-boundary tests so the inspector keeps delegating layout/style rows to focused components.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal --filter "FullyQualifiedName~DashboardInspector"` passed with 5 tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 386 tests.
    - `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false` passed with 711 tests.
    - `git diff --check` passed.
- Next step: continue splitting dashboard inspector metric data rows next.
- Dashboard inspector metric row split:
  - Extracted app-level metric selection, generated metric parameter rows, preview, and open-metric action into `DashboardInspectorAppMetricRows`.
  - Extracted legacy/dashboard-local metric query editing into `DashboardInspectorMetricQueryRows` so the parent inspector no longer owns the compact query row markup.
  - Extracted metric binding and multi-series slot rows into `DashboardInspectorMetricBindingRows`.
  - Kept `DashboardInspector` focused on state, widget classification, auto-apply commands, and group composition.
  - Added source-boundary tests so the inspector delegates app metric rows, query rows, and binding rows to focused components.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal --filter "FullyQualifiedName~DashboardInspector"` passed with 6 tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 387 tests.
    - `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false` passed with 712 tests.
- Dashboard inspector event filter row split:
  - Extracted event/status/filter field rows into `DashboardInspectorEventFilterRows`.
  - Kept `DashboardInspector` responsible for draft mutation and metric-query synchronization while the focused component owns filter row markup.
  - Moved QoS/retain segmented filter rendering out of the parent inspector.
  - Added a source-boundary test so the inspector keeps delegating filter rows to the focused component.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal --filter "FullyQualifiedName~DashboardInspector"` passed with 7 tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 388 tests.
    - `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false` passed with 713 tests.
- Dashboard inspector metric visualization row split:
  - Extracted visualization selection and dynamic value/digital visualization property rows into `DashboardInspectorMetricVisualizationRows`.
  - Moved visualization property editor rendering, color picker/select/toggle/segmented handling, and alignment option lookup out of the parent inspector.
  - Kept `DashboardInspector` responsible for applying visualization config changes and composing property groups.
  - Added source-boundary tests so the inspector delegates visualization rows to the focused component.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal --filter "FullyQualifiedName~DashboardInspector"` passed with 8 tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 389 tests.
    - First full solution run hit a transient `JsonSchemaValidatorComponentTests.Input_RoutesValidAndInvalidEnvelopesToBranchOutputs` miss; the focused rerun passed.
    - `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false` passed on rerun with 714 tests.
- Dashboard inspector display row split:
  - Extracted visual metric/card rows into `DashboardInspectorVisualMetricRows`.
  - Extracted gauge, chart, and topic display-mode rows into `DashboardInspectorDisplayModeRows`.
  - Kept `DashboardInspector` responsible for state mutation and property-group composition while focused row components own their own markup.
  - Added source-boundary tests so the inspector delegates visual metric/card rows and display-mode rows to focused components.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal --filter "FullyQualifiedName~DashboardInspector"` passed with 10 tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 391 tests.
    - `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false` passed with 716 tests.
    - `git diff --check` passed.
- Dashboard inspector metric query option row split:
  - Extracted legacy aggregate/window option rows into `DashboardInspectorMetricQueryOptionRows`.
  - Kept `DashboardInspector` responsible for metric draft mutation and auto-apply.
  - Added a source-boundary test so aggregate/window row markup stays out of the parent inspector.
  - Verification:
    - Initial parallel build/test attempt hit a locked UI intermediate assembly; rerunning the build alone passed.
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal --filter "FullyQualifiedName~DashboardInspector"` passed with 11 tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 392 tests.
    - `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false` passed with 717 tests.
    - `git diff --check` passed.
- Next step: commit the focused metric query option-row split, then inspect the remaining dashboard inspector state-loading methods.
- Dashboard metric query mapper cleanup:
  - Moved dashboard query to `DashboardMetricSnapshot` conversion into `DashboardMetricQueryMapper`.
  - Removed the duplicate local snapshot mapping helper from `DashboardInspector`.
  - Added mapper and source-boundary tests so the inspector keeps delegating snapshot conversion.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal --filter "FullyQualifiedName~DashboardMetricQueryMapper|FullyQualifiedName~DashboardInspector"` passed with 14 tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 394 tests.
    - First full solution run hit a transient `ScenarioRunnerTests.RunAsync_SkipsRemainingStepsWhenWhenEventDoesNotMatch` assertion; the focused rerun passed.
    - `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false` passed on rerun with 719 tests.
- Next step: commit the metric mapper cleanup, then continue reducing the dashboard inspector state-loading path.
- Dashboard metric reference resolver cleanup:
  - Added `DashboardMetricReferenceResolver` as a thin UI-service adapter over app metric artifacts and `FluxMetricResolver`.
  - Moved app metric definition/snapshot resolution out of `DashboardInspector`.
  - Kept the inspector's artifact dictionary read only for metric selector options.
  - Added tests for parameterized app metric reference resolution and inspector boundary enforcement.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal --filter "FullyQualifiedName~DashboardMetricReferenceResolver|FullyQualifiedName~DashboardInspector"` passed with 13 tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 395 tests.
    - `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false` passed with 720 tests.
    - `git diff --check` passed.
- Next step: commit the metric reference resolver cleanup, then continue with a small dashboard inspector draft-loading extraction.
- Dashboard inspector metric binding state cleanup:
  - Added `DashboardInspectorMetricBindingState` to own metric binding list initialization and save-time normalization.
  - Removed inline initial binding list normalization and final binding list construction from `DashboardInspector`.
  - Kept add, remove, move, and primary metric commands in the inspector for now.
  - Added unit coverage for slot/non-slot binding normalization plus source-boundary coverage for inspector delegation.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal --filter "FullyQualifiedName~DashboardInspectorMetricBindingState|FullyQualifiedName~DashboardInspector"` passed with 15 tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 398 tests.
    - `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false` passed with 723 tests.
    - `git diff --check` passed.
- Next step: commit the metric binding state cleanup, then inspect whether `OnParametersSet` can be safely reduced without obscuring UI state.
