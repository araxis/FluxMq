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
- Started the next Fork Flow component after replay and publish support: live MQTT message source.
- Added `MqttMessageSourceComponent` to bridge `IMqttSession.Messages` into Dataflow-backed Fork Flow graphs.
- Added tests for message order, reader completion, reader failure conversion to `FlowError`, clean completion, and explicit fault behavior.
- Added `MqttConditionRouterComponent` to route `MqttEnvelope` values into true/false branches.
- Added tests for topic-prefix routing, predicate failure conversion to `FlowError`, pending-error completion, and explicit fault behavior.
- Added `MqttRecordingSinkComponent` in `FluxMq.Replay` so recording can remain a flow component without making `FluxMq.Pipeline` depend on storage.
- Added tests for recording order, repository failure conversion to `FlowError`, continued processing after failed writes, and explicit fault behavior.
- Added `MqttMetricsSinkComponent` and `MqttMetricsSnapshot` in `FluxMq.Pipeline` for observability projections.
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
  - `mqtt.metrics-sink`
- Added runtime tests proving registered components can be linked through flow definitions and that invalid component configuration becomes a structured build error.
- Reintroduced `FluxMq.App` as a class-library workflow application host boundary.
- Added `FlowApplicationConfigurationLoader` to load `FluxMq:FlowApplication` through .NET configuration.
- Added `FlowApplicationHost` with build, start, stop, state, and structured host build errors.
- Added `FluxMq.App.Tests` covering configuration loading, runtime build, stop completion, missing configuration, and invalid component configuration.
- Added `FluxMq.Cli` as the first thin command-line host over `FluxMq.App`.
- Added `validate --config <path>` to validate a flow application configuration through the application host.
- Added `--output json` for machine-readable validation results while keeping text output as the default.
- Added `samples/flow-applications/metrics-only.json` as the first alpha validation sample.

## Current Next Step

Continue the alpha path by adding either `FluxMq.Cli run` for the existing no-service flow subset or the next stable runtime registration boundary for service-backed resources.
