# FluxMQ Progress Log

Chronological progress record.

## 2026-06-13 - Metrics tab redesign slice

- Redesigned the app-level Metrics tab into a dense two-pane workspace:
  - command bar with count, search, type filter, reset, and `New metric`
  - compact metric resource list with type, parameter summary, reference count, and latest reading state
  - inspector-style draft editor with identity fields, type selector, descriptor-driven parameters, validation, references, and live preview
- Added a catalog-driven metric creation dialog that derives metric type options and default parameters from `IFluxMetricCatalog`.
- Added draft-based save/cancel behavior, dirty-state prompting on selection/create, metric type reset confirmation, duplicate/delete actions, and rename through the existing metric rename path so dashboard bindings are preserved.
- Added read-only metric reference summaries for dashboard/widget bindings.
- Added UI helper test coverage for row formatting, filtering, default parameters, draft dirty/validation/save normalization, rename binding updates, reference summaries, and duplicate resources.
- Verified:
  - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore -p:UseSharedCompilation=false` passed
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false` passed with 410 tests
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -m:1` passed with 754 tests
- Remaining check: manual desktop visual smoke for the redesigned Metrics tab in light/dark and narrow windows.
- Follow-up visual polish pass tightened the same Metrics tab without changing saved definitions:
  - metric rows now use type and latest-value pills for clearer scanning
  - secondary editor commands moved into a compact icon action strip with tooltips
  - metric type details now show a callout plus compact facts
  - live preview now renders as a status/value card, and references show widget type when available
  - verified again with UI build, UI tests, and full solution tests
- Second alignment pass replaced the Metrics toolbar's form-like search/select controls with flat app controls so search, type filter, reset, and create align on one baseline; editor MudBlazor inputs were also toned down with scoped flat styling to reduce visual noise.
- Because the desktop app was running and locking the default UI build output, verification used temporary artifact output folders:
  - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-verify\` passed
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-tests-verify\` passed with 410 tests
- Third compact editor pass reduced visual noise in the same Metrics tab:
  - collapsed separate Identity, Metric type, Parameters, Live preview, References, and Validation cards into one compact edit panel plus a narrow details rail
  - removed always-visible helper text and the success validation card so the editor shows labels, values, and real issues instead of persistent explanatory copy
  - changed validation to a slim alert strip plus an error-only side detail panel
  - tightened preview, type facts, references, gaps, borders, and responsive behavior for a flatter app-workspace feel
  - verified again with temp-output UI build and UI tests; `git diff --check` passed with only the existing line-ending normalization warning
- Fourth density pass focused on the Metrics list/workspace frame:
  - shortened the command bar controls and editor header
  - changed the desktop split to give the compact editor more useful width
  - reduced metric row height and grid column minimums to avoid horizontal pressure
  - softened type/latest/reference pills so the list reads more like a flat data grid
  - tightened editor panel gaps, side rail width, input font sizing, and stacked responsive rows
  - removed stale CSS from the superseded card layout
  - verified with temp-output UI build and focused UI tests passing with 410 tests
- Fifth polish pass redesigned the `New metric` dialog:
  - replaced form-like MudBlazor search/name fields with flat compact app controls
  - shortened metric type rows and moved descriptions to the selected details pane
  - added resource id preview and default-parameter preview without changing the created metric schema
  - rebuilt the dialog scoped CSS around the same flat Metrics workspace language
  - verified with temp-output UI build and focused UI tests passing with 410 tests
- Sixth safety polish pass replaced the Metrics delete message box with a compact confirmation dialog:
  - shows the metric id in a copy-friendly monospace row
  - shows dashboard/widget reference count and read-only reference rows before deletion
  - keeps the destructive action visually distinct with a flat warning surface and red primary action
  - verified with temp-output UI build and focused UI tests passing with 410 tests
- Seventh confirmation polish pass replaced the remaining Metrics message boxes:
  - dirty-selection/create/duplicate discard prompts now use a compact flat confirmation dialog
  - metric type changes now use the same dialog before resetting draft parameters to descriptor defaults
  - the helper is UI-only and leaves metric runtime/schema behavior unchanged
  - verified with temp-output UI build and focused UI tests passing with 410 tests
- Eighth rename polish pass made metric identity an explicit workflow:
  - the main editor now shows metric id as a read-only metadata row instead of a normal text field
  - added a compact `Rename metric` dialog with id validation and dashboard-binding update note
  - rename is available only when the draft is clean, so Save remains focused on metric content edits
  - existing project rename behavior is still used so dashboard metric bindings stay updated
  - verified with temp-output UI build and focused UI tests passing with 410 tests
- Ninth duplicate polish pass made metric copying explicit:
  - added a compact `Duplicate metric` dialog with source id, display-name input, generated resource-id preview, and copy-behavior note
  - the UI now creates the copy with the chosen id/display name while preserving type, parameters, labels, and export policy
  - the copied metric is selected immediately after creation
  - verified with temp-output UI build and focused UI tests passing with 410 tests

## 2026-06-13 - Candidate validation after FluxFlow package update

- Ran the next V1 candidate-hardening pass after the FluxFlow package update.
- Fixed the operations dashboard/test-studio sample so it validates with the current metric resource and dashboard widget model:
  - `messageVolume` now uses `message.count.windowed`
  - `messageRate` keeps `event.rate` but uses the metric-owned `topic` parameter
  - `topicVolume` now uses `topic.count.windowed`
  - composite `status.strip` and `qos.retain.breakdown` sample widgets were replaced with focused `status.value` and `qos.breakdown` widgets to avoid migration-generated invalid cells
  - docs-site sample copy was kept in sync
- Added App test coverage that loads and validates `samples/flow-applications/operations-dashboard-test-studio.json` through the configuration loader.
- Re-ran candidate gates:
  - `.\eng\verify-samples.ps1` passed
  - `dotnet run --project .\src\FluxMq.Cli\FluxMq.Cli.csproj -- validate --config .\samples\flow-applications\operations-dashboard-test-studio.json --output json` returned `isValid: true`
  - docs-site `npm run build` passed
  - `dotnet test .\FluxMq.sln --no-restore --nologo --verbosity minimal -p:UseSharedCompilation=false -p:UseAppHost=false -m:1` passed with 747 tests
  - release-shaped restore and Release/win-x64 solution tests passed with 747 tests
  - `.\eng\package-windows.ps1 -Configuration Release -Version 0.1.0` produced refreshed portable zip and MSI artifacts
- Local packaging note: the installed global WiX tool was v7 and required the OSMF EULA, so it was replaced with the documented WiX `6.0.2` tool before rerunning the package script.
- Remaining candidate gap: rerun focused manual packaged-desktop smoke against the refreshed package.

## 2026-06-13 - FluxFlow package update slice

- Updated FluxMQ package references to the newly published FluxFlow package versions:
  - `FluxFlow.Engine` `1.1.0`
  - `FluxFlow.Components.Designer` `1.0.1`
  - `FluxFlow.Components.Secrets`, `FluxFlow.Components.Storage`, and `FluxFlow.Components.Storage.FileSystem` `1.1.0`
  - remaining consumed `FluxFlow.Components.*` packages `1.2.0`
- Adapted MQTT publisher and trigger FluxMQ wrappers to the new package output contract:
  - package `Errors` outputs are now consumed by Dataflow links into local error sinks
  - removed casts to `IReceivableSourceBlock<FlowError>` because the new package fanout source is linkable but not receivable
- Verified:
  - `dotnet restore .\FluxMq.sln --nologo` passed
  - `dotnet build .\FluxMq.sln --no-restore --nologo --verbosity minimal -p:UseAppHost=false -m:1` passed with 0 warnings
  - `dotnet test .\FluxMq.sln --no-restore --nologo --verbosity minimal -p:UseSharedCompilation=false -p:UseAppHost=false -m:1` passed with 746 tests
  - FluxFlow package outdated scan returned no remaining FluxFlow updates
  - `git diff --check` passed with existing line-ending normalization warnings for edited files

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
- Dashboard inspector draft-loading cleanup:
  - Reduced `OnParametersSet` to lifecycle coordination: selected-cell draft load, empty-widget cleanup, and selected-widget draft load.
  - Split widget metric name resolution, metric draft setup, dashboard metric snapshot resolution, and legacy query-builder snapshot creation into focused local helpers.
  - Kept behavior unchanged and avoided new public abstractions in this slice.
  - Added source-boundary coverage for the draft-loading lifecycle shape.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal --filter "FullyQualifiedName~DashboardInspector"` passed with 16 tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 399 tests.
    - `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false` passed with 724 tests.
    - `git diff --check` passed.
- Next step: commit the draft-loading cleanup, then review remaining dashboard inspector command handlers to decide if this cleanup phase is complete.
- Dashboard inspector metric binding command cleanup:
  - Moved primary/add/remove/move metric binding rules into `DashboardInspectorMetricBindingState`.
  - Kept `DashboardInspector` responsible for invoking auto-apply and loading the selected metric draft.
  - Added coverage for binding list add/remove/move and primary insertion behavior.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal --filter "FullyQualifiedName~DashboardInspectorMetricBindingState|FullyQualifiedName~DashboardInspector"` passed with 18 tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 401 tests.
    - `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false` passed with 726 tests.
    - `git diff --check` passed.
- Next step: commit the binding-command cleanup, then stop this cleanup phase unless a review exposes a concrete remaining blocker.
- Dashboard metric cleanup PR:
  - Pushed `work/metric-dashboard-cleanup`.
  - Opened draft PR #169: `Clean up dashboard metric boundaries`.
  - GitHub validation:
    - `Validate Windows desktop app` passed.
    - `Package Windows desktop app` was skipped.
- Next step: keep PR #169 in draft for review, then mark ready or merge only after approval.
- Metric stream integration follow-up:
  - Started `work/metric-stream-integration` from clean `main` after PR #169 was merged and local `main` was synced.
  - Added focused pipeline acceptance coverage proving `metric.source` can route `FluxMetricReading<double>` through `flow.when`.
  - Added focused pipeline acceptance coverage proving `metric.source` can feed `flow.mapper` and map a metric reading into an MQTT publish request.
  - Added UI model coverage for `MetricSourceNodeModel` creation, configuration normalization, typed output, and parameter round-trip.
  - Polished the Metric Source node editor so metric parameters use allowed-value selects, boolean selects, and a readable app-metric sentence instead of treating every parameter as plain text.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore --filter "FullyQualifiedName~MetricSourceFactory|FullyQualifiedName~FluxMetricRuntimeHost|FullyQualifiedName~MetricSourceComponent" -p:UseAppHost=false --verbosity minimal` passed with 5 tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~SourceNodeModelTests" -p:UseAppHost=false --verbosity minimal` passed with 9 tests.
    - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 116 tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 404 tests.
    - `git diff --check` passed with line-ending normalization warnings for the edited files.
- Next step: run the broader UI/App test gate, then move `event.counter` from dashboard-local metric query editing to app-level metric selection if the Metric Source editor QA looks good.
- Event Counter app-metric consumption:
  - Moved `event.counter` inspector editing onto the same app-level metric selector path as KPI.
  - Kept legacy widget-side metric query fallback intact for old dashboard JSON.
  - Filtered the Event Counter metric selector to count-based app metrics so the widget remains focused on one responsibility.
  - Added behavior coverage proving Event Counter can resolve a root app metric artifact and calculate its value from runtime events through the shared dashboard metric bridge.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal --filter "FullyQualifiedName~DashboardEventFilterCatalogTests"` passed with 94 tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal --filter "FullyQualifiedName~DashboardEventFilterCatalogTests|FullyQualifiedName~GetDashboardMetricValue_UsesAppMetricArtifactForEventCounter"` passed with 95 tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 405 tests.
- Next step: commit the Event Counter app-metric slice, then continue one widget at a time with `event.rate` moving from dashboard-local query editing to app-level rate metric selection.
- Event Rate app-metric consumption:
  - Moved `event.rate` inspector editing onto the app-level metric selector path.
  - Filtered the Event Rate selector to rate-based app metrics so it remains a focused rate widget.
  - Kept legacy query fallback in place for existing dashboard-local rate query JSON.
  - Added behavior coverage proving Event Rate can resolve a root app metric artifact and calculate events-per-second from runtime events through the shared dashboard metric bridge.
  - Verification:
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal --filter "FullyQualifiedName~DashboardEventFilterCatalogTests|FullyQualifiedName~GetDashboardMetricValue_UsesAppMetricArtifactForEventRate"` passed with 95 tests.
    - A parallel UI build attempt hit a transient XAML compiler file lock; the serial rerun passed.
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings on serial rerun.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 406 tests.
- Next step: commit the Event Rate app-metric slice, then review remaining dashboard metric consumers and choose the next single widget instead of doing a broad migration.
- Rate Tile app-metric consumption:
  - Moved `rate.tile` onto the app-level metric selector path.
  - Removed `rate.tile` from the old visual-metric/default `primaryMetric` path so new rate tiles save as display config plus selected metric binding.
  - Filtered the Rate Tile selector to rate-based app metrics.
  - Kept the widget rendering as the existing focused value visual; no new visualization choices were added in this slice.
  - Added coverage for clean rate-tile widget config, composer defaults, inspector source ownership, and runtime value resolution from a root app metric artifact.
  - Verification:
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal --filter "FullyQualifiedName~DashboardWidgetSettingsDraft_WritesRateTileAsAppMetricConfiguration|FullyQualifiedName~DashboardInspector_UsesAppMetricsForKpiCounterAndRateWidgets|FullyQualifiedName~AddDashboardWidget_AddsRateTileDefaults|FullyQualifiedName~GetDashboardMetricValue_UsesAppMetricArtifactForRateTile"` passed with 4 tests.
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 409 tests.
- Next step: commit the Rate Tile app-metric slice, then choose between `status.value` and `event.gauge` as the next single metric consumer to clean up.
- Status Value app-metric consumption:
  - Moved `status.value` onto the app-level metric selector path as a count/status metric consumer.
  - Replaced the old metric-card renderer with the shared metric value visualization view and made the widget host let the visual own its header.
  - Removed `status.value` from the old visual-metric/default `primaryMetric` path so new status widgets save as display config plus selected metric binding.
  - Filtered the Status Value selector to count-based app metrics to keep it distinct from rate widgets.
  - Added coverage for clean status-value widget config, composer defaults, inspector source ownership, shared rendering, and runtime value resolution from a root app metric artifact.
  - Verification:
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~DashboardWidgetSettingsDraft_WritesStatusValueAsAppMetricConfiguration|FullyQualifiedName~DashboardInspector_UsesAppMetricsForFocusedMetricValueWidgets|FullyQualifiedName~DashboardWidgetModuleCatalog_ProvidesFocusedPropertyDefinitionsForAllPaletteWidgets|FullyQualifiedName~DashboardMetricValueWidgets_UseSharedVisualizationView|FullyQualifiedName~AddDashboardWidget_AddsStatusValueDefaults|FullyQualifiedName~GetDashboardMetricValue_UsesAppMetricArtifactForStatusValue" -p:UseAppHost=false --verbosity minimal` passed with 6 tests.
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 411 tests.
    - `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false` passed with 738 tests.
    - `git diff --check` passed with line-ending normalization warnings for the edited files.
- Next step: commit the Status Value app-metric slice, then continue one widget at a time with `event.gauge` unless a review finds a smaller dashboard metric blocker.
- Event Gauge app-metric consumption:
  - Moved `event.gauge` onto the app-level metric selector path while keeping the existing ring/meter gauge shape setting.
  - Replaced snapshot-derived primary metric cards in the gauge renderer with `DashboardMetricValue` from the shared dashboard metric bridge.
  - Removed `event.gauge` from the old visual-metric/default `primaryMetric` path so new gauges save as `title`, `metric`, and `gaugeStyle`.
  - Kept compatibility fallback through existing metric binding/migration, but did not add new gauge range or threshold UI in this slice.
  - Added coverage for clean gauge widget config, composer defaults, inspector source ownership, shared metric-value rendering, and runtime value resolution from a root app metric artifact.
  - Verification:
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~DashboardWidgetSettingsDraft_WritesEventGaugeAsAppMetricConfiguration|FullyQualifiedName~DashboardWidgetSettingsProfiles_ExposeDedicatedSettingsShape|FullyQualifiedName~DashboardMetricValueWidgets_UseSharedVisualizationView|FullyQualifiedName~DashboardInspector_UsesAppMetricsForFocusedMetricWidgets|FullyQualifiedName~DashboardWidgetModuleCatalog_ProvidesFocusedPropertyDefinitionsForAllPaletteWidgets|FullyQualifiedName~AddDashboardWidget_AddsEventGaugeDefaults|FullyQualifiedName~GetDashboardMetricValue_UsesAppMetricArtifactForEventGauge|FullyQualifiedName~UpdateDashboardMetric_WritesQueryShapeWithoutSchemaMigration" -p:UseAppHost=false --verbosity minimal` passed with 8 tests.
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 414 tests.
    - `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false` passed with 741 tests.
    - `git diff --check` passed with line-ending normalization warnings for the edited files.
- Next step: commit the Event Gauge app-metric slice, then continue one widget at a time with chart/payload metric consumers or pause for a dashboard-gauge visual range/settings review.
- Event Gauge range/settings polish:
  - Added gauge-owned range settings for `event.gauge`: min, max, target, warning threshold, critical threshold, and normal/warning/critical colors.
  - Kept these settings in widget presentation config, separate from app-level metric definitions and metric streams.
  - Updated the gauge renderer so ring and meter shapes map the latest `DashboardMetricValue` through the configured range instead of treating the raw metric value as a percent.
  - Removed the old raw gauge-percent helper so range-aware gauge state is the only remaining gauge calculation path.
  - Added a target marker and range/target labels to the rendered gauge state.
  - Exposed the new settings in the existing property grid with auto-apply, using numeric rows and compact color-picker rows. No popup editor or schema change was added.
  - Added coverage for gauge range math, module defaults, draft save output, inspector rows, and composer defaults.
  - Verification:
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~DashboardWidgetFormatting_MapsGaugeMetricValueThroughConfiguredRange|FullyQualifiedName~DashboardWidgetModuleCatalog_ProvidesFocusedPropertyDefinitionsForAllPaletteWidgets|FullyQualifiedName~DashboardWidgetSettingsDraft_WritesEventGaugeAsAppMetricConfiguration|FullyQualifiedName~DashboardInspector_UsesFocusedDisplayModeRowComponent|FullyQualifiedName~AddDashboardWidget_AddsEventGaugeDefaults" -p:UseAppHost=false --verbosity minimal` passed with 5 tests.
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 415 tests.
    - `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false` passed with 742 tests.
    - A parallel build/test rerun hit the known XAML compiler file-lock condition; the serial build and final solution rerun both passed.
- Next step: run `git diff --check`, commit the Event Gauge range/settings slice, then review the gauge UI in the running dashboard before choosing the next single widget consumer.
- DI-first runtime cleanup:
  - Replaced the metric runtime measure switch with keyed metric stream modules for count, rate, unique topics, payload bytes, average payload, and retained counts.
  - Kept `FluxMetricRuntimeHost` focused on lifecycle, stream reuse, latest-reading lookup, and app-runtime event attachment; metric classes still own calculation.
  - Added FluxMQ runtime node modules keyed by node type and changed the engine registry extension to project those modules into the existing FluxFlow `RuntimeNodeFactoryRegistry`.
  - Added `AddFluxMqMetricStreams`, `AddFluxMqRuntimeNodes`, and `AddFluxMqAppRuntime` registration extensions.
  - Wired default app hosts and the CLI validation/run path through the new app runtime composition while preserving existing `FlowApplicationHost.CreateDefault(...)` entrypoints.
  - Added synchronous disposal to `FluxMetricRuntimeHost` so DI containers can be disposed safely from both sync and async host paths.
  - Kept dashboard/widget/scenario metadata catalogs untouched in this slice.
  - Verification:
    - `dotnet build src\FluxMq.App\FluxMq.App.csproj --no-restore --verbosity minimal` passed with 0 warnings.
    - `dotnet build src\FluxMq.Cli\FluxMq.Cli.csproj --no-restore --verbosity minimal` passed with 0 warnings.
    - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore --filter "FullyQualifiedName~AddFluxMqMetricStreams_RegistersAllNumberMeasureModulesAsKeyedServices|FullyQualifiedName~AddFluxMqRuntimeNodes_RegistersFluxMqNodeModulesAsKeyedServices|FullyQualifiedName~RegisterPipelineComponentFactories_RegistersStableComponentTypes|FullyQualifiedName~FluxMetricRuntimeHost_StartsConfiguredMetricStreams|FullyQualifiedName~MetricSourceComponent_RelaysExistingMetricStream" -p:UseAppHost=false --verbosity minimal` passed with 5 tests.
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 118 tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 415 tests.
    - `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false` passed with 744 tests.
    - `git diff --check` passed with line-ending normalization warnings for edited files.
  - Next step: choose either dashboard catalog/module cleanup or unsupported gauge-shape cleanup as the next focused cleanup slice.
- Dashboard catalog/module cleanup:
  - Split `DashboardMetricVisualizationCatalog` into explicit visualization module providers for `metric.value` and `metric.digital`.
  - Added a provider-composition seam to `DashboardWidgetModuleCatalog` and grouped widgets by Metrics, Events, Charts, MQTT Ops, and Topics providers.
  - Moved Metrics provider-owned module construction and metric property groups out of `DashboardWidgetModuleCatalog` into `DashboardMetricWidgetModuleProvider`.
  - Removed obsolete KPI/metric module helper methods from the central catalog so it now acts more like a thin provider composer for metric widgets.
  - Moved Events provider-owned module construction, event filter groups, metric-query groups, latest-event field settings, event table settings, and event-gauge range groups into `DashboardEventWidgetModuleProvider`.
  - Removed the corresponding event-only helper methods from the central catalog; shared helpers remained only where Charts, MQTT Ops, or Topics still needed them.
  - Moved Charts provider-owned module construction and chart-specific property groups into `DashboardChartWidgetModuleProvider`.
  - Removed the central chart-only helper methods from `DashboardWidgetModuleCatalog`.
  - Kept the old chart compatibility alias on the line-chart module and kept donut chart sizing/defaults unchanged.
  - Moved MQTT Ops provider-owned module construction, payload bucket settings, and QoS/retain breakdown settings into `DashboardMqttOpsWidgetModuleProvider`.
  - Removed the central MQTT Ops-only bucket and breakdown helper methods from `DashboardWidgetModuleCatalog`.
  - Kept payload distribution, QoS breakdown, retain breakdown, data requirements, sizing, and the old QoS/retain compatibility alias unchanged.
  - Moved Topics provider-owned module construction, topic activity settings, and topic tree settings into `DashboardTopicWidgetModuleProvider`.
  - Deleted the remaining central event-style factory/helper block from `DashboardWidgetModuleCatalog`.
  - Left `DashboardWidgetModuleCatalog` as a thin provider composer and compatibility lookup surface only.
  - Kept all dashboard widget ids, visualization ids, property definitions, defaults, schema, and UI behavior unchanged.
  - Used explicit provider lists rather than reflection scanning or DI for static/listable dashboard metadata.
  - Added catalog coverage proving provider ids are unique and provider-built modules match the catalog module order.
  - Added coverage proving the Metrics provider owns the KPI, Status Value, and Rate Tile definitions and keeps their clean metric defaults.
  - Added coverage proving the Events provider owns Event Counter, Latest Event, Event Rate, Event Gauge, and Event Table definitions and keeps their clean event defaults.
  - Added coverage proving the Charts provider owns Line Chart, Area Chart, Bar Chart, and Donut Chart definitions and keeps their chart defaults/alias behavior.
  - Added coverage proving the MQTT Ops provider owns Payload Size Distribution, QoS Breakdown, and Retain Breakdown definitions and keeps their groups/alias behavior.
  - Added coverage proving the Topics provider owns Topic Activity and Topic Tree definitions and keeps their groups/defaults/layout behavior.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~DashboardWidgetModuleCatalog|FullyQualifiedName~DashboardMetricVisualizationCatalog" -p:UseAppHost=false --verbosity minimal` passed with 4 tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~DashboardWidgetModuleCatalog|FullyQualifiedName~DashboardMetricWidgetModuleProvider|FullyQualifiedName~DashboardMetricVisualizationCatalog" -p:UseAppHost=false --verbosity minimal` passed with 5 tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~DashboardWidgetModuleCatalog|FullyQualifiedName~DashboardMetricWidgetModuleProvider|FullyQualifiedName~DashboardEventWidgetModuleProvider|FullyQualifiedName~DashboardMetricVisualizationCatalog" -p:UseAppHost=false --verbosity minimal` passed with 6 tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~DashboardWidgetModuleCatalog_ComposesCategoryProviderModules|FullyQualifiedName~DashboardMetricWidgetModuleProvider_OwnsMetricWidgetDefinitions|FullyQualifiedName~DashboardEventWidgetModuleProvider_OwnsEventWidgetDefinitions|FullyQualifiedName~DashboardChartWidgetModuleProvider_OwnsChartWidgetDefinitions" -p:UseAppHost=false --verbosity minimal` passed with 4 tests.
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings after the MQTT Ops move.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~DashboardWidgetModuleCatalog_ComposesCategoryProviderModules|FullyQualifiedName~DashboardChartWidgetModuleProvider_OwnsChartWidgetDefinitions|FullyQualifiedName~DashboardMqttOpsWidgetModuleProvider_OwnsMqttOpsWidgetDefinitions" -p:UseAppHost=false --verbosity minimal` passed with 3 tests.
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings after the Topics move.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~DashboardWidgetModuleCatalog_ComposesCategoryProviderModules|FullyQualifiedName~DashboardMetricWidgetModuleProvider_OwnsMetricWidgetDefinitions|FullyQualifiedName~DashboardEventWidgetModuleProvider_OwnsEventWidgetDefinitions|FullyQualifiedName~DashboardChartWidgetModuleProvider_OwnsChartWidgetDefinitions|FullyQualifiedName~DashboardMqttOpsWidgetModuleProvider_OwnsMqttOpsWidgetDefinitions|FullyQualifiedName~DashboardTopicWidgetModuleProvider_OwnsTopicWidgetDefinitions" -p:UseAppHost=false --verbosity minimal` passed with 6 tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 422 tests.
    - `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false` passed with 751 tests.
    - `git diff --check` passed with line-ending normalization warnings for edited files.
  - Next step: split the DI runtime cleanup and dashboard catalog cleanup into focused commits before starting another cleanup target.
- Event Gauge shape cleanup:
  - Merged PR #170 (`Modernize runtime composition and dashboard catalogs`) into `main`; post-merge Windows validation passed, with the package job skipped as optional.
  - Started `work/dashboard-gauge-cleanup` from clean `main`.
  - Removed the unsupported `tiles` gauge style from dashboard inspector controls and the widget editor dialog.
  - Deleted the unused `GaugeStyleTiles` constant and made old/unknown `tiles` values normalize to the implemented `ring` style.
  - Added coverage proving only implemented gauge shapes are exposed and stale `tiles` values fall back to `ring`.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~GaugeStyleOptions_ExposeOnlyImplementedShapes|FullyQualifiedName~DashboardWidgetSettingsDraft_WritesEventGaugeAsAppMetricConfiguration|FullyQualifiedName~DashboardWidgetFormatting_MapsGaugeMetricValueThroughConfiguredRange" -p:UseAppHost=false --verbosity minimal` passed with 3 tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 423 tests.
    - `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false` passed with 752 tests.
    - `git diff --check` passed with line-ending normalization warnings for edited files.
  - Next step: commit and PR the focused gauge cleanup, then continue with the next cleanup target after it is merged.
- Runtime package option cleanup:
  - Merged PR #171 (`Remove unsupported gauge shape option`) into `main`; post-merge Windows validation passed, with the package job skipped as optional.
  - Started `work/runtime-node-helper-cleanup` from clean `main`.
  - Extracted package component option wiring for timers, mapping, state, storage, and routing from `RuntimeNodeFactoryRegistryExtensions` into `FluxMqRuntimePackageComponentOptions`.
  - Kept FluxMQ runtime node module adaptation and node-specific creation helpers in the registry extension.
  - Moved the routing context factory beside the routing package option registration; message-filter topic context remains node-specific.
  - Verification:
    - `dotnet build src\FluxMq.App\FluxMq.App.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore --filter "FullyQualifiedName~AddFluxMqRuntimeNodes_RegistersFluxMqNodeModulesAsKeyedServices|FullyQualifiedName~RegisterPipelineComponentFactories_RegistersStableComponentTypes|FullyQualifiedName~MetricSourceComponent_RelaysExistingMetricStream" -p:UseAppHost=false --verbosity minimal` passed with 3 tests.
    - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 118 tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 423 tests.
    - `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false` passed with 752 tests.
    - `git diff --check` passed with line-ending normalization warnings for edited files.
  - Next step: commit and PR the focused runtime helper cleanup.
- Runtime node config reader cleanup:
  - Merged PR #172 (`Extract runtime package option wiring`) into `main`; post-merge Windows validation passed, with the package job skipped as optional.
  - Started `work/runtime-node-config-reader-cleanup` from clean `main`.
  - Extracted generic runtime node configuration readers from `RuntimeNodeFactoryRegistryExtensions` into `FluxMqRuntimeNodeConfigurationReader`.
  - Kept node-specific MQTT subscription, generated message, connection-profile, and topic-filter behavior in the runtime registry extension.
  - Left runtime node ids, metric source behavior, dashboard/test schemas, and FluxFlow unchanged.
  - Verification:
    - `dotnet build src\FluxMq.App\FluxMq.App.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore --filter "FullyQualifiedName~AddFluxMqRuntimeNodes_RegistersFluxMqNodeModulesAsKeyedServices|FullyQualifiedName~RegisterPipelineComponentFactories_RegistersStableComponentTypes|FullyQualifiedName~MetricSourceComponent_RelaysExistingMetricStream" -p:UseAppHost=false --verbosity minimal` passed with 3 tests.
    - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 118 tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 423 tests.
    - First parallel `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false` attempt hit a Windows App SDK `input.json` file lock while the UI test project was running in parallel.
    - Serial rerun of `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false` passed with 752 tests.
  - Next step: commit/PR/merge the focused config-reader cleanup, then continue the cleanup phase with the next runtime module split.
- Runtime source node module cleanup:
  - Merged PR #173 (`Extract runtime node configuration readers`) into `main`; post-merge Windows validation passed, with the package job skipped as optional.
  - Started `work/runtime-source-node-modules-cleanup` from clean `main`.
  - Moved stored session source, replay source, generated MQTT source, and metric source runtime module implementations into `FluxMqSourceRuntimeNodeModules`.
  - Removed the corresponding source creation bodies from `RuntimeNodeFactoryRegistryExtensions` and the thin adapter classes from `FluxMqRuntimeNodeModules`.
  - Kept source node ids, port names, DI registration keys, metric source behavior, dashboard/test schemas, and FluxFlow unchanged.
  - Kept MQTT trigger subscription parsing local to the runtime registry, while generated-source message parsing now lives with the generated source module.
  - Current line movement before staging:
    - `RuntimeNodeFactoryRegistryExtensions.cs`: 181 lines removed.
    - `FluxMqRuntimeNodeModules.cs`: 32 lines removed.
    - `FluxMqSourceRuntimeNodeModules.cs`: 272-line source module file added.
  - Verification:
    - `dotnet build src\FluxMq.App\FluxMq.App.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore --filter "FullyQualifiedName~AddFluxMqRuntimeNodes_RegistersFluxMqNodeModulesAsKeyedServices|FullyQualifiedName~RegisterPipelineComponentFactories_RegistersStableComponentTypes|FullyQualifiedName~MetricSourceComponent_RelaysExistingMetricStream" -p:UseAppHost=false --verbosity minimal` passed with 3 tests.
    - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 118 tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 423 tests.
    - `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false` passed with 752 tests.
    - PR #174 (`Extract runtime source node modules`) merged into `main`; post-merge Windows validation passed, with the package job skipped as optional.
  - Next step: continue with the next runtime node family split.
- Runtime sink node module cleanup:
  - Started `work/runtime-sink-node-modules-cleanup` from clean `main`.
  - Moved MQTT publisher, MQTT recorder, and file writer runtime module implementations into `FluxMqSinkRuntimeNodeModules`.
  - Removed the corresponding sink creation bodies from `RuntimeNodeFactoryRegistryExtensions` and the thin adapter classes from `FluxMqRuntimeNodeModules`.
  - Kept sink node ids, port names, DI registration keys, repository requirements, dashboard/test schemas, and FluxFlow unchanged.
  - Current line movement before staging:
    - `RuntimeNodeFactoryRegistryExtensions.cs`: 68 lines removed.
    - `FluxMqRuntimeNodeModules.cs`: 24 lines removed.
    - `FluxMqSinkRuntimeNodeModules.cs`: 115-line sink module file added.
  - Verification:
    - `dotnet build src\FluxMq.App\FluxMq.App.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings after adding the missing `FlowError` namespace.
    - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore --filter "FullyQualifiedName~AddFluxMqRuntimeNodes_RegistersFluxMqNodeModulesAsKeyedServices|FullyQualifiedName~RegisterPipelineComponentFactories_RegistersStableComponentTypes|FullyQualifiedName~MetricSourceComponent_RelaysExistingMetricStream" -p:UseAppHost=false --verbosity minimal` passed with 3 tests.
    - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 118 tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 423 tests.
    - `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false` passed with 752 tests.
    - `git diff --check` passed with line-ending normalization warnings for edited files.
  - Next step: commit/PR/merge this focused sink-node cleanup, then continue with the next runtime node family split.
- MQTT metrics rate-refresh blocker fix:
  - Merged PR #175 (`Extract runtime sink node modules`) into `main`; PR validation passed, then post-merge Windows validation exposed a release-only timing failure in `MqttMetricsComponentTests.Snapshots_EmitsRateDecayWhenTrafficStops`.
  - Root cause: the rate refresh timer could fire after input started the timer but before the inner metrics aggregate emitted its first package snapshot; the old refresh path treated a missing package snapshot like an empty stream and disposed the timer permanently.
  - Started `fix/mqtt-metrics-rate-refresh-race` from clean `main`.
  - Updated `MqttMetricsComponent.RefreshRateSnapshot` so a missing first aggregate snapshot keeps the refresh timer alive, while a real zero-sample package snapshot still stops the timer.
  - Verification so far:
    - Focused release test `MqttMetricsComponentTests.Snapshots_EmitsRateDecayWhenTrafficStops` passed.
    - First full release Components test run hit a separate transient LiteDB enumeration failure in `SessionRepositoryTests.GetAll_ReturnsMostRecentFirst`; immediate rerun passed.
    - Full release solution test with `win-x64`, serial execution, and shared compilation disabled passed.
  - Next step: run final diff hygiene, commit/PR/merge the blocker fix, then continue the runtime cleanup phase after main is green.
- Runtime control node module cleanup:
  - Merged PR #176 (`Fix MQTT metrics rate refresh race`) into `main`; post-merge Windows validation passed, with the package job skipped as optional.
  - Started `work/runtime-control-node-modules-cleanup` from clean `main`.
  - Moved message filter, condition router, and flow assertion runtime module implementations into `FluxMqControlRuntimeNodeModules`.
  - Moved control expression option building, assertion option building, topic-filter control context, and expression-engine resolution beside the control modules.
  - Kept control node ids, port names, supported assertion input types, expression engine behavior, dashboard/test schemas, and FluxFlow unchanged.
  - Current line movement before staging:
    - `RuntimeNodeFactoryRegistryExtensions.cs`: 340 lines removed, 1 composition call updated.
    - `FluxMqRuntimeNodeModules.cs`: 24 lines removed.
    - `FluxMqControlRuntimeNodeModules.cs`: 367-line control module file added.
  - Verification so far:
    - `dotnet build src\FluxMq.App\FluxMq.App.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore --filter "FullyQualifiedName~AddFluxMqRuntimeNodes_RegistersFluxMqNodeModulesAsKeyedServices|FullyQualifiedName~RegisterPipelineComponentFactories_RegistersStableComponentTypes|FullyQualifiedName~MetricSourceComponent_RelaysExistingMetricStream" -p:UseAppHost=false --verbosity minimal` passed with 3 tests.
    - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 118 tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 423 tests.
    - `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false -m:1` passed with 752 tests.
  - Next step: run final diff hygiene, then commit/PR/merge this focused control-node cleanup.
- Runtime MQTT node module cleanup:
  - Merged PR #177 (`Extract runtime control node modules`) into `main`; post-merge Windows validation passed, with the package job skipped as optional.
  - Started `work/runtime-mqtt-node-modules-cleanup` from clean `main`.
  - Moved MQTT connection, MQTT trigger, and MQTT connection-state trigger runtime module implementations into `FluxMqMqttRuntimeNodeModules`.
  - Moved MQTT connection profile parsing, subscription parsing, QoS parsing, and topic-filter validation beside the MQTT runtime modules.
  - Kept MQTT node ids, port names, subscription semantics, connection resource lookup, dashboard/test schemas, and FluxFlow unchanged.
  - Current line movement before staging:
    - `RuntimeNodeFactoryRegistryExtensions.cs`: 206 lines removed.
    - `FluxMqRuntimeNodeModules.cs`: 24 lines removed.
    - `FluxMqMqttRuntimeNodeModules.cs`: 222-line MQTT module file added.
  - Verification so far:
    - `dotnet build src\FluxMq.App\FluxMq.App.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore --filter "FullyQualifiedName~AddFluxMqRuntimeNodes_RegistersFluxMqNodeModulesAsKeyedServices|FullyQualifiedName~RegisterPipelineComponentFactories_RegistersStableComponentTypes|FullyQualifiedName~MetricSourceComponent_RelaysExistingMetricStream" -p:UseAppHost=false --verbosity minimal` passed with 3 tests.
    - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 118 tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 423 tests.
    - `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false -m:1` passed with 752 tests.
  - Next step: run final diff hygiene, then commit/PR/merge this focused MQTT-node cleanup.
- Runtime inspection node module cleanup:
  - Merged PR #178 (`Extract runtime MQTT node modules`) into `main`; post-merge Windows validation passed, with the package job skipped as optional.
  - Started `work/runtime-inspection-node-modules-cleanup` from clean `main`.
  - Moved payload inspector, MQTT metrics, flow logger, and JSON schema validator runtime module implementations into `FluxMqInspectionRuntimeNodeModules`.
  - Moved payload inspection output wiring, metrics snapshot output wiring, flow logger input selection, and JSON schema definition parsing beside those modules.
  - Kept inspection node ids, port names, schema validation behavior, logging behavior, metrics behavior, dashboard/test schemas, and FluxFlow unchanged.
  - Current line movement before staging:
    - `RuntimeNodeFactoryRegistryExtensions.cs`: 158 lines removed.
    - `FluxMqRuntimeNodeModules.cs`: 33 lines removed.
    - `FluxMqInspectionRuntimeNodeModules.cs`: 192-line inspection module file added.
  - Verification so far:
    - `dotnet build src\FluxMq.App\FluxMq.App.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore --filter "FullyQualifiedName~AddFluxMqRuntimeNodes_RegistersFluxMqNodeModulesAsKeyedServices|FullyQualifiedName~RegisterPipelineComponentFactories_RegistersStableComponentTypes|FullyQualifiedName~MetricSourceComponent_RelaysExistingMetricStream" -p:UseAppHost=false --verbosity minimal` passed with 3 tests.
    - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 118 tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 423 tests.
    - First `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false -m:1` run hit the known transient LiteDB enumeration failure in `SessionRepositoryTests.GetAll_ReturnsMostRecentFirst`; the focused failed test passed on immediate rerun.
    - Second `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false -m:1` run passed with 752 tests.
    - `git diff --check` passed with line-ending normalization warnings for edited files.
  - Next step: commit/PR/merge this focused inspection-node cleanup, then audit whether the remaining runtime composition list should stay in the registry extension or move to a small module catalog file.
- Runtime module catalog cleanup:
  - Merged PR #179 (`Extract runtime inspection node modules`) into `main`; post-merge Windows validation passed, with the package job skipped as optional.
  - Started `work/runtime-module-catalog-cleanup` from clean `main`.
  - Moved fallback runtime module resolution and no-DI default module instance creation into `FluxMqRuntimeNodeModuleCatalog`.
  - Kept `RuntimeNodeFactoryRegistryExtensions` focused on package component registration plus FluxMQ runtime adapter registration.
  - Kept runtime node ids, keyed-DI registration order, fallback no-DI behavior, dashboard/test schemas, and FluxFlow unchanged.
  - Current line movement before staging:
    - `RuntimeNodeFactoryRegistryExtensions.cs`: 35 lines removed and 1 composition call updated.
    - `FluxMqRuntimeNodeModuleCatalog.cs`: 39-line runtime module catalog file added.
    - `FluxMqRuntimeNodeModules.cs`: 1 spacing line added between service collection extension classes.
  - Verification so far:
    - `dotnet build src\FluxMq.App\FluxMq.App.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore --filter "FullyQualifiedName~AddFluxMqRuntimeNodes_RegistersFluxMqNodeModulesAsKeyedServices|FullyQualifiedName~RegisterPipelineComponentFactories_RegistersStableComponentTypes|FullyQualifiedName~MetricSourceComponent_RelaysExistingMetricStream" -p:UseAppHost=false --verbosity minimal` passed with 3 tests.
    - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 118 tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 423 tests.
    - `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false -m:1` passed with 752 tests.
    - `git diff --check` passed with line-ending normalization warnings for edited files.
  - Next step: commit/PR/merge this focused runtime module catalog cleanup, then move to dashboard catalog/module cleanup as the next non-runtime phase.
- Dashboard metric provider file cleanup:
  - Merged PR #180 (`Extract runtime module catalog`) into `main`; post-merge Windows validation passed, with the package job skipped as optional.
  - Started `work/dashboard-metric-provider-file-cleanup` from clean `main`.
  - Moved `DashboardMetricWidgetModuleProvider` and its metric-specific helper methods out of the shared dashboard provider bundle into `DashboardMetricWidgetModuleProvider.cs`.
  - Kept `DashboardWidgetModuleCatalog` provider composition, Metrics provider id, widget ids, defaults, property groups, visualization defaults, dashboard schema, and UI behavior unchanged.
  - Left Events, Charts, MQTT Ops, and Topics providers in `DashboardWidgetModuleProviders.cs` for later focused splits.
  - Current line movement before staging:
    - `DashboardWidgetModuleProviders.cs`: 160 lines removed.
    - `DashboardMetricWidgetModuleProvider.cs`: 165-line Metrics provider file added.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~DashboardWidgetModuleCatalog_ComposesCategoryProviderModules|FullyQualifiedName~DashboardMetricWidgetModuleProvider_OwnsMetricWidgetDefinitions" -p:UseAppHost=false --verbosity minimal` passed with 2 tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 423 tests.
    - `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false -m:1` passed with 752 tests.
    - `git diff --check` passed with line-ending normalization warnings for edited files.
  - Next step: commit/PR/merge this focused Metrics provider file cleanup, then split the Events provider into its own file as the next small dashboard cleanup slice.
- Dashboard events provider file cleanup:
  - Merged PR #181 (`Extract dashboard metric provider`) into `main`; post-merge Windows validation passed, with the package job skipped as optional.
  - Started `work/dashboard-events-provider-file-cleanup` from clean `main`.
  - Moved `DashboardEventWidgetModuleProvider` and its event-specific helper methods out of the shared dashboard provider bundle into `DashboardEventWidgetModuleProvider.cs`.
  - Kept `DashboardWidgetModuleCatalog` provider composition, Events provider id, event widget ids, renderer kinds, defaults, property groups, layout spans, dashboard schema, and UI behavior unchanged.
  - Left Charts, MQTT Ops, and Topics providers in `DashboardWidgetModuleProviders.cs` for later focused splits.
  - Current line movement before staging:
    - `DashboardWidgetModuleProviders.cs`: 206 lines removed.
    - `DashboardEventWidgetModuleProvider.cs`: 211-line Events provider file added.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~DashboardWidgetModuleCatalog_ComposesCategoryProviderModules|FullyQualifiedName~DashboardEventWidgetModuleProvider_OwnsEventWidgetDefinitions" -p:UseAppHost=false --verbosity minimal` passed with 2 tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 423 tests.
    - First `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false -m:1` run hit the known transient LiteDB enumeration failure in `SessionRepositoryTests.GetAll_ReturnsMostRecentFirst`; the focused failed test passed on immediate rerun.
    - Second `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false -m:1` run passed with 752 tests.
    - `git diff --check` passed with line-ending normalization warnings for edited files.
  - Next step: commit/PR/merge this focused Events provider file cleanup, then split the Charts provider into its own file as the next small dashboard cleanup slice.
- Dashboard charts provider file cleanup:
  - Merged PR #182 (`Extract dashboard events provider`) into `main`; post-merge Windows validation passed, with the package job skipped as optional.
  - Started `work/dashboard-charts-provider-file-cleanup` from clean `main`.
  - Moved `DashboardChartWidgetModuleProvider` and its chart-specific helper methods out of the shared dashboard provider bundle into `DashboardChartWidgetModuleProvider.cs`.
  - Kept `DashboardWidgetModuleCatalog` provider composition, Charts provider id, chart widget ids, renderer kinds, defaults, property groups, layout spans, compatibility aliases, dashboard schema, and UI behavior unchanged.
  - Left MQTT Ops and Topics providers in `DashboardWidgetModuleProviders.cs` for later focused splits.
  - Current line movement before staging:
    - `DashboardWidgetModuleProviders.cs`: 199 lines removed.
    - `DashboardChartWidgetModuleProvider.cs`: 198-line Charts provider file added.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - First focused chart provider test command collided with the parallel UI build over the intermediate XAML assembly; immediate serial rerun passed with 2 tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 423 tests.
    - `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false -m:1` passed with 752 tests.
    - `git diff --check` passed with line-ending normalization warnings for edited files.
  - Next step: commit/PR/merge this focused Charts provider file cleanup, then split the MQTT Ops provider into its own file as the next small dashboard cleanup slice.
- Dashboard MQTT Ops provider file cleanup:
  - Merged PR #183 (`Extract dashboard charts provider`) into `main`; post-merge Windows validation passed, with the package job skipped as optional.
  - Started `work/dashboard-mqtt-ops-provider-file-cleanup` from clean `main`.
  - Moved `DashboardMqttOpsWidgetModuleProvider` and its MQTT Ops-specific helper methods out of the shared dashboard provider bundle into `DashboardMqttOpsWidgetModuleProvider.cs`.
  - Kept `DashboardWidgetModuleCatalog` provider composition, MQTT Ops provider id, widget ids, renderer kinds, defaults, property groups, layout spans, compatibility aliases, dashboard schema, and UI behavior unchanged.
  - Left the Topics provider in `DashboardWidgetModuleProviders.cs` for the final focused split.
  - Current line movement before staging:
    - `DashboardWidgetModuleProviders.cs`: 126 lines removed.
    - `DashboardMqttOpsWidgetModuleProvider.cs`: 131-line MQTT Ops provider file added.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~DashboardWidgetModuleCatalog_ComposesCategoryProviderModules|FullyQualifiedName~DashboardMqttOpsWidgetModuleProvider_OwnsMqttOpsWidgetDefinitions" -p:UseAppHost=false --verbosity minimal` passed with 2 tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 423 tests.
    - `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false -m:1` passed with 752 tests.
    - `git diff --check` passed with line-ending normalization warnings for edited files.
  - Next step: commit/PR/merge this focused MQTT Ops provider file cleanup, then split the Topics provider into its own file as the final small dashboard provider cleanup slice.
- Dashboard topics provider file cleanup:
  - Merged PR #184 (`Extract dashboard MQTT ops provider`) into `main`; post-merge Windows validation passed, with the package job skipped as optional.
  - Started `work/dashboard-topics-provider-file-cleanup` from clean `main`.
  - Moved `DashboardTopicWidgetModuleProvider` and its topic-specific helper methods out of the remaining dashboard provider bundle into `DashboardTopicWidgetModuleProvider.cs`.
  - Deleted the now-obsolete `DashboardWidgetModuleProviders.cs` bundle file.
  - Kept `DashboardWidgetModuleCatalog` provider composition, Topics provider id, widget ids, renderer kinds, defaults, property groups, layout spans, dashboard schema, and UI behavior unchanged.
  - Current line movement before staging:
    - `DashboardWidgetModuleProviders.cs`: 115-line bundle file deleted.
    - `DashboardTopicWidgetModuleProvider.cs`: 115-line Topics provider file added.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~DashboardWidgetModuleCatalog_ComposesCategoryProviderModules|FullyQualifiedName~DashboardTopicWidgetModuleProvider_OwnsTopicWidgetDefinitions" -p:UseAppHost=false --verbosity minimal` passed with 2 tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 423 tests.
    - `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false -m:1` passed with 752 tests.
    - `git diff --check` passed with a line-ending normalization warning for the edited progress log.
  - Next step: commit/PR/merge this final provider-file cleanup, then choose the next cleanup slice now that dashboard widget provider files are fully split.
- Dashboard catalog cache cleanup:
  - Merged PR #185 (`Extract dashboard topics provider`) into `main`; post-merge Windows validation passed, with the package job skipped as optional.
  - Started `work/dashboard-catalog-cache-cleanup` from clean `main`.
  - Cached dashboard widget provider composition and module composition in `DashboardWidgetModuleCatalog` so repeated lookup paths reuse the same explicit metadata set.
  - Cached metric visualization provider composition and module composition in `DashboardMetricVisualizationCatalog` for the same reason.
  - Kept provider ids, provider order, module ids, widget ids, visualization ids, defaults, schema, UI text, and rendering behavior unchanged.
  - Added focused assertions that repeated catalog reads return the cached provider/module lists while preserving provider/module order.
  - Verification:
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~DashboardWidgetModuleCatalog_ComposesCategoryProviderModules|FullyQualifiedName~DashboardMetricVisualizationCatalog_ComposesExplicitProviderModules" -p:UseAppHost=false --verbosity minimal` passed with 2 tests.
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 423 tests.
    - `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false -m:1` passed with 752 tests.
    - `git diff --check` passed with line-ending normalization warnings for edited files.
  - Next step: commit/PR/merge this catalog-cache cleanup, then inspect obsolete dashboard fallback/default configuration paths as the next small cleanup candidate.
- Dashboard default fallback cleanup:
  - Merged PR #186 (`Cache dashboard catalog composition`) into `main`; post-merge Windows validation passed, with the package job skipped as optional.
  - Started `work/dashboard-default-fallback-cleanup` from clean `main`.
  - Removed the obsolete default-configuration switch from `FlowDashboardDefinitionFactory.CreateWidgetConfiguration`; known widget defaults now come only from `DashboardWidgetModuleCatalog`.
  - Kept unknown/custom widget types as empty configuration.
  - Removed stale dashboard widget category predicate helpers from `DashboardWidgetCatalog`; the remaining topic subtitle check now uses the focused widget id directly.
  - Added a regression test proving every current module type and legacy focused alias gets defaults from the module catalog.
  - Current line movement before staging:
    - 133 lines removed.
    - 46 lines added.
  - Verification:
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~FlowDashboardDefinitionFactory_CreatesWidgetDefaultsFromModuleCatalog|FullyQualifiedName~AddDashboardWidget_AddsVisualDashboardWidgetDefaults|FullyQualifiedName~AddDashboardWidget_AddsFocusedGaugeDefaults" -p:UseAppHost=false --verbosity minimal` passed with 2 tests.
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 424 tests.
    - `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false -m:1` passed with 753 tests.
    - `git diff --check` passed with line-ending normalization warnings for edited files.
  - Next step: commit/PR/merge this default fallback cleanup, then inspect the remaining legacy dashboard descriptor/migration surface before deciding the next small cleanup.
- Dashboard legacy surface cleanup:
  - Merged PR #187 (`Remove dashboard default fallback`) into `main`; post-merge Windows validation passed, with the package job skipped as optional.
  - Started `work/dashboard-legacy-surface-cleanup` from clean `main`.
  - Moved dashboard widget instance-name prefixes into `DashboardWidgetModule` so module metadata owns add-time names together with defaults, compatibility ids, property groups, and layout contracts.
  - Replaced the old `FlowDashboardDefinitionFactory.WidgetNamePrefix` switch with `DashboardWidgetModuleCatalog.InstanceNamePrefixFor`.
  - Kept legacy add aliases normalized through focused widget types, so `status.strip`, `event.chart`, and `qos.retain.breakdown` still create `statusValue`, `barChart`, and `qosBreakdown` instances.
  - Added regression coverage for module-owned prefixes and unknown widget fallback naming.
  - Current line movement before staging:
    - 85 lines added.
    - 35 lines removed.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~DashboardWidgetModuleCatalog_OwnsInstanceNamePrefixes|FullyQualifiedName~AddDashboardWidget_AddsWidgetAndAssignsSelectedSlot|FullyQualifiedName~AddDashboardWidget_AddsVisualDashboardWidgetDefaults|FullyQualifiedName~AddDashboardWidget_PlacesDuplicateOrdinalAfterMetricSuffix" -p:UseAppHost=false --verbosity minimal` passed with 4 tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 425 tests.
    - First full solution run hit the known transient LiteDB session repository collection-modified failure; immediate focused rerun of the failed test passed with 1 test.
    - Second `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false -m:1` passed with 754 tests.
    - `git diff --check` passed with line-ending normalization warnings for edited files.
  - Next step: commit/PR/merge this legacy surface cleanup, then inspect the remaining legacy dashboard descriptor list and decide whether compatibility descriptors can become module metadata or should stay as explicit migration-only entries.
- Dashboard compatibility descriptor cleanup:
  - Merged PR #188 (`Move dashboard widget names into modules`) into `main`; post-merge Windows validation passed, with the package job skipped as optional.
  - Started `work/dashboard-compat-descriptor-cleanup` from clean `main`.
  - Removed the duplicated focused and legacy dashboard widget descriptor lists from `DashboardWidgetCatalog`; widget descriptors now come from `DashboardWidgetModuleCatalog` through `DashboardWidgetRegistry`.
  - Kept `DashboardWidgetCatalog` as the constants/normalization home, including legacy add-time alias normalization for `status.strip`, `event.chart`, and `qos.retain.breakdown`.
  - Routed dashboard designer widget labels through `DashboardWidgetRegistry`, so load-time compatibility ids still resolve through module-owned compatibility metadata without exposing legacy compatibility descriptors as visible catalog items.
  - Added regression coverage proving the registry exposes only focused descriptors while old ids still resolve to their focused modules.
  - Current line movement before staging:
    - 221 lines removed.
    - 37 lines added.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - Initial parallel focused test command collided with the simultaneous UI build on the generated XAML intermediate output.
    - Serial rerun of `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~DashboardWidgetRegistry_ExposesFocusedDescriptorsAndKeepsCompatibilityLookup|FullyQualifiedName~DashboardWidgetModuleCatalog_ComposesCategoryProviderModules|FullyQualifiedName~DashboardWidgetModuleCatalog_OwnsInstanceNamePrefixes|FullyQualifiedName~DashboardWidgetModuleCatalog_ProvidesFocusedPropertyDefinitionsForAllPaletteWidgets" -p:UseAppHost=false --verbosity minimal` passed with 4 tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 426 tests.
    - `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false -m:1` passed with 755 tests.
    - `git diff --check` passed with line-ending normalization warnings for edited files.
  - Next step: commit/PR/merge this focused descriptor cleanup, then inspect whether `DashboardWidgetCatalog` constants can be split into focused constant groups or should stay as the temporary dashboard id/key compatibility home.
- Dashboard widget catalog static cleanup:
  - Merged PR #189 (`Remove dashboard compatibility descriptors`) into `main`; post-merge Windows validation passed, with the package job skipped as optional.
  - Started `work/dashboard-widget-catalog-static-cleanup` from clean `main`.
  - Converted `DashboardWidgetCatalog` to a static constants/normalizer class now that descriptor ownership moved to widget modules and no instance consumers remain.
  - Removed the stale `DashboardWidgetCatalog` service registration from `MauiProgram`.
  - Added source guard coverage proving the catalog stays static and is not reintroduced as a workspace component injection or DI service.
  - Current line movement before staging:
    - 2 lines removed.
    - 33 lines added.
  - Verification:
    - Initial parallel build/test found a missing `FluxMq.UI.Models` using after the static-class edit; adding the using back fixed it.
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~DashboardWidgetCatalog_IsStaticAndNotRegisteredAsAService|FullyQualifiedName~DashboardWidgetRegistry_ExposesFocusedDescriptorsAndKeepsCompatibilityLookup" -p:UseAppHost=false --verbosity minimal` passed with 2 tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 427 tests.
    - `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false -m:1` passed with 756 tests.
    - `git diff --check` passed with line-ending normalization warnings for edited files.
  - Next step: commit/PR/merge this static catalog cleanup, then inspect focused constant ownership for the next small cleanup slice.
- Dashboard digital visualization constant cleanup:
  - Merged PR #190 (`Make dashboard widget catalog static`) into `main`; post-merge Windows validation passed, with the package job skipped as optional.
  - Started `work/dashboard-visualization-constant-cleanup` from clean `main`.
  - Moved digital readout visual keys, defaults, option values, and normalizers from the general `DashboardWidgetCatalog` into `DashboardMetricDigitalVisualizationOptions`.
  - Updated the digital visualization module provider, reusable digital readout, digital visualization view, settings draft, widget formatting, and tests to depend on the focused digital owner.
  - Kept persisted key strings, visualization id, widget ids, defaults, schema, UI text, and rendering behavior unchanged.
  - Added guard coverage proving the general dashboard widget catalog no longer owns digital visual constants and the digital options class owns default/normalization behavior.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~DashboardWidgetCatalog_IsStaticAndNotRegisteredAsAService|FullyQualifiedName~DashboardMetricDigitalVisualizationOptions_OwnsDigitalVisualDefaults|FullyQualifiedName~DashboardMetricVisualizationCatalog_ProvidesMetricValueFoundation|FullyQualifiedName~DashboardWidgetSettingsDraft_WritesDigitalVisualizationSettingsOnlyWhenSelected" -p:UseAppHost=false --verbosity minimal` passed with 4 tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 428 tests.
    - `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false -m:1` passed with 757 tests.
    - `git diff --check` passed with line-ending normalization warnings for edited files.
  - Next step: commit/PR/merge this digital visual ownership cleanup, then inspect value visual constant ownership as the next small cleanup candidate.
- Dashboard value visualization constant cleanup:
  - Merged PR #191 (`Move digital visualization constants`) into `main`; post-merge Windows validation passed, with the package job skipped as optional.
  - Started `work/dashboard-value-visualization-constant-cleanup` from clean `main`.
  - Moved value visual keys, defaults, alignment values, placement values, and normalizers from the general `DashboardWidgetCatalog` into `DashboardMetricValueVisualizationOptions`.
  - Kept legacy `kpi.*` compatibility keys in `DashboardWidgetCatalog` so old saved widget config can still be read as fallback input.
  - Updated the value visualization module provider, value visualization view, inspector visual rows, metric designer preview config, settings draft, widget formatting, and tests to depend on the focused value owner.
  - Kept persisted key strings, visualization id, widget ids, defaults, schema, UI text, and rendering behavior unchanged.
  - Added guard coverage proving the general dashboard widget catalog no longer owns value or digital visual constants and the value options class owns default/normalization behavior.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~DashboardWidgetCatalog_IsStaticAndNotRegisteredAsAService|FullyQualifiedName~DashboardMetricValueVisualizationOptions_OwnsValueVisualDefaults|FullyQualifiedName~DashboardMetricVisualizationCatalog_ProvidesMetricValueFoundation|FullyQualifiedName~DashboardWidgetSettingsDraft_WritesValueVisualizationSettingsOnlyWhenSelected|FullyQualifiedName~DashboardMetricValueModuleAddsUnitVisibilityAndCustomUnitText" -p:UseAppHost=false --verbosity minimal` passed with 3 matching tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 429 tests.
    - `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false -m:1` passed with 758 tests.
    - `git diff --check` passed with line-ending normalization warnings for edited files.
  - Next step: commit/PR/merge this value visual ownership cleanup, then inspect gauge/chart constant ownership as the next small cleanup candidate.
- Dashboard gauge widget constant cleanup:
  - Merged PR #192 (`Move value visualization constants`) into `main`; post-merge Windows validation passed, with the package job skipped as optional.
  - Started `work/dashboard-gauge-constant-cleanup` from clean `main`.
  - Moved event gauge keys, defaults, style values, colors, and style normalization from the general `DashboardWidgetCatalog` into `DashboardEventGaugeWidgetOptions`.
  - Updated the event gauge module provider, inspector rows, widget settings draft, gauge renderer, gauge formatting, and tests to depend on the focused gauge owner.
  - Kept persisted key strings, `event.gauge` widget id, defaults, schema, UI text, and rendering behavior unchanged.
  - Added guard coverage proving the general dashboard widget catalog no longer owns gauge option constants and the gauge options class owns default/normalization behavior.
  - Current line movement before memory update:
    - 182 lines added.
    - 190 lines removed.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - Initial parallel focused test command collided with the simultaneous UI build on the generated XAML intermediate output.
    - Serial rerun of `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~DashboardEventGaugeWidgetOptions_OwnsGaugeDefaults|FullyQualifiedName~GaugeStyleOptions_ExposeOnlyImplementedShapes|FullyQualifiedName~AddDashboardWidget_AddsEventGaugeDefaults|FullyQualifiedName~DashboardWidgetFormatting_MapsGaugeMetricValueThroughConfiguredRange|FullyQualifiedName~DashboardWidgetCatalog_IsStaticAndNotRegisteredAsAService" -p:UseAppHost=false --verbosity minimal` passed with 5 tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 430 tests.
    - `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false -m:1` passed with 759 tests.
    - `git diff --check` passed with line-ending normalization warnings for the edited test file.
  - Next step: commit/PR/merge this gauge ownership cleanup, then inspect chart constant ownership as the next small cleanup candidate.
- CI fast validation split:
  - Merged PR #193 (`Move gauge widget constants`) into `main`; post-merge Windows validation passed, with the package job skipped as optional.
  - Started `work/ci-fast-validation` from clean `main`.
  - Split normal Windows validation from packaging so pull requests and pushes to `main` run only the fast validation path.
  - Moved release validation and package artifact creation into a manual-only workflow.
  - Removed the package job from the normal validation workflow so pull requests no longer show a skipped package stage.
  - Kept packaging available on demand for candidate/release checks; no app code, schema, dashboard, or test-studio behavior changed.
  - Verification:
    - Workflow files were inspected for tabs/trailing whitespace and normalized to LF.
    - Source grep confirmed the normal validation workflow no longer contains the package or release-validation steps; those live in the manual package workflow.
    - Local YAML parsing was unavailable because PyYAML is not installed in this shell.
    - `git diff --check` passed with line-ending normalization warnings for edited files.
  - Next step: commit/PR/merge this CI speedup, confirm the PR shows only normal validation, then continue with chart constant ownership cleanup.
- Dashboard chart constant cleanup:
  - Merged PR #194 (`Speed up normal Windows validation`) into `main`; post-merge Windows validation passed with only the fast validation job.
  - Started `work/dashboard-chart-constant-cleanup` from clean `main`.
  - Moved chart option key, chart type values, and chart type normalization from the general `DashboardWidgetCatalog` into `DashboardChartWidgetOptions`.
  - Updated the chart module provider, inspector rows, chart renderer, settings draft, widget editor dialog, and tests to depend on the focused chart owner.
  - Kept persisted key strings, chart widget ids, legacy `event.chart` compatibility, defaults, schema, UI text, and rendering behavior unchanged.
  - Added guard coverage proving the general dashboard widget catalog no longer owns chart option constants and the chart options class owns default/normalization behavior.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~DashboardChartWidgetOptions_OwnsChartDefaults|FullyQualifiedName~DashboardChartWidgetModuleProvider_OwnsChartWidgetDefinitions|FullyQualifiedName~AddDashboardWidget_NormalizesLegacyEventChart|FullyQualifiedName~DashboardWidgetCatalog_IsStaticAndNotRegisteredAsAService" -p:UseAppHost=false --verbosity minimal` passed with 3 matching tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 431 tests.
    - `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false -m:1` passed with 760 tests.
    - `git diff --check` passed with line-ending normalization warnings for edited files.
  - Next step: commit/PR/merge this chart ownership cleanup, then inspect whether remaining shared visual metric constants should move next or whether to pause cleanup and return to dashboard behavior work.
- Dashboard cell fit UX:
  - Merged PR #195 (`Move chart widget constants`) into `main`; post-merge Windows validation passed.
  - Started `work/dashboard-cell-fit-ux` from clean `main`.
  - Returned from cleanup to dashboard behavior/UX work.
  - Kept the existing responsive grid contract and focused this slice on cell adaptation.
  - Removed fixed edit-cell metric value font sizes so editor previews scale with cell width and height like live dashboard widgets.
  - Updated shared value and digital metric visuals to scale from both container width and height, improving short, wide, tall, and narrow cell behavior.
  - Lowered tablet/mobile runtime grid minimums through container-query variables so cells reflow before widgets feel crushed.
  - No dashboard schema, widget type, metric model, or FluxFlow change.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~DashboardDesigner_UsesContainerResponsiveGridForEditAndLive|FullyQualifiedName~DashboardWidgets_UseContainerResponsiveValueAndDigitalSizing|FullyQualifiedName~DashboardDesigner_EditPreviewKeepsMetricValuePlacement|FullyQualifiedName~DashboardDesigner_AppliesCellWidgetAlignmentToEditAndLiveViews" -p:UseAppHost=false --verbosity minimal` passed with 4 tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 431 tests.
    - `git diff --check` passed with line-ending normalization warnings for edited files.
  - Next step: commit/PR/merge this focused dashboard UX patch, then continue KPI/value visual polish if it passes review.
- Dashboard value/digital visual fit UX:
  - Merged PR #196 (`Improve dashboard cell adaptation`) into `main`; post-merge Windows validation passed.
  - Started `work/dashboard-value-fit-ux` from clean `main`.
  - Added value visual `Fit` ownership so KPI/value-style metric widgets can explicitly fill the cell or render compactly.
  - Added digital visual readout alignment and placement ownership so the selected digital representation can be positioned independently of the outer cell.
  - Wired value fit and digital alignment/placement through module defaults, property definitions, settings draft serialization, render components, and shared dashboard widget CSS.
  - Kept the change focused on existing KPI/value and digital visual behavior: no schema, widget type, metric model, or FluxFlow change.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~DashboardWidgetSettingsDraft_WritesValueVisualizationSettings|FullyQualifiedName~DashboardWidgetSettingsDraft_WritesDigitalVisualizationSettingsOnlyWhenSelected|FullyQualifiedName~DashboardMetricVisualizationCatalog_ProvidesMetricValueFoundation|FullyQualifiedName~DashboardMetricValueVisualizationOptions_OwnsValueVisualDefaults|FullyQualifiedName~DashboardMetricDigitalVisualizationOptions_OwnsDigitalVisualDefaults|FullyQualifiedName~DashboardMetricDigitalVisualization_UsesReusableReadoutComponent|FullyQualifiedName~DashboardMetricValueVisualization_UsesFitClassForEditorAndLiveParity" -p:UseAppHost=false --verbosity minimal` passed with 7 tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 432 tests.
    - `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false -m:1` passed with 761 tests.
    - `git diff --check` passed with line-ending normalization warnings for edited files.
  - Next step: commit/PR/merge this visual fit slice, then continue dashboard behavior work with the next one-widget discussion and manual UI feedback.
- Dashboard event counter visual UX:
  - Merged PR #197 (`Improve metric visual fit controls`) into `main`; post-merge Windows validation passed.
  - Started `work/dashboard-event-counter-visual-ux` from clean `main`.
  - Promoted `event.counter` from generic display rows to the focused metric visualization path.
  - Seeded counter widgets with value-visual defaults while preserving counter-specific title/subtitle text.
  - Removed the old counter-specific display property group so title, subtitle, unit, fit, alignment, padding, and colors are owned by the selected visual representation.
  - Kept metric query/binding behavior unchanged and avoided schema, widget type, metric model, or FluxFlow changes.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~DashboardWidgetSettingsDraft_WritesEventCounterAsMetricQueryConfiguration|FullyQualifiedName~DashboardWidgetModuleCatalog_ProvidesFocusedPropertyDefinitionsForAllPaletteWidgets|FullyQualifiedName~DashboardEventWidgetModuleProvider_OwnsEventWidgetDefinitions|FullyQualifiedName~AddDashboardWidget_WritesFocusedEventCounterWidgetAndMetricQuery|FullyQualifiedName~AddDashboardWidget_AddsWidgetAndAssignsSelectedSlot|FullyQualifiedName~DashboardEventCounterModuleView_UsesFocusedMetricValueRenderPath" -p:UseAppHost=false --verbosity minimal` passed with 6 tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 432 tests.
    - `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false -m:1` passed with 761 tests.
    - `git diff --check` passed with line-ending normalization warnings for edited files.
  - Next step: commit/PR/merge this event-counter visual UX slice, then continue one-widget-at-a-time dashboard work with `event.rate` visual ownership if review passes.
- Dashboard event rate visual UX:
  - Merged PR #198 (`Align event counter visual settings`) into `main`; post-merge Windows validation passed.
  - Started `work/dashboard-event-rate-visual-ux` from clean `main`.
  - Promoted `event.rate` from old rate display rows to the focused metric visualization path.
  - Seeded event-rate widgets with value-visual defaults while preserving rate-specific title/subtitle text.
  - Removed the old event-rate `Title`/`Unit`/`Decimals` property group so title, subtitle, unit, fit, alignment, padding, and colors are owned by the selected visual representation.
  - Kept the rate metric query/binding behavior unchanged and avoided schema, widget type, metric model, or FluxFlow changes.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~DashboardWidgetSettingsProfiles_ExposeDedicatedSettingsShape|FullyQualifiedName~DashboardWidgetSettingsDraft_WritesEventRateAsMetricQueryConfiguration|FullyQualifiedName~DashboardWidgetModuleCatalog_ProvidesFocusedPropertyDefinitionsForAllPaletteWidgets|FullyQualifiedName~AddDashboardWidget_AddsEventRateDefaults|FullyQualifiedName~DashboardEventCounterRateAndRateTileModuleViews_UseFocusedMetricValueRenderPath" -p:UseAppHost=false --verbosity minimal` passed with 4 matching tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 432 tests.
    - Initial full-solution run hit the known transient LiteDB storage test race in `SessionRepositoryTests.GetAll_ReturnsMostRecentFirst`; the isolated test rerun passed.
    - `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false -m:1` passed with 761 tests on rerun.
  - Next step: commit/PR/merge this event-rate visual UX slice, then continue one-widget-at-a-time dashboard work with `rate.tile` visual ownership if review passes.
- Dashboard rate tile visual UX:
  - Merged PR #199 (`Align event rate visual settings`) into `main`; post-merge Windows validation passed.
  - Started `work/dashboard-rate-tile-visual-ux` from clean `main`.
  - Promoted `rate.tile` from the old plain title/format default shape to the focused metric visualization path.
  - Seeded rate-tile widgets with value-visual defaults while preserving the rate-tile metric source behavior.
  - Removed stale rate-tile module property groups for local window, format, and threshold settings; metric definition owns query behavior and the selected visualization owns display behavior.
  - Kept the app-metric binding path unchanged and avoided schema, widget type, metric model, or FluxFlow changes.
  - Verification:
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~DashboardWidgetSettingsProfiles_ExposeDedicatedSettingsShape|FullyQualifiedName~DashboardWidgetSettingsDraft_WritesRateTileAsAppMetricConfiguration|FullyQualifiedName~DashboardWidgetModuleCatalog_ProvidesFocusedPropertyDefinitionsForAllPaletteWidgets|FullyQualifiedName~AddDashboardWidget_AddsRateTileDefaults|FullyQualifiedName~DashboardEventCounterRateAndRateTileModuleViews_UseFocusedMetricValueRenderPath" -p:UseAppHost=false --verbosity minimal` passed with 4 matching tests.
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 432 tests.
    - `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false -m:1` passed with 761 tests.
    - `git diff --check` passed with line-ending normalization warnings for edited files.
  - Next step: commit/PR/merge this rate-tile visual UX slice, then continue one-widget-at-a-time dashboard work by reviewing the next metric visual consumer before changing it.
- Dashboard status value visual UX:
  - Merged PR #200 (`Align rate tile visual settings`) into `main`; post-merge Windows validation passed.
  - Started `work/dashboard-status-value-visual-ux` from clean `main`.
  - Promoted `status.value` from old plain title/format rows to the focused metric visualization path used by its renderer.
  - Seeded status-value widgets with value-visual defaults while preserving the app-metric source behavior.
  - Removed stale status-value module ownership for the local `Value` select and local format rows; metric definition owns the source and the selected visualization owns display.
  - Kept this as a status-value alignment slice only: no new status visual, schema, widget type, metric model, or FluxFlow change.
  - Verification:
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~DashboardWidgetSettingsProfiles_ExposeDedicatedSettingsShape|FullyQualifiedName~DashboardWidgetSettingsDraft_WritesStatusValueAsAppMetricConfiguration|FullyQualifiedName~DashboardWidgetModuleCatalog_ProvidesFocusedPropertyDefinitionsForAllPaletteWidgets|FullyQualifiedName~AddDashboardWidget_AddsStatusValueDefaults|FullyQualifiedName~DashboardMetricValueWidgets_UseSharedVisualizationView|FullyQualifiedName~GetDashboardMetricValue_UsesAppMetricArtifactForStatusValue" -p:UseAppHost=false --verbosity minimal` passed with 6 matching tests.
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 432 tests.
    - `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false -m:1` passed with 761 tests.
    - `git diff --check` passed with line-ending normalization warnings for edited files.
  - Next step: commit/PR/merge this status-value visual UX slice, then decide whether `event.gauge` should become a focused gauge visualization consumer or stay gauge-specific for now.
- Dashboard gauge visualization UX:
  - Merged PR #201 (`Align status value visual settings`) into `main`; post-merge Windows validation passed.
  - Started `work/dashboard-gauge-visualization-ux` from clean `main`.
  - Promoted `event.gauge` to the shared metric visualization host with a new reusable `metric.radialGauge` visualization module.
  - Moved gauge shape, label, range, target, thresholds, and threshold colors into `metric.gauge.*` visualization-owned settings.
  - Kept legacy `gauge.*` keys as render/draft fallback only; new event-gauge defaults and saves write visualization-owned keys.
  - Removed event-gauge module ownership of hard-coded gauge/threshold property groups so the widget owns metric source and the visualization owns gauge display.
  - Kept this as an event-gauge alignment slice only: no new widget type, schema, metric model, or FluxFlow change.
  - Verification:
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~DashboardWidgetSettingsProfiles_ExposeDedicatedSettingsShape|FullyQualifiedName~DashboardWidgetSettingsDraft_WritesEventGaugeAsAppMetricConfiguration|FullyQualifiedName~DashboardWidgetModuleCatalog_ProvidesFocusedPropertyDefinitionsForAllPaletteWidgets|FullyQualifiedName~DashboardMetricVisualizationCatalog_ProvidesMetricValueFoundation|FullyQualifiedName~DashboardMetricVisualizationCatalog_ComposesExplicitProviderModules|FullyQualifiedName~AddDashboardWidget_AddsEventGaugeDefaults|FullyQualifiedName~DashboardMetricValueWidgets_UseSharedVisualizationView|FullyQualifiedName~GaugeStyleOptions_ExposeOnlyImplementedShapes" -p:UseAppHost=false --verbosity minimal` passed with 8 matching tests.
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 432 tests.
    - `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false -m:1` passed with 761 tests.
    - `git diff --check` passed with line-ending normalization warnings for edited files.
  - Next step: commit/PR/merge this event-gauge visual UX slice, then continue dashboard behavior/UX one widget at a time with manual review of gauge visual settings before selecting the next component.
- Dashboard latest event visual UX:
  - Merged PR #202 (`Align event gauge visual settings`) into `main`; post-merge Windows validation passed before this slice.
  - Started `work/dashboard-latest-event-visual-ux` from clean `main`.
  - Refactored `latest.event` so the widget owns focused latest-event display settings: header, field visibility, empty text, and latest-event text colors.
  - Kept event matching/filter behavior unchanged; this slice only separates display behavior from the old generic field toggles.
  - Updated edit-cell and live rendering to use the same latest-event visual component, with the widget owning its header instead of the outer dashboard chrome.
  - New latest-event defaults and saves write `latest.*` visual keys while existing legacy field keys still load as fallback.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --filter "FullyQualifiedName~DashboardEventFilterCatalogTests" --verbosity minimal` passed with 115 tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 433 tests.
    - `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false -m:1` passed with 762 tests.
    - `git diff --check` passed with line-ending normalization warnings for edited files.
  - Next step: commit/PR/merge this latest-event visual UX slice, then continue one-widget-at-a-time with `event.table` only after visual review passes.
- Dashboard event table visual UX:
  - Merged PR #203 (`Align latest event visual settings`) into `main`; post-merge Windows validation passed before this slice.
  - Started `work/dashboard-event-table-visual-ux` from clean `main`.
  - Refactored `event.table` so table display settings are owned by focused `table.*` visual keys: header, row count, density, column visibility, empty text, and table colors.
  - Kept event matching/filter behavior unchanged; this slice only separates table display behavior from the old raw table rows.
  - Updated edit-cell and live rendering to use the same table visual component, with the widget owning its header instead of the outer dashboard chrome.
  - New event-table defaults and saves write `table.*` visual keys while existing legacy `rowCount`, `density`, and `payloadPreview` keys still load as fallback.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --filter "FullyQualifiedName~DashboardEventFilterCatalogTests" --verbosity minimal` passed with 116 tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 434 tests.
    - `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false -m:1` passed with 763 tests.
    - `git diff --check` passed with line-ending normalization warnings for edited files.
  - Next step: commit/PR/merge this event-table visual UX slice, then continue one-widget-at-a-time with the next dashboard component only after review passes.
- Dashboard topic tree visual UX:
  - Merged PR #204 (`Align event table visual settings`) into `main`; post-merge Windows validation passed before this slice.
  - Started `work/dashboard-topic-tree-visual-ux` from clean `main`.
  - Refactored `topic.tree` so the topic-tree component owns focused display settings: header, summary visibility, topic/message counters, system topic visibility, empty text, and tree colors.
  - Kept live topic data behavior unchanged; this slice only separates topic-tree presentation from the generic dashboard shell and removes unused `depth`/`badges` property metadata.
  - Updated edit-cell and live rendering to use the same topic-tree visual component, with the widget owning its header instead of the outer dashboard chrome.
  - New topic-tree defaults and saves write `topic.tree.*` visual keys while existing `title` and system-topic settings still load as fallback.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --filter "FullyQualifiedName~DashboardEventFilterCatalogTests" --verbosity minimal` passed with 116 tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 434 tests.
    - `dotnet test FluxMq.sln --no-restore --verbosity minimal` passed with 763 tests.
    - `git diff --check` passed with line-ending normalization warnings for edited files.
  - Next step: commit/PR/merge this topic-tree visual UX slice, then continue one-widget-at-a-time by reviewing topic activity/top topics before changing it.
- Dashboard topic activity visual UX:
  - Merged PR #205 (`Align topic tree visual settings`) into `main`; post-merge Windows validation passed before this slice.
  - Started `work/dashboard-topic-activity-visual-ux` from clean `main`.
  - Refactored `topic.activity` so the topic activity visual owns focused display settings: header, topic limit, count visibility, empty text, and visual colors.
  - Kept topic projection/event filtering behavior unchanged; this slice only separates topic activity presentation from the generic dashboard shell and removes unused category/palette property metadata.
  - Updated edit-cell and live rendering to use the same topic activity visual component, with the widget owning its header instead of the outer dashboard chrome.
  - New topic-activity defaults and saves write `topic.activity.*` visual keys while existing `title` and legacy `limit` still load as fallback.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --filter "FullyQualifiedName~DashboardEventFilterCatalogTests|FullyQualifiedName~FlowDefinitionComposerTests" --verbosity minimal` passed with 227 tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 435 tests.
    - `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false -m:1` passed with 764 tests.
    - `git diff --check` passed with line-ending normalization warnings for edited files.
  - Next step: commit/PR/merge this topic-activity visual UX slice before selecting the next dashboard component.
- Dashboard line chart visual UX:
  - Merged PR #206 (`Align topic activity visual settings`) into `main`; post-merge Windows validation passed before this slice.
  - Started `work/dashboard-line-chart-visual-ux` from clean `main`.
  - Refactored `chart.line` so the line chart visual owns focused display settings: header, grid visibility, label visibility, point visibility, line width, empty text, and line/grid/label colors.
  - Kept event matching, windowing, and bucket calculation behavior unchanged; this slice only separates line-chart presentation from the old shared chart renderer/type switch.
  - Updated edit-cell and live rendering to use a focused line-chart component, with the widget owning its header instead of the outer dashboard chrome.
  - New line-chart defaults and saves write `chart.line.*` visual keys while existing `showGrid`, `showLabels`, `showPoints`, and `lineColor` keys still load as fallback.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --filter "FullyQualifiedName~DashboardEventFilterCatalogTests|FullyQualifiedName~FlowDefinitionComposerTests" --verbosity minimal` passed with 228 tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 436 tests.
    - `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false -m:1` passed with 765 tests.
    - `git diff --check` passed with line-ending normalization warnings for edited files.
  - Next step: commit/PR/merge this line-chart visual UX slice, then review `chart.line` manually before selecting the next dashboard component.
- Dashboard area chart visual UX:
  - Merged PR #207 (`Align line chart visual settings`) into `main`; post-merge Windows validation passed before this slice.
  - Started `work/dashboard-area-chart-visual-ux` from clean `main`.
  - Refactored `chart.area` so the area chart visual owns focused display settings: header, grid visibility, label visibility, point visibility, line width, fill opacity, empty text, and line/fill/grid/label colors.
  - Kept event matching, windowing, and bucket calculation behavior unchanged; this slice only separates area-chart presentation from the old shared chart renderer/type switch.
  - Updated edit-cell and live rendering to use a focused area-chart component, with the widget owning its header instead of the outer dashboard chrome.
  - New area-chart defaults and saves write `chart.area.*` visual keys while existing `showGrid`, `showLabels`, `showPoints`, `lineColor`, `fillColor`, and `fillOpacity` keys still load as fallback.
  - FluxFlow remained unchanged.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --filter "FullyQualifiedName~DashboardEventFilterCatalogTests|FullyQualifiedName~FlowDefinitionComposerTests" --verbosity minimal` passed with 229 tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 437 tests.
    - `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false -m:1` passed with 766 tests.
    - `git diff --check` passed with line-ending normalization warnings for edited files.
  - Next step: commit/PR/merge this area-chart visual UX slice, then review `chart.area` manually before selecting `chart.bar`.
- Dashboard bar chart visual UX:
  - Merged PR #208 (`Align area chart visual settings`) into `main`; post-merge Windows validation passed before this slice.
  - Started `work/dashboard-bar-chart-visual-ux` from clean `main`.
  - Refactored `chart.bar` so the bar chart visual owns focused display settings: header, grid visibility, label visibility, orientation, bar radius, empty text, and bar/grid/label colors.
  - Kept event matching, windowing, and bucket calculation behavior unchanged; this slice only separates bar-chart presentation from the old shared chart renderer/type switch.
  - Updated edit-cell and live rendering to use a focused bar-chart component, with the widget owning its header instead of the outer dashboard chrome.
  - New bar-chart defaults and saves write `chart.bar.*` visual keys while existing `showGrid`, `showLabels`, `barColor`, and `orientation` keys still load as fallback.
  - FluxFlow remained unchanged.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --filter "FullyQualifiedName~DashboardEventFilterCatalogTests|FullyQualifiedName~FlowDefinitionComposerTests" --verbosity minimal` passed with 230 tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 438 tests.
    - `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false -m:1` passed with 767 tests.
    - `git diff --check` passed with line-ending normalization warnings for edited files.
  - Next step: commit/PR/merge this bar-chart visual UX slice, then review `chart.bar` manually before selecting the next dashboard component.
- Metric attribute filter JSON compatibility:
  - Merged PR #209 (`Align bar chart visual settings`) into `main`; post-merge Windows validation passed before this slice.
  - Started `fix/metric-attribute-filter-json` from clean `main` after app startup failed on metric `additionalFilters.attributes`.
  - Fixed dashboard metric promotion so legacy nested `filters.attributes` values are written as flat metric additional filters such as `attribute:qos`.
  - Added tolerant metric additional-filter JSON reading so already-saved app metrics with nested `additionalFilters.attributes` load without crashing.
  - Aligned configuration/CLI loading with workspace migration so validation uses the same normalized app definition shape.
  - Verified the local file `C:\Users\meisa\OneDrive\Documents\FluxMQ\app1.json` now validates with `isValid: true`.
  - Verification:
    - `dotnet test tests\FluxMq.App.Tests\FluxMq.App.Tests.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 121 tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 438 tests.
    - `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false -m:1` passed with 770 tests.
    - `dotnet run --project src\FluxMq.Cli\FluxMq.Cli.csproj -- validate --config C:\Users\meisa\OneDrive\Documents\FluxMQ\app1.json --output json` returned `isValid: true`.
  - Next step: commit/PR/merge this focused compatibility fix, then return to one-dashboard-component-at-a-time review.
- Dashboard donut chart visual UX:
  - Merged PR #210 (`Fix metric attribute filter loading`) into `main`; post-merge Windows validation passed before this slice.
  - Started `work/dashboard-donut-chart-visual-ux` from clean `main`.
  - Refactored `chart.donut` so the donut chart visual owns focused display settings: header, legend visibility, center total visibility, category limit, hole size, empty text, five segment colors, label color, and muted color.
  - Kept event matching, windowing, and topic breakdown behavior unchanged; this slice only separates donut presentation from the old generic series-bars/chart-adapter path.
  - Updated edit-cell and live rendering to use a focused donut-chart component, with the widget owning its header instead of the outer dashboard chrome.
  - New donut-chart defaults and saves write `chart.donut.*` visual keys while existing `title` and legacy `limit` still load as fallback; old generic `primaryMetric`, `groupBy`, and `palette` keys are not written back for donut.
  - Verification so far:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore --verbosity minimal -p:UseAppHost=false` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~DashboardEventFilterCatalogTests" -p:UseAppHost=false --verbosity minimal` passed with 121 tests.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseAppHost=false --verbosity minimal` passed with 439 tests.
    - `dotnet test FluxMq.sln --no-restore --verbosity minimal -p:UseAppHost=false -m:1` passed with 771 tests.
    - `git diff --check` passed with line-ending normalization warnings for edited files.
  - Next step: review `chart.donut` manually before selecting the next dashboard component.
- Metric source framework redesign (2026-06-13), branch `work/metric-source-framework-redesign`:
  - Replaced the generic `source + measure + filters` metric model with focused one-class-per-metric **metric types**. A metric is now a `FluxMetricResourceDefinition` (`typeId` + flat type-owned `parameters`, no `additionalFilters.attributes` bag). Dashboards/pipelines select metric resources and consume `FluxMetricReading<double>` streams.
  - New contracts (`FluxMq.App.Metrics`): `IFluxMetricType<TValue>` (owns type id, display, parameter descriptors, validation, summary, source creation), `IFluxMetricSource<TValue>` (`ISourceBlock<FluxMetricReading<TValue>>`), shared `EventWindowMetricSource` mechanics + `EventFilter` (FlowEvent field knowledge), and six type classes — `event.count`, `event.rate`, `topic.unique-count`, `payload.bytes`, `payload.average`, `message.retained`. Types resolved from DI by `typeId` via `IFluxMetricTypeRegistry`.
  - `FluxMetricRuntimeHost` (and its keyed stream modules) replaced by a small `FluxMetricStreamHost` that only configures, caches, starts/stops, and exposes streams — no metric calculation, filter parsing, event-field knowledge, or measure switching. `metric.source` node, app validation, the JSON migrator (`measure`->`typeId`), and `FlowApplicationHost` startup all run on the resource model.
  - UI: Metrics page creates resources from registered types and renders type-owned parameter rows; dashboard KPI/counter/rate widgets and the `metric.source` widget select a resource + optional parameter overrides. Dashboard value resolution maps `typeId` -> `DashboardEventSnapshot` aggregate.
  - Migrated the `operations-dashboard-test-studio` sample (and docs-site copy) from dashboard-local metrics to app-level `flowApplication.metrics` resources.
  - Deleted the fully-replaced old runtime (`FluxMetricRuntime.cs`, `FluxMetricStreamModules.cs`). The legacy generic **query** classes (`FluxMetricCatalog`/`FluxMetricDefinition`/`FluxMetricQueryDraft`/`FluxMetricEvaluationEngine`/`FluxMetricValidator`/`FluxMetricPreviewService`/`FluxMetricQuerySummary`/`FluxMetricArtifactDefinition`/`FluxMetricResolver`) remain because the dashboard chart/topic/payload widget query-authoring UI (`DashboardMetricQueryBuilder`/`Dialog`/`Mapper`/`Preview`/`Summary` + the inspector inline editor) still depends on them — a separate dashboard-viz concern, like the out-of-scope `event.*` widgets. Fully deleting them needs a follow-up that re-points that chart-widget query authoring at metric resources.
  - Verification: `dotnet test FluxMq.sln` green (Core 44, Scenarios 36, Components 113, App 132, Cli 18, UI 439 = 782); `git diff --check` clean. Out of scope (unchanged): `mqtt.metrics` inline node, dashboard `event.*` widgets, scenario `assert.metric.threshold`.
- Metrics tab type-picker polish:
  - Replaced the inline metric type dropdown in the Metrics editor with a compact read-only type summary plus an explicit `Change` action.
  - Added a focused type-change dialog with search, current-type marking, selected-type details, parameter default preview, and a single clear action that resets draft parameters to the chosen descriptor defaults.
  - Kept app JSON, metric schema, runtime behavior, and descriptor defaults unchanged; this is UI workflow polish only.
  - Verification:
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-tests-verify\` passed with 410 tests.
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-verify\` passed with 0 warnings after rerunning without the parallel test build file lock.
  - Next step: manually smoke the Metrics type-change dialog in the running desktop app and then continue with parameter field density/alignment polish.
- Metrics tab parameter-field polish:
  - Replaced full-height MudBlazor parameter form controls in the Metrics editor with compact inspector-style controls that still write to the same draft parameter dictionary.
  - Topic/text parameters keep wider grid spans, short numeric/select/duration/toggle parameters use compact spans, and the narrow breakpoint stacks every parameter cleanly.
  - Moved parameter help into small tooltip icons and kept min/max/required metadata in tiny label chips so the section stays aligned without always-visible helper text.
  - Kept validation, descriptor-driven defaults, metric schema, and runtime behavior unchanged.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-verify\` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-tests-verify\` passed with 410 tests.
  - Next step: manually smoke parameter editing in the desktop app, then polish the editor header/action strip if the form still feels cramped.
- Metrics tab editor-header polish:
  - Replaced the editor header's warning chip, Mud icon button strip, and Mud save button with one flat title/status/action bar.
  - The header now shows a compact type icon, display name, parameter summary, resource id metadata, saved/unsaved/error state pill, native icon actions, and a slim Save button.
  - Added responsive wrapping so the state/actions align cleanly when the editor header stacks.
  - Kept save/cancel/duplicate/rename/delete behavior unchanged.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-verify\` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-tests-verify\` passed with 410 tests.
  - Next step: manually smoke the Metrics editor header/action strip, then polish the side rail/read-only cards if the screen still feels noisy.
- Metrics tab side-rail polish:
  - Flattened the Metrics editor read-only side rail so type details, live preview, references, and validation read as one quiet context panel instead of heavier mini-cards.
  - Added compact heading badges for value kind, live/idle state, reference count, and validation count.
  - Reduced live preview height/value scale, shortened idle copy, removed duplicate type-kind text, and made reference rows smaller with softer separators.
  - Kept `TryGetLatestMetricReading`, dashboard reference summaries, validation messages, metric schema, and runtime behavior unchanged.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-verify\` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-tests-verify\` passed with 410 tests.
  - Next step: manually smoke the full Metrics editor in the desktop app, then tighten the metric list/empty states if anything still reads noisy.
- Metrics tab list/empty-state polish:
  - Replaced generic Metrics list/editor empty placeholders with compact local empty states and native flat actions for `New metric` and `Reset filters`.
  - Tightened metric list rows, header height, latest-value pills, reference-count pills, and narrow-width row height.
  - Added stable row keys for filtered/selected metric rows.
  - Kept list filtering, selection, creation, latest reading, reference counts, metric schema, and runtime behavior unchanged.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-verify\` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-tests-verify\` passed with 410 tests.
  - Next step: manually smoke the full Metrics tab in the desktop app across empty, filtered, and populated states, then do a final visual sweep for any remaining alignment issues.
- Metrics tab command-bar alignment polish:
  - Replaced the last MudBlazor toolbar button in the Metrics command bar with a native flat `New metric` action so it matches search, type filter, reset, and the editor controls.
  - Tightened command-bar padding/gaps and normalized button icon/text geometry.
  - Kept creation behavior, dialogs, filtering, metric schema, and runtime behavior unchanged.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-verify\` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-tests-verify\` passed with 410 tests.
  - Next step: manually smoke the full Metrics tab in the desktop app, then stop UI polish unless a concrete visual issue remains.
- Metrics tab identity-field alignment polish:
  - Replaced the remaining heavier display-name and description inputs in the Metrics editor with the same compact native inspector field language used by the parameter editor.
  - Removed the now-unused scoped editor input overrides, leaving the id, display name, type, and description blocks on one flat visual system.
  - Kept draft editing, dirty detection, validation, metric schema, and runtime behavior unchanged.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-verify\` passed with 0 warnings.
    - First focused UI test run timed out at 2 minutes before reporting results; rerunning with a longer timeout passed.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-tests-verify\` passed with 410 tests.
  - Next step: manually smoke the full Metrics tab in the desktop app, then stop UI polish unless a concrete visual issue remains.
- Metrics tab edit-grid alignment polish:
  - Normalized the compact identity grid so metric id, display name, type, and description cells stretch to the same row rhythm.
  - Brought the metric id cell onto the same 50px minimum height and padding language as the editable identity fields, with tighter internal row spacing.
  - Kept all draft editing, rename, validation, schema, and runtime behavior unchanged.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-verify\` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-tests-verify\` passed with 410 tests.
  - Next step: manually inspect the Metrics tab in the desktop app; with no concrete visual issue left, move to the next requested product slice.
- Metrics tab validation de-duplication:
  - Removed the full-width validation alert strip from the Metrics editor so validation is no longer represented three times for the same issue.
  - Kept the compact header state as the save/action status and the side-rail validation block as the single detailed error list.
  - Removed the now-unused validation-strip CSS.
  - Kept validation rules, save disabling, draft behavior, schema, and runtime behavior unchanged.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-verify\` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-tests-verify\` passed with 410 tests.
  - Next step: visually confirm invalid metric editing now shows one detailed validation area, then move to field-level validation only if needed.
- Metrics tab validation-state simplification:
  - Hid the editor header state pill while validation errors exist, so invalid metrics no longer show `Needs attention` in the header and detailed validation in the side rail.
  - Kept the header state pill for normal `Saved` and `Unsaved` states only.
  - Added a quiet disabled-save title with the validation summary and removed unused header error-state CSS.
  - Kept validation rules, side-rail validation details, save disabling, draft behavior, schema, and runtime behavior unchanged.
  - Verification:
    - First `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-verify\` attempt timed out before reporting; rerun passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-tests-verify\` passed with 410 tests.
  - Next step: visually confirm invalid metrics now show only the side-rail validation details, then consider field-level error highlighting if discoverability needs more help.
- Metrics tab field-level validation hint:
  - Added a shared `MetricDesignerState.ValidateParameter(...)` helper so parameter-field highlighting uses the same rules as the side-rail validation list.
  - Invalid parameter controls now get subtle label/control tint and `aria-invalid`, but no inline error sentence, keeping the side rail as the only detailed validation text.
  - Added focused UI-state test coverage for missing topic and QoS range parameter validation.
  - Kept save disabling, side-rail validation details, draft behavior, metric schema, and runtime behavior unchanged.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-verify\` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-tests-verify\` passed with 411 tests.
  - Next step: visually confirm the invalid Topic filter field is highlighted without repeating the error sentence, then stop validation polish unless another concrete issue appears.
- Metrics tab identity validation hint:
  - Added shared `MetricDesignerState.ValidateMetricId(...)` and `ValidateDisplayName(...)` helpers so identity highlighting uses the same messages as draft validation.
  - Metric id, display name, and unknown type blocks now get subtle invalid tint and `aria-invalid` without adding another visible error sentence.
  - Added focused UI-state test coverage for empty display name, invalid metric id, duplicate metric id, and valid identity state.
  - Kept side-rail validation as the single detailed validation text source; save disabling, rename flow, schema, and runtime behavior are unchanged.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-verify\` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-tests-verify\` passed with 412 tests.
  - Next step: visually confirm empty display name and invalid topic states both highlight locally while keeping only one detailed validation list.
- Metrics tab validation navigation:
  - Changed the side-rail validation list from plain strings to structured validation items with optional target field ids.
  - Validation rows now link to the affected metric id, display name, metric type, or parameter field, while still keeping the side rail as the only detailed validation text.
  - Added stable field ids and scroll margin to the compact editor fields; rows without a target still render as quiet non-link validation lines.
  - Kept validation rules, field hints, save disabling, metric schema, and runtime behavior unchanged.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-verify\` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-tests-verify\` passed with 412 tests.
  - Next step: visually click validation rows in the Metrics side rail and confirm the editor scrolls to the highlighted field.
- Metrics tab validation target highlight:
  - Added a scoped `:target` outline for metric id, identity, type, and parameter fields so validation-row links visibly land on the affected compact field.
  - Kept the effect flat and non-layout-shifting with outline/box-shadow only; no JavaScript or duplicate validation text was added.
  - Kept validation rules, field ids, side-rail validation links, schema, and runtime behavior unchanged.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-verify\` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-tests-verify\` passed with 412 tests.
  - Next step: manually click a Metrics validation row and confirm the target field scrolls into view with the flat focus outline.
- Metrics tab validation accessibility wiring:
  - Added stable message ids to the side-rail validation rows and wired invalid metric id, display name, metric type, and parameter controls with `aria-errormessage`.
  - Added `tabindex="-1"` to validation target wrappers so linked compact fields can act as focus targets without entering the normal tab order.
  - Kept the side rail as the single detailed validation text surface; no duplicate inline error messages or JavaScript were added.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-verify\` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-tests-verify\` passed with 412 tests.
  - Next step: manually confirm screen-reader/keyboard behavior for invalid Metrics fields if doing a full accessibility pass.
- Metrics tab reference row polish:
  - Reworked side-rail reference rows into compact dashboard/widget rows with separate widget and dashboard text, quiet type/primary tags, title text, and a small open-dashboard icon action.
  - The open action uses the existing workspace dashboard selection path and prompts through the Metrics dirty-discard dialog before leaving an unsaved draft.
  - Kept metric reference data, dashboard bindings, schema, latest-reading behavior, and runtime behavior unchanged.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-verify\` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-tests-verify\` passed with 412 tests.
    - `git diff --check -- src\FluxMq.UI\Components\Workspace\MetricDesigner.razor src\FluxMq.UI\Components\Workspace\MetricDesigner.razor.css` passed with the existing LF-to-CRLF warning for the Razor file.
  - Next step: manually click a Metrics reference open action with and without dirty editor changes to confirm the prompt and dashboard switch feel right.
- Metrics tab reference landing polish:
  - Extended the UI-only metric reference summary with the dashboard cell that hosts each referenced widget.
  - Added a workspace service method that activates a dashboard and optional dashboard cell in one notification, then changed the Metrics reference open action to use it.
  - Updated the dashboard designer to seed its selected cell from the workspace service when opening a dashboard from a Metrics reference, so the reference action lands on the widget cell when available.
  - Added focused tests for reference summary cell names and combined dashboard/cell activation.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-verify\` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-tests-verify\` passed with 413 tests.
    - `git diff --check` over the touched Metrics/dashboard/service/test files passed with existing LF-to-CRLF warnings.
  - Next step: manually click a Metrics reference open action and confirm the dashboard opens in edit mode with the referenced widget cell selected.
- Metrics tab reference location display:
  - Added muted dashboard-cell labels to Metrics side-rail reference rows when a referenced widget has a resolved cell.
  - Added the same quiet cell label to the metric delete confirmation reference list so destructive review shows where each binding lives.
  - Cleaned up reference action markup alignment in the Metrics side rail.
  - Kept reference data, bindings, navigation behavior, metric schema, and runtime behavior unchanged.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-verify\` passed with 0 warnings.
    - The first focused UI test run timed out before reporting results; rerunning with a longer timeout passed.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-tests-verify\` passed with 413 tests.
    - Whitespace checks over the touched markup/CSS files passed with the existing LF-to-CRLF warning for the tracked Razor file.
  - Next step: manually inspect Metrics references and the delete dialog to confirm the cell labels are useful but not visually noisy.
- Metrics tab reference location label polish:
  - Kept the technical dashboard cell id on metric reference summaries for navigation, and added a separate display label derived from dashboard cell coordinates.
  - Metrics reference rows and the delete confirmation now show readable labels such as `Cell R1 C1` instead of raw ids such as `Cell cell`.
  - Merged cells include their span in the label, while existing dashboard bindings, widget selection, app JSON, metric schema, and runtime behavior remain unchanged.
  - Added a focused assertion for the new reference summary display label.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-verify\` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-tests-verify\` passed with 413 tests.
    - `git diff --check` over the touched reference-summary/composer/markup/test files passed with existing LF-to-CRLF warnings.
  - Next step: manually confirm the Metrics reference side rail and delete dialog show `Cell R1 C1` style labels clearly at desktop and narrow widths.
- Metrics tab reference row de-clutter:
  - Moved dashboard/cell location text into the secondary line under each referenced widget instead of rendering cell location as a separate tag.
  - Reduced the delete-confirmation reference row from five columns to a calmer two-line main text plus type/primary metadata.
  - Removed the now-unused muted tag styling from the Metrics side rail and delete dialog.
  - Kept reference navigation, binding summaries, app JSON, metric schema, and runtime behavior unchanged.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-verify\` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-tests-verify\` passed with 413 tests.
    - Whitespace checks over the touched markup/CSS files passed with the existing LF-to-CRLF warning for the tracked Razor file.
  - Next step: manually inspect the Metrics reference list and delete dialog at narrow width to confirm the two-line reference rows stay compact.
- Metrics tab reference row responsive polish:
  - Added narrow-width layout rules for Metrics side-rail reference rows so the icon, main text, metadata, and open action stack into stable tracks instead of squeezing type/primary against the location line.
  - Added matching compact dialog rules so delete-confirmation reference metadata drops below the main reference text on small screens.
  - Kept reference data, navigation behavior, app JSON, metric schema, and runtime behavior unchanged.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-verify\` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-tests-verify\` passed with 413 tests.
    - Whitespace checks over the touched CSS files passed.
  - Next step: manually resize the Metrics tab/delete dialog to narrow width and confirm the reference rows stay readable without horizontal pressure.
- Metrics tab reference title/action wording polish:
  - Updated Metrics reference row titles and open-action labels to use the readable dashboard/cell location line, for example `ops · Cell R1 C1`.
  - Added matching title text to delete-confirmation reference rows so truncated rows still expose widget, location, type, and primary-binding context on hover.
  - Kept visible row layout, reference data, navigation behavior, app JSON, metric schema, and runtime behavior unchanged.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-verify\` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-tests-verify\` passed with 413 tests.
    - Whitespace checks over the touched Razor files passed with the existing LF-to-CRLF warning for the tracked Metrics file.
  - Next step: manually hover/focus the Metrics reference open action and delete-dialog reference rows to confirm the wording is clear.
- Metrics tab type context simplification:
  - Removed the separate right-rail `Type details` card because the selected type is already part of the main editor workflow.
  - Folded the type id, unit, and parameter count into the existing compact type picker line, with the descriptor description kept as hover text.
  - Narrowed the read-only side rail so live preview, references, and validation stay focused while the main edit panel gets more usable width.
  - Kept metric definitions, type switching, validation, live preview, references, app JSON, and runtime behavior unchanged.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-verify\` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-tests-verify\` passed with 413 tests.
    - `git diff --check -- src\FluxMq.UI\Components\Workspace\MetricDesigner.razor src\FluxMq.UI\Components\Workspace\MetricDesigner.razor.css` passed with the existing LF-to-CRLF warning for the tracked Metrics file.
    - Whitespace checks over the touched Razor/CSS files passed.
  - Next step: manually inspect the Metrics editor at desktop width and confirm the side rail feels calmer with preview/references/validation only.
- Metrics tab idle preview flattening:
  - Changed the Metrics side-rail idle live-preview state from a bordered mini-card with two visible text lines to a single quiet status row.
  - Kept the richer live value card only for actual readings, and preserved the stopped/not-emitted detail as hover text.
  - Removed stale muted-preview-card styling from the scoped Metrics CSS.
  - Kept latest-reading lookup, formatting, app JSON, metric schema, and runtime behavior unchanged.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-verify\` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-tests-verify\` passed with 413 tests.
    - `git diff --check -- src\FluxMq.UI\Components\Workspace\MetricDesigner.razor src\FluxMq.UI\Components\Workspace\MetricDesigner.razor.css` passed with the existing LF-to-CRLF warning for the tracked Metrics file.
    - Whitespace checks over the touched Razor/CSS files passed.
  - Next step: manually inspect the Metrics side rail with the app stopped and confirm the idle preview reads as quiet status text, not another card.
- Metrics tab live-preview badge de-duplication:
  - Removed the idle badge from the `Live preview` heading so the stopped state is shown once through the quiet `No reading` row.
  - Kept the `Live` badge when an actual latest metric reading exists.
  - Deleted the now-unused muted side-badge styling from the scoped Metrics CSS.
  - Kept latest-reading lookup, formatting, app JSON, metric schema, and runtime behavior unchanged.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-verify\` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-tests-verify\` passed with 413 tests.
    - `git diff --check -- src\FluxMq.UI\Components\Workspace\MetricDesigner.razor src\FluxMq.UI\Components\Workspace\MetricDesigner.razor.css` passed with the existing LF-to-CRLF warning for the tracked Metrics file.
    - Whitespace checks over the touched Razor/CSS files passed.
  - Next step: manually compare the side rail with and without a live reading to confirm only the active state gets the `Live` badge.
- Metrics tab empty reference badge de-duplication:
  - Hid the side-rail `References` count badge when the selected metric has zero dashboard bindings.
  - Kept the count badge when references exist, so non-empty binding state remains visible at a glance.
  - Kept the existing `No dashboard bindings.` empty row as the single empty-state message.
  - Kept reference summaries, dashboard navigation, app JSON, metric schema, and runtime behavior unchanged.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-verify\` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-tests-verify\` passed with 413 tests.
    - `git diff --check -- src\FluxMq.UI\Components\Workspace\MetricDesigner.razor src\FluxMq.UI\Components\Workspace\MetricDesigner.razor.css` passed with the existing LF-to-CRLF warning for the tracked Metrics file.
    - Whitespace checks over the touched Razor/CSS files passed.
  - Next step: manually inspect a metric with no dashboard bindings and one with bindings to confirm the References badge appears only when useful.
- Metrics tab saved-state header de-clutter:
  - Removed the always-visible `Saved` pill from the Metrics editor header so clean drafts do not add a redundant status chip beside the action buttons.
  - Kept the `Unsaved` pill for dirty drafts while validation is otherwise clear, preserving the visible prompt to save or cancel changes.
  - Removed the now-unused saved-state dot styling from the scoped Metrics CSS.
  - Kept draft save/cancel behavior, validation behavior, app JSON, metric schema, and runtime behavior unchanged.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-verify\` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-tests-verify\` passed with 413 tests.
    - `git diff --check -- src\FluxMq.UI\Components\Workspace\MetricDesigner.razor src\FluxMq.UI\Components\Workspace\MetricDesigner.razor.css` passed with the existing LF-to-CRLF warning for the tracked Metrics file.
    - Whitespace checks over the touched Razor/CSS files passed.
  - Next step: manually inspect the Metrics editor header before and after editing a field to confirm the status chip appears only for unsaved clean-validation drafts.
- Metrics tab clean-header action de-clutter:
  - Hid the header `Cancel changes` icon while the selected metric draft is clean, instead of rendering a disabled no-op action.
  - Kept the cancel command visible as soon as the draft becomes dirty, including invalid dirty drafts where cancel remains useful.
  - Kept save, duplicate, rename, delete, validation, app JSON, metric schema, and runtime behavior unchanged.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-verify\` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-tests-verify\` passed with 413 tests.
    - `git diff --check -- src\FluxMq.UI\Components\Workspace\MetricDesigner.razor src\FluxMq.UI\Components\Workspace\MetricDesigner.razor.css` passed with the existing LF-to-CRLF warning for the tracked Metrics file.
    - Whitespace checks over the touched Razor/CSS files passed.
  - Next step: manually inspect the Metrics editor header before and after editing a field to confirm the cancel icon appears only when there are draft changes to discard.
- Metrics tab clean-save action de-clutter:
  - Hid the header `Save` button while the selected metric draft is clean, instead of rendering a disabled no-op action.
  - Kept the `Save` button visible for dirty drafts, with existing validation disabling and validation-summary title text intact.
  - Simplified the save tooltip/title helper because the clean `No changes to save` branch no longer renders.
  - Kept save/cancel behavior, duplicate, rename, delete, validation, app JSON, metric schema, and runtime behavior unchanged.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-verify\` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-tests-verify\` passed with 413 tests.
    - `git diff --check -- src\FluxMq.UI\Components\Workspace\MetricDesigner.razor src\FluxMq.UI\Components\Workspace\MetricDesigner.razor.css` passed with the existing LF-to-CRLF warning for the tracked Metrics file.
    - Whitespace checks over the touched Razor/CSS files passed.
  - Next step: manually inspect the Metrics editor header before and after editing a field to confirm the Save button appears only when there are draft changes to save.
- Metrics tab dirty-rename action de-clutter:
  - Hid the header rename icon while the selected metric draft is dirty, instead of rendering a disabled no-op action.
  - Hid the inline metric-id row rename button while dirty as well, keeping rename visible only in the clean-draft workflow where it can run.
  - Removed the now-unused dirty rename tooltip helper.
  - Kept rename behavior, dashboard binding rename updates, save/cancel behavior, app JSON, metric schema, and runtime behavior unchanged.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-verify\` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-tests-verify\` passed with 413 tests.
    - `git diff --check -- src\FluxMq.UI\Components\Workspace\MetricDesigner.razor src\FluxMq.UI\Components\Workspace\MetricDesigner.razor.css` passed with the existing LF-to-CRLF warning for the tracked Metrics file.
    - Whitespace checks over the touched Razor/CSS files passed.
  - Next step: manually inspect the Metrics editor before and after editing a field to confirm rename controls appear only when the draft is clean.
- Metrics tab duplicate rename affordance cleanup:
  - Removed the header rename icon entirely because the metric id row already has a contextual `Rename` action beside the id being renamed.
  - Kept the inline `Rename` button visible only for clean drafts, preserving the existing clean-draft rename workflow.
  - Touched only markup structure and a local indentation fix; app JSON, metric schema, rename behavior, dashboard binding updates, and runtime behavior remain unchanged.
  - Verification:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-verify\` passed with 0 warnings.
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false -p:UseAppHost=false -p:OutputPath=D:\Projects\FluxMq\artifacts\ui-tests-verify\` passed with 413 tests.
    - `git diff --check -- src\FluxMq.UI\Components\Workspace\MetricDesigner.razor src\FluxMq.UI\Components\Workspace\MetricDesigner.razor.css` passed with the existing LF-to-CRLF warning for the tracked Metrics file.
    - Whitespace checks over the touched Razor/CSS files passed.
  - Next step: manually inspect the clean Metrics header and metric-id field to confirm rename is discoverable in the id row without duplicating header controls.

## 2026-06-20 - Workspace UI chrome de-clutter continuation

- Continued the flat app-workspace polish after the Metrics tab pass:
  - The live inspector publish area now uses quieter publish controls and reduces always-visible action chrome while keeping the existing publish workflow intact.
  - The component catalog metadata area now reads as compact metadata instead of stacked badges, preserving the same catalog data and selection behavior.
  - Test scenario step rows now use `test-step-meta` naming instead of badge naming, and run-history status, phase counts, step type/status, and step result metadata were flattened from pill chips into compact text metadata.
  - Test scenario card markers use square-ended flat strips, and old badge/radius styling is guarded against in UI tests.
  - App JSON, runtime behavior, scenario execution, metric schema, and package-backed component boundaries were unchanged.
- Verification for the latest test-scenario metadata slice:
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false --filter "FullyQualifiedName~TestScenarioDesigner_UsesFlatCompactScenarioChrome"` passed.
  - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false` passed with 462 tests.
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -m:1` passed.
  - Remote Windows validation passed before merge.
- Next step: continue scanning the remaining high-noise workspace surfaces and apply compact flat app polish only where it improves workflow clarity.

## 2026-06-20 - Pipeline canvas metric chrome de-clutter

- Continued the workspace UI chrome de-clutter pass on the pipeline designer:
  - Replaced the bordered `flow-canvas-chip` metric tokens in the pipeline canvas header with flat `flow-canvas-stat` rows separated by quiet dividers.
  - Kept node/link/resource/diagnostic counts, runtime state, diagram behavior, link editing, and workflow JSON behavior unchanged.
  - Added a guard to the existing canvas-chrome UI test so the old chip class does not return to the pipeline header.
- Verification:
  - Initial focused test run without an isolated output path was blocked because the running desktop app had `src\FluxMq.UI\bin\Debug\net10.0-windows10.0.19041.0\win-x64\FluxMq.UI.exe` locked.
  - The focused canvas-chrome guard passed through the isolated UI test output path.
  - The full UI test project passed with 462 tests through the isolated UI test output path.
  - The isolated UI build passed with 0 warnings.
  - The whitespace check passed with the existing LF-to-CRLF warnings for the touched files.
- Next step: manually inspect the pipeline canvas header at desktop and narrow widths, with and without diagnostics/resource nodes, to confirm the flat metric row reads clearly without crowding the runtime state.

## 2026-06-20 - Recordings moved into live workspace tools

- Updated the workspace plan and shell placement for recorded sessions:
  - Moved recorded-session browsing and capture controls into the live tools panel as a dedicated `Recordings` tab beside publish, topics, and payload inspection.
  - Removed the session-only left rail, left collapsible panel, and stale left-panel shell styling.
  - Widened the live tools panel slightly now that the left rail is gone, and kept the new recordings tab in the existing flat tab/header language.
  - Kept recording, stored-session loading, publish, topic inspection, payload inspection, app JSON, and runtime behavior unchanged.
- Verification:
  - Focused guards for live tools, shell placement, and session rows passed.
  - The full UI test project passed with 463 tests.
  - The isolated UI build passed with 0 warnings.
  - The whitespace check passed with the existing LF-to-CRLF warnings for the touched files.
- Next step: manually inspect the live tools panel at desktop and narrow widths, especially the new Recordings tab while idle, recording, and viewing a stored session.

## 2026-06-20 - Pipeline canvas header no longer overlaps nodes

- Fixed the pipeline diagram info bar placement:
  - Moved the canvas header into normal layout flow above the diagram canvas instead of keeping it as an absolute overlay.
  - Let the diagram canvas flex below the header, so auto-zoomed and manually positioned nodes are not hidden behind the info bar.
  - Preserved the same header content, compact metrics, zoom command, diagram behavior, link editing, node positions, and workflow JSON behavior.
  - Added a guard to the existing canvas-chrome UI test so the header must render before the canvas and keep non-overlay CSS.
- Verification:
  - The focused canvas-chrome guard passed.
  - The full UI test project passed with 463 tests.
  - The first isolated UI build attempt hit a transient intermediate-file lock from another compiler process; rerunning the build by itself passed with 0 warnings.
- Next step: manually inspect the pipeline diagram at desktop and narrow widths to confirm the header stays above the canvas while diagnostics and selected-link controls remain usable.

## 2026-06-20 - Pipeline diagnostics route through Logs

- Simplified pipeline diagnostic review from the diagram header:
  - Removed the expanded validation/error overlay from the pipeline canvas so it no longer covers nodes or links.
  - Made the compact error/warning count in the canvas header an accessible action that opens the workspace `Logs` tab.
  - Added one-shot initial log query support so that diagnostic navigation opens Logs with the matching error or warning level selected, and with the active pipeline name as search text when every current diagnostic belongs to that pipeline.
  - Kept node diagnostic highlighting, runtime validation logging, diagram behavior, selected-link editing, app JSON, and workflow persistence unchanged.
  - Added UI guards so the old canvas overlay does not return and the logs-routing path stays wired.
- Verification:
  - Focused guards for the canvas diagnostic action, logs initial query, and workspace routing passed.
  - The full UI test project passed with 464 tests.
  - The isolated UI build passed with 0 warnings.
- Next step: manually inspect a pipeline with validation errors and confirm clicking the header error count opens Logs with the expected filter and no canvas overlap.

## 2026-06-20 - Logs problem banner removed

- Removed the redundant `Action needed` problem banner from the Logs tab:
  - Kept the header problem count, level filters, search, reset, clear, and per-row severity indicators as the primary log inspection workflow.
  - Deleted the banner-specific markup, helper properties, responsive CSS, and the `Show problems` action.
  - Added a guard so the old banner wording and CSS do not return.
- Verification:
  - The focused Logs chrome guard passed.
  - The full UI test project passed with 464 tests.
  - The isolated UI build passed with 0 warnings.
- Next step: manually inspect Logs after routing from the pipeline error count and confirm the page opens directly into the filtered rows without the extra banner.

## 2026-06-20 - Pipeline header fault/error duplication removed

- Reduced duplicate red status in the pipeline canvas header:
  - Suppressed the separate `Faulted` runtime pill when current errors are already shown in the actionable `Errors` count.
  - Kept normal runtime state display for idle, running, valid, stopped, and faulted-without-current-errors cases.
  - Kept the `Errors` count as the single red header action that opens Logs with the error filter.
  - Added a guard for the faulted/error de-duplication rule.
- Verification:
  - The focused canvas diagnostic guard passed after one timed-out compile attempt was rerun with a longer timeout.
  - The full UI test project passed with 464 tests.
  - The isolated UI build passed with 0 warnings.
- Next step: manually inspect a faulted pipeline with validation errors and confirm the header shows only the `Errors` action, not both `Faulted` and `Errors`.

## 2026-06-20 - Pipeline error navigation applies pipeline context

- Tightened Logs filtering when opening errors from the pipeline header:
  - The header error action now applies the active pipeline name as the Logs search when current problem diagnostics are either scoped to that pipeline or unscoped.
  - Unscoped validation/build/runtime problem diagnostics recorded while a pipeline is active now carry that active pipeline as log artifact metadata, so the automatic search does not produce an empty list.
  - Explicit workflow-scoped diagnostics, row severity, Logs level filtering, reset/clear behavior, and diagram diagnostics remain unchanged.
  - Added a service regression test for unscoped validation problems and a UI guard for the active-pipeline search rule.
- Verification:
  - Focused service and designer guards passed.
  - The full UI test project passed with 465 tests.
  - The isolated UI build passed with 0 warnings.
- Next step: revalidate the same app, click the pipeline `Errors` count, and confirm Logs opens with `Error` selected and the active pipeline name in search while still showing the matching rows.

## 2026-06-20 - Right panel is now app-level MQTT Publisher

- Redefined the right workspace dock around one responsibility:
  - Removed the tabbed live inspector surface from the right panel, including inspect, topics, recordings, and last-payload sections.
  - Rebuilt the panel as an app-level `MQTT Publisher` with active-app label, app-scoped MQTT client selector, topic, payload, QoS, retain, selected-client state, connect action, and MQTT status.
  - Made the publisher available whenever an app is active instead of only while pipelines or dashboards are selected.
  - Scoped publisher clients to the active app's `mqtt.connection` resources and auto-registers missing live clients from the active app definition.
  - Let manual publish connect the selected client first when needed, then record successful publishes through the existing app event/log projection.
  - Updated shell toggle text from live tools to MQTT publisher.
- Verification:
  - Focused right-panel, shell, and policy guards passed.
  - The full UI test project passed with 465 tests.
  - The isolated UI build passed with 0 warnings.
- Next step: manually inspect the desktop shell across Pipeline, Dashboard, Tests, Topics, Logs, and no-app states to confirm the publisher dock feels global, collapses correctly, and does not crowd the active workspace.

## 2026-06-20 - MQTT Publisher form fields stack vertically

- Adjusted the right-dock MQTT Publisher form layout:
  - Changed the publish form grid from two columns to one column so MQTT client and Topic no longer share a cramped horizontal row.
  - Kept payload, QoS, retain, publish, selected-client state, and status behavior unchanged.
  - Updated the UI guard to prevent the two-column publisher form from returning.
- Verification:
  - The first focused guard run timed out during build/test startup.
  - Rerunning the focused publisher panel guard passed.
- Next step: visually inspect the right dock at the current desktop width and a narrower window to confirm the stacked fields read correctly.

## 2026-06-20 - MQTT Publisher selected-client duplicate removed

- Removed the redundant selected-client summary card from the right-dock MQTT Publisher:
  - Kept client selection in the `MQTT client` picker and connection state in the header badge.
  - Kept publish auto-connect behavior for the selected client, so the separate Connect card/action is no longer needed.
  - Deleted the unused selected-client card markup, connect method, and scoped CSS.
  - Updated the publisher UI guard to prevent the duplicate section from returning.
- Verification:
  - Focused publisher panel guard passed.
- Next step: visually inspect the right dock after selecting connected and disconnected clients to confirm the picker plus header badge provide enough state without the extra card.

## 2026-06-20 - Logs toolbar control heights aligned

- Normalized the Logs filter toolbar control heights:
  - Added a shared `--workspace-log-control-height` for the Scope segment, Level segment, forced-scope chip, and search field.
  - Applied the same height through the MudTextField wrapper, input container, input root, adornment, and outlined border so the search box no longer renders taller than the filter segments.
  - Follow-up changed the shared toolbar control height from 28px to 40px and increased the segmented-control buttons to 30px, matching the actual MudTextField visual height instead of trying to compress the search field.
  - Updated the Logs chrome UI guard to prevent the old 30px search-field height from returning.
- Verification:
  - Initial focused test command used a stale filter name and built successfully with no matching test.
  - Rerunning `WorkspaceLogPanel_UsesFlatCompactWorkspaceChrome` passed after both the initial normalization and the follow-up height increase.
- Next step: visually inspect Logs at desktop and narrow widths to confirm Scope, Level, and Search now share the search field height without the segment controls looking undersized.

## 2026-06-20 - Topics tab prioritizes latest topic state

- Reworked the first-level Topics tab layout around topic tree plus latest message state:
  - Kept the topic tree as the left primary navigation surface.
  - Replaced the large embedded `PayloadInspectorPanel` area with a lighter `Latest topic message` state panel at the top of the detail pane.
  - The latest state panel shows selected topic context, payload preview, payload type/bytes, received time, QoS, retain flag, and full topic metadata.
  - Moved the message grid into a lower `History` section and sorts it newest-first.
  - Selecting a topic in the tree still selects the latest matching message, but the top state is now calculated from the latest filtered message rather than from arbitrary history-row selection.
  - Updated the Topics chrome UI guard so the old payload-inspector-first layout does not return.
- Verification:
  - Focused `TopicExplorerPanel_UsesFlatCompactWorkspaceChrome` guard passed.
  - Full UI test project passed with 465 tests.
  - Isolated UI build passed with 0 warnings.
- Next step: visually inspect the Topics tab with live traffic and stored sessions to confirm the top state panel is useful without making history too short.

## 2026-06-20 - Topics history pager stays at bottom

- Fixed the Topics history table footer placement:
  - Made the history table wrapper a vertical flex container.
  - Applied flex sizing through MudBlazor table internals from the parent `topic-message-table` wrapper.
  - Let the table body/container take the available space and scroll, while the MudTable pagination footer stays pinned at the bottom with `margin-top: auto`.
  - Updated the Topics chrome UI guard to preserve the pager-bottom layout.
- Verification:
  - Focused `TopicExplorerPanel_UsesFlatCompactWorkspaceChrome` guard passed.
- Next step: visually inspect a topic with only a few history messages and confirm the pagination footer stays at the bottom of the history area.

## 2026-06-20 - Topics tab monitors and groups brokers

- Reworked the Topics tab broker handling:
  - Widened the topic explorer column so broker groups and deeper topic paths have more room.
  - The Topics tab now starts active-app MQTT connection monitors through the live workspace service without requiring the app runtime to be running.
  - The workspace monitor subscription is now the app-wide `#` filter.
  - Live messages are stamped with the managed broker/resource name before projection and recording, and stored messages preserve that optional broker label.
  - The topic navigator groups messages by broker, shows each broker state/count, and the latest-state/history detail panes now include broker context.
  - Updated UI, live-service, and storage guards for broker grouping, broker stamping, and broker-label persistence.
- Verification:
  - Focused Topics/live-service guards passed.
  - Focused storage round-trip guard passed.
  - Live MQTT workspace service tests passed with 11 tests.
  - Storage-focused component tests passed with 27 tests.
  - Full UI test project passed with 466 tests.
- Next step: visually inspect the Topics tab with two app MQTT connections and confirm each broker appears as its own group, receives live `#` traffic without running the app, and selecting a broker/topic updates latest state plus history correctly.

## 2026-06-20 - Topics uses separate app broker monitor clients

- Corrected the Topics direction to keep the current app `Topics` tab as the MQTT Explorer-like surface:
  - Removed the no-app `Topic explorer` entry that had been added during the first interpretation pass.
  - Kept the panel heading as `Topics` and clarified the live copy around app broker monitoring.
  - Changed monitor startup so each app MQTT broker gets an internal Topics monitor resource name and a separate MQTT client subscribed to `#`.
  - Kept visible broker names clean by stripping the internal Topics monitor prefix before stamping captured messages or showing broker rows.
  - Hid internal Topics monitor clients from the general Connections panel so they do not appear as user-managed app connections.
  - Recorded the broader product direction in `memory/10-development-plan.md`: the existing app `Topics` tab should behave like MQTT Explorer, with richer topic detail, payload/history controls, publish support, filtering, and stats inside that tab.
  - Updated UI and service guard tests for app-owned Topics behavior, internal monitor-client separation, visible broker names, and no standalone launcher.
  - Added `FLUXMQ_REPOSITORY_ROOT` support to the UI test repository-root helper so tests can run from isolated artifact output while the desktop app locks the normal output folder.
- Verification:
  - The first normal focused test/build attempt was blocked by the running desktop app locking `FluxMq.UI.dll`.
  - A relative isolated-output attempt produced generated `artifacts/verify/topic-explorer-*` folders under source/test project directories; those 22 generated folders were removed.
  - Isolated UI build passed with 0 warnings using `UseArtifactsOutput=true` and an absolute temp `ArtifactsPath`.
  - Focused guards passed: `TopicExplorerPanel_UsesFlatCompactWorkspaceChrome`, `WorkspacePage_UsesPipelineSpecificDesignerShell`, `ConnectionPanel_UsesFlatCompactConnectionRows`, and `TopicMonitorConnection_UsesSeparateClientAndVisibleBrokerName`.
  - `git diff --check` passed with line-ending warnings only.
- Next step: add richer MQTT Explorer topic-detail controls inside the current app `Topics` tab, starting with selected-topic payload/history controls and broker-scoped publish.

## 2026-06-20 - Topics no-message state shows monitor status

- Reworked the empty/no-message state in the app `Topics` tab:
  - Replaced the large generic `No messages yet` latest-state block with a `Waiting for broker traffic` state.
  - The top empty state now shows the active broker monitor rows, endpoint, `Sub #`, and connection state so a live-but-quiet broker still has useful context.
  - Selecting a broker changes the empty title to broker-specific waiting text while keeping the monitor status visible.
  - Replaced the repeated large no-message block in the history table with a compact `No history for the current selection` row.
  - Removed the stale table-empty CSS and updated the Topics chrome guard so the duplicated empty state does not return.
- Verification:
  - Isolated UI build passed with 0 warnings.
  - Focused `TopicExplorerPanel_UsesFlatCompactWorkspaceChrome` guard passed.
- Next step: visually inspect the `Topics` tab with quiet brokers and confirm the empty state reads as active monitoring rather than a blank page.

## 2026-06-20 - Topics monitor includes system topics

- Fixed why the app `Topics` tab did not show Mosquitto `$SYS` traffic while MQTT Explorer did:
  - Kept the normal app/default broker monitor subscription as `#`.
  - Added a dedicated `TopicExplorerMonitorSubscription` of `#,$SYS/#` for the internal `Topics` tab monitor clients.
  - Updated the `Topics` monitor startup to use the dedicated subscription, so it still creates separate clients per app broker but now also receives `$SYS/...` topics.
  - Updated the quiet-monitor status label to show `Sub # + $SYS/#`.
  - Updated UI/service guards so the Topics monitor subscribes to both filters and keeps visible broker names clean.
- Verification:
  - Isolated UI build passed with 0 warnings.
  - Focused guards passed: `TopicExplorerPanel_UsesFlatCompactWorkspaceChrome` and `TopicMonitorConnection_UsesSeparateClientAndVisibleBrokerName`.
- Next step: visually inspect the `Topics` tab against Mosquitto and confirm `$SYS` topics appear without publishing app traffic manually.

## 2026-06-20 - Topics history uses virtual scrolling

- Replaced the Topics history pager with a virtualized endless-scroll table:
  - Enabled MudTable fixed-header virtualization with a full-height scroll container.
  - Removed the pager and row-page sizing from the history table.
  - Forced the history table/container/table stack to fill the full detail width with fixed column sizing.
  - Updated the Topics chrome guard so pagination and narrow content-width table layout do not return.
- Verification:
  - Focused `TopicExplorerPanel_UsesFlatCompactWorkspaceChrome` guard passed using isolated output because the running desktop app locked the normal UI output files.
  - `git diff --check` passed with line-ending warnings only.
- Next step: visually inspect live Topics history with enough broker traffic to confirm row virtualization scrolls smoothly and the grid spans the full detail panel.

## 2026-06-20 - Topics history row details and column alignment

- Tightened the virtualized Topics history grid:
  - Added a MudTable `ColGroup` so the virtualized header and rows share the same Time/Broker/Topic/QoS/Bytes column widths.
  - Removed width control from header/body `nth-child` CSS and kept only numeric alignment there.
  - Added history-row selection and a right-side `Message details` pane with broker, topic, received time, QoS, retain flag, payload type/bytes, and payload preview.
  - Kept row selection synchronized with topic/broker selection and with the existing live selected-message projection.
  - Follow-up fixed odd-count detail metadata separators by removing the `nth-last-child(-n + 2)` rule and spanning the final odd metadata field across both columns.
  - Follow-up fixed the remaining header/row mismatch by scoping deep MudTable styles from the local wrapper, keeping `.mud-table-root` as a real table instead of flex, and applying matching fixed widths to header and row cells.
  - Updated the Topics chrome guard for aligned columns, selected-row detail state, and no pager regression.
- Verification:
  - Focused `TopicExplorerPanel_UsesFlatCompactWorkspaceChrome` guard passed using isolated output.
  - `git diff --check` passed with line-ending warnings only.
- Next step: visually inspect a live `$SYS` topic and click several history rows to confirm header/row alignment and selected-message detail updates are correct.

## 2026-06-20 - App header removes duplicate active artifact chip

- Removed the redundant active artifact/page label from the app identity area:
  - The app pill now shows only the active app name.
  - Removed the trailing active artifact meta text from the app structure toolbar.
  - Deleted the unused `CurrentArtifactLabel`/`BuildActiveMeta` helpers and stale chip/meta CSS.
  - Updated the app structure chrome guard so the duplicated artifact label does not return.
- Verification:
  - Focused `AppStructureMenu_UsesCompactInlineArtifactActions` guard passed using isolated output.
- Next step: visually inspect the top workspace bar and confirm `app1` no longer repeats `Topics` beside the active Topics tab.

## 2026-06-20 - Designer shell removes broad focus border

- Removed the workspace artifact/designer region `focus-within` inset border that showed a green outline around the full diagram canvas.
- Kept the left tool panel focus cue and individual control focus states intact.
- Updated the workspace shell guard so the full-content focus ring does not return.
- Verification:
  - Focused `WorkspacePage_UsesPipelineSpecificDesignerShell` guard passed using isolated output.
- Next step: visually inspect the pipeline canvas after focusing a diagram control and confirm the full green frame is gone.

## 2026-06-20 - App structure top bar flattens app identity and menus

- Flattened the app structure top bar:
  - Removed the border/background from the active app identity label while keeping the no-app empty state pill.
  - Toned down app-structure MudMenu popovers into compact anchored dropdowns with lighter shadow, smaller width, lower z-index, and internal scroll.
  - Kept existing menu actions and artifact selection behavior unchanged.
  - Updated the app structure chrome guard to prevent the old bordered app pill and modal-like menu styling from returning.
- Verification:
  - Focused `AppStructureMenu_UsesCompactInlineArtifactActions` guard passed using isolated output.
- Next step: visually inspect the top bar dropdowns and confirm they read as normal anchored menus instead of modal-like panels.

## 2026-06-20 - App structure menus are non-modal dropdowns

- Removed the remaining modal feel from the app structure menus:
  - Set each Brokers/Pipelines/Dashboards/Metrics/Tests MudMenu to `Modal="false"` so opening a menu does not create a dark page backdrop.
  - Removed the outer border from the app-structure dropdown panel and kept only a light shadow for separation.
  - Follow-up fixed the actual remaining dark backdrop by scoping the global MudBlazor overlay color to dialog scrims only and forcing popover/menu overlays transparent.
  - Follow-up removed the active/selected visual treatment from the closed app-structure toolbar buttons, so `Pipelines 2` and other structure menus stay visually neutral.
  - Follow-up normalized the top breadcrumb typography so `Workspace`, the app name, and structure menu labels share the same font size, weight, and neutral text color.
  - Follow-up flattened the `No app open` breadcrumb state by removing the old empty-state border/background and matching the app identity typography.
  - Updated the app-structure chrome guard to prevent the modal menu default and bordered dropdown panel from returning.
- Verification:
  - Focused `AppStructureMenu_UsesCompactInlineArtifactActions` guard passed using isolated output.
- Next step: visually inspect the top bar menus and confirm the workspace canvas no longer dims when a menu is open.

## 2026-06-20 - Topics latest and selected details share metadata layout

- Aligned the `Topics` tab latest-message and selected-history detail surfaces:
  - The latest-message body now uses the same right-side detail column width as the lower history/details split.
  - The latest metadata no longer renders as separate bordered cards; it uses the same two-column separator-grid style as the lower `Message details` pane.
  - Payload label and payload preview spacing/typography were normalized between the top latest panel and lower selected-message details.
  - Follow-up changed the latest-message metadata rail to stretch the full top-panel height and use vertical label/value rows, giving long topic paths more horizontal room than the old two-column cell grid.
  - Follow-up applied the same full-width label/value row layout to the lower selected-message details metadata so selected history rows no longer truncate topic paths in half-width cells.
  - Updated the Topics chrome guard so the old narrow one-column latest metadata panel does not return.
- Verification:
  - Focused `TopicExplorerPanel_UsesFlatCompactWorkspaceChrome` guard passed using isolated output.
- Next step: visually inspect a selected `$SYS` message in `Topics` and confirm the latest-state metadata and lower message-details metadata have matching width and style.

## 2026-06-20 - Topics history retention is per broker/topic

- Fixed selected-topic history shrinking while live `$SYS/#` traffic is active:
  - Changed the live MQTT workspace projection from a small global recent-message buffer to per broker/topic retention.
  - `$SYS/broker/uptime` history is no longer evicted just because faster `$SYS` topics arrive between uptime publishes.
  - Kept retention bounded per broker/topic so high-volume topics still trim their own oldest rows.
  - Added projection guards for preserving a topic history while other topics exceed the limit and trimming only within the same broker/topic.
- Verification:
  - Focused `WorkspaceMessageProjectionTests` passed using isolated output.
  - Focused `TopicExplorerPanel_UsesFlatCompactWorkspaceChrome` guard passed using isolated output.
- Next step: visually inspect `$SYS/broker/uptime` in `Topics` for several publish intervals and confirm the selected history row count increases instead of shrinking from unrelated `$SYS` traffic.

## 2026-06-20 - No-app workspace removes shell chrome

- Cleaned the startup/closed-app workspace state:
  - The main shell now hides the top workspace command bar and bottom status bar when there is no active app.
  - The no-app canvas uses the full window height and keeps the centered `New app` / `Open file` actions as the only workflow controls.
  - App-scoped validate/run/stop, app state, MQTT state, message count, file path, and publisher-toggle chrome return automatically once an app is active.
  - Added a layout guard for the no-app shell class and full-height grid.
- Verification:
  - Focused `MainLayout_RemovesSessionOnlyLeftRail` guard passed using isolated output.
- Next step: visually inspect initial launch and after closing the active app to confirm only the centered no-app action state remains.

## 2026-06-20 - App JSON uses Monaco viewer

- Replaced the flat raw-text App JSON body with the existing Monaco editor integration:
  - The App JSON tab now renders a read-only Monaco JSON viewer with the FluxMQ Monaco theme and automatic layout.
  - The existing toolbar metadata, unsaved status, copy action, empty state, and generated full-definition JSON source stay unchanged.
  - Theme changes reconfigure Monaco and project definition changes sync the viewer content.
  - Follow-up fixed the blank viewer by applying Monaco sizing through a `::deep` selector under the local editor shell, so CSS isolation reaches the child editor DOM.
  - Follow-up moved App JSON into the normal workspace artifact tab strip as an `App JSON` tab, so clicking a regular artifact tab returns to visual/graphic workspace mode and clicking `App JSON` again closes the code view.
  - Follow-up kept the app identity area as plain app identity, not a hidden JSON/visual toggle.
  - Follow-up catches non-critical Monaco/JS interop failures during setup and sync so editor failures do not trap the rest of the workspace.
  - Updated the App JSON and workspace guards so the old `<pre>` viewer and old right-side `</>` toggle do not return.
- Verification:
  - Isolated UI test build passed with 0 warnings.
  - Focused `AppJsonPanel_UsesFlatCompactCodeViewerChrome` and `WorkspacePage_RoutesPipelineDiagnosticsToFilteredLogs` guards passed using isolated output.
- Next step: visually inspect the App JSON tab, click `pip1` or another normal artifact tab, and confirm the visual workspace returns with tab switching still working.

## 2026-06-29 - Pipeline node UI design-system round

- Current local branch: `work/pipeline-node-ui-system`.
- Goal narrowed from an open-ended UI-polish effort into an achievable branch:
  - Establish one coherent Pipeline node UI direction.
  - Prioritize complex editors first because broken editor sizing and empty code surfaces block real use.
  - Keep runtime behavior, saved JSON schema, node ids, ports, contracts, and app resources unchanged.
- Accepted and committed local slices:
  - `508ce60 Restore mapper workbench columns`
  - `dcf418f Fix mapper editor height`
  - `081fb27 Fix code editor sizing`
  - `884f386 Polish state reducer editor`
  - `9a3958e Polish assertion editor`
  - `Polish message filter editor`
  - `Polish condition router editor`
  - `Polish routing switch editor`
  - `Polish routing matching editors`
  - `Polish routing utility editors`
  - `Polish source and trigger editors`
  - `Polish actor sink editors`
- Dynamic Mapper result:
  - Rebuilt the edit dialog around three stable columns: sample input, mapping expression, and result.
  - Made the expression editor the dominant full-height workspace.
  - Removed extra side metadata panels that were not needed for the mapping workflow.
  - Fixed editor measurement/layout so code content no longer collapses into a thin strip.
- JSON Schema Validator result:
  - Fixed the inline schema editing path so it uses a proper code-editor surface instead of leaving a large blank area.
  - Kept schema source/id behavior and saved configuration shape unchanged.
- State Reducer result:
  - Replaced the reducer multiline text area with a full code-editor surface.
  - Kept key expression, variables, max keys, input buffer, validation, and config persistence behavior unchanged.
  - Focused build and source-level UI test passed before the local commit.
- Flow Assertion result:
  - Replaced the pass-condition multiline text area with a measured full code-editor surface.
  - Kept assertion name, input type, input buffer, failure message, variables, validation, and saved config behavior unchanged.
  - Visual check confirmed the editor layout: expression workspace fills the left side, failure message and variables stay in the right sidecar, and the node header remains plain node name plus component subtitle.
  - Verification passed:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~FlowAssertionNodeWidget" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
- Message Filter result:
  - Replaced the optional condition multiline text area with the same stable full-height code-editor workspace.
  - Moved topic pattern editing into a compact right sidecar and kept variable references there.
  - Kept topic-pattern validation, blank-condition behavior, view-mode facts, pattern chips, expression preview, and saved configuration shape unchanged.
  - Verification passed:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~MessageFilterNodeWidget" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
  - Desktop visual note: a temporary `flow.filter` project opened correctly and confirmed the compact node face; this session could not drive the WebView settings button reliably enough to complete the edit-dialog manual path.
- Condition Router result:
  - Replaced the route-condition multiline text area with the same measured full-height code-editor workspace.
  - Moved input type selection and variable reference into a compact right sidecar.
  - Kept input-type normalization, type-change default expression reset, blank-condition validation, branch ports, and saved configuration shape unchanged.
  - Verification passed:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~ConditionRouterNodeWidget" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
- Routing Switch result:
  - Replaced the routing expression multiline text area with the same measured full-height code-editor workspace.
  - Moved input type, input buffer, emit-envelope, variable reference, and route rows into a compact right sidecar.
  - Kept expression validation, route validation, duplicate match-key validation, route parsing, ports, and saved configuration shape unchanged.
  - Verification passed:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~RoutingSwitchNodeWidget" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
- Routing Correlation and Join result:
  - Replaced correlation key/side expression fields and join left/right key expression fields with paired full-height code-editor workspaces.
  - Moved input types, side names, timeout, max pending, input buffer, and case sensitivity into compact right sidecars.
  - Kept required-expression validation, correlation side-name validation, normalization, ports, and saved configuration shape unchanged.
  - View mode now favors input/side facts, timeout/buffer facts, case/pending state, and expression previews without join output contract clutter.
  - Verification passed:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~RoutingCorrelationAndJoinNodeWidgets" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
  - Desktop manual check attempted with native desktop control. The app window was controllable, but the Windows file picker overlay was not targetable by the helper and direct `--open` launch stayed on the startup splash, so edit-dialog visual verification remains a gap for this slice.
- Routing Fork/Merge/Window result:
  - Reworked the remaining routing utility editors into compact main editor areas plus support sidecars.
  - Fork and Merge keep port row editing, add/remove controls, input type, input buffer, validation, ports, and saved configuration shape unchanged.
  - Window keeps max items, time window, input type, input buffer, emit-partial control, zero-boundary validation, ports, and saved configuration shape unchanged.
  - View mode remains compact operational facts with fork/merge port chips and window boundary chips.
  - Verification passed:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~RoutingFanNodeWidgets" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~RoutingWindowNodeWidget" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
  - Desktop manual check was not completed because native desktop automation was stopped with Escape during file-open setup; no manual visual acceptance is claimed for this slice.
- Source and Trigger result:
  - Removed view-mode output-field contract rows from `mqtt.connection-state-trigger`, `generated.source`, `replay.source`, `session.source`, and timer nodes, leaving compact operational facts and previews only.
  - Reworked `mqtt.trigger` so subscription editing remains the primary table workspace and broker/output-buffer controls sit in a compact sidecar.
  - Reworked `generated.source` so generated message rows remain the primary table workspace and output-buffer control sits in a compact sidecar.
  - Replay, stored session, connection-state, and timer editors remain dense flat configuration forms without code-editor workspaces.
  - Validation, saved configuration shape, node ids, ports, runtime source/trigger semantics, and normalization behavior are unchanged.
  - Verification passed:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~MqttTriggerNodeWidget|FullyQualifiedName~ConnectionStateTriggerNodeWidget|FullyQualifiedName~GeneratedSourceNodeWidget|FullyQualifiedName~ReplaySourceNodeWidget|FullyQualifiedName~StoredSessionSourceNodeWidget|FullyQualifiedName~TimerNodeWidget" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
- Actor Sink result:
  - Removed view-mode request-field contract rows from `mqtt.publisher`, `mqtt.recorder`, and `file.writer`, leaving compact broker/target and input-buffer facts only.
  - Kept `mqtt.publisher` broker selection, app-resource synchronization, live-connection synchronization, input-buffer validation, and saved configuration shape unchanged.
  - Kept `mqtt.recorder` and `file.writer` as dense single-control input-buffer editors with existing validation and saved configuration unchanged.
  - Runtime behavior, request contracts, node ids, ports, component contracts, and normalization behavior are unchanged.
  - Verification passed:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~MqttPublisherNodeWidget|FullyQualifiedName~MqttRecorderNodeWidget|FullyQualifiedName~FileWriterNodeWidget" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
- Metric Node result:
  - Removed the `metric.source` view-mode output-field contract row, leaving selected metric, latest value, start mode, output buffer, and parameter preview facts.
  - Kept `mqtt.metrics` on its compact runtime summary with rate window, readout layout, selected readouts, top topics, and last-topic state without adding contract or decorative status rows.
  - Both metric editors remain configuration-heavy flat editors without code-editor workspaces.
  - Runtime behavior, saved configuration shape, node ids, ports, metric resources, snapshots, component contracts, and normalization behavior are unchanged.
  - Verification passed:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~MqttMetricsNodeWidget|FullyQualifiedName~MetricSourceNodeWidget" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
- Generic Node result:
  - Removed view-mode request/result/field dump rows from `http.request`, `payload.inspect`, and legacy `mqtt.payload-inspector`, leaving compact operational facts and option state.
  - Renamed fallback editor port-detail wording for `GenericFlowNodeWidget` and `DefaultNodeWidget` away from contract language while keeping the same summary, port counts, and port chips.
  - Kept `flow.logger` on the generic widget path; no dedicated widget was added.
  - All affected editors remain flat detail/configuration surfaces without code-editor workspaces.
  - Runtime behavior, saved configuration shape, node ids, ports, registry mappings, request/result shapes, component contracts, and normalization behavior are unchanged.
  - Verification passed:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~HttpRequestNodeWidget|FullyQualifiedName~PayloadInspectNodeWidget|FullyQualifiedName~PayloadInspectorNodeWidget|FullyQualifiedName~GenericFlowNodeWidget|FullyQualifiedName~DefaultNodeWidget" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
- Topics Publish Composer result:
  - Added a compact flat publish composer inside the app-owned `Topics` tab detail area.
  - Publish targets only active app MQTT broker resources; internal `topics:` monitor clients remain read-only monitor clients and are filtered out of the publish broker list.
  - Selected broker/topic changes prefill the publish broker and topic when there is a clean app-broker match, with fallback to the first app broker and an editable topic field.
  - Reused `LiveMqttWorkspaceService.PublishAsync`, `Live.ConnectAsync`, and `FlowWorkspaceService.RecordManualMqttPublish`; no runtime component behavior, saved app schema, explorer schema, storage format, node ids, ports, or component contracts changed.
  - Verification passed:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~TopicExplorerPanel|FullyQualifiedName~LiveMqttWorkspaceService|FullyQualifiedName~TopicExplorerMonitorResolver" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
- Topics Payload Detail Controls result:
  - Added local `Formatted`, `Raw`, `Hex`, and `Meta` payload view controls for both the latest message and selected history message in the existing app-owned `Topics` tab.
  - Added copy actions for the currently visible latest/selected payload view through the app's clipboard and snackbar feedback pattern.
  - Preserved the broker tree, latest metadata rail, publish composer, virtualized history grid, row selection, and selected-message detail layout.
  - No MQTT monitor behavior, publish behavior, saved app schema, explorer schema, storage format, runtime components, node ids, ports, or component contracts changed.
  - Verification passed:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~TopicExplorerPanel" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
- Topics History Filter and Export result:
  - Added a compact lower-history toolbar with displayed-field text filtering, QoS filtering, retain-state filtering, reset, and JSON export for visible rows.
  - Kept topic-tree search and broker/topic selection separate from local history filters; latest message remains broker/topic scoped while the selected detail tracks the visible history grid.
  - Export reuses the app's `SaveAsDialog` pattern and writes visible-row JSON with broker, topic, timestamp, QoS, retain, payload byte count, payload type, base64 payload, text payload when text, and hex dump.
  - No MQTT monitor behavior, publish behavior, saved app schema, explorer schema, storage format, runtime components, node ids, ports, or component contracts changed.
  - Verification passed:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~TopicExplorerPanel" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
- Topics Payload Diff result:
  - Added a selected-history-only `Diff` payload view in the existing app-owned `Topics` tab.
  - The diff compares the selected history message against the current latest message for the active broker/topic scope, with compact latest-row, unchanged-payload, bounded text diff, and binary metadata/first-differing-byte states.
  - The selected payload copy action now copies the visible diff text when `Diff` is active, while latest payload controls remain limited to formatted, raw, hex, and metadata views.
  - Preserved the broker tree, latest panel, publish composer, history filters/export, virtualized grid, row selection, selected-message detail, monitor behavior, publish behavior, saved app schema, explorer schema, storage format, runtime components, node ids, ports, and component contracts.
  - Verification passed:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~TopicExplorerPanel" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
- Topics Publish Presets result:
  - Added compact publish-assist controls inside the existing app-owned `Topics` publish composer.
  - `Use latest` and `Use selected` load topic, text payload, QoS, retain, and a clean matching app broker into the composer when the source payload is text; binary/non-text payloads are not coerced into publish text.
  - Successful publishes from this composer are kept in a bounded component-local recent list with load and clear actions, using broker resource/label, topic, text payload, QoS, retain, timestamp, payload type, and byte count.
  - Preserved broker targeting, internal monitor-client filtering, `Live.ConnectAsync`, `Live.PublishAsync`, `FlowWorkspaceService.RecordManualMqttPublish`, selected broker/topic prefill, saved app schema, explorer schema, storage format, runtime components, node ids, ports, and component contracts.
  - Verification passed:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~TopicExplorerPanel" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
- Topics Stats Panel result:
  - Added a compact stats strip for the active broker/topic scope inside the existing app-owned `Topics` tab.
  - Stats are derived only from scoped `HistoryMessages`, not local lower-history filters, and include message count, unique topic count, retained count, total payload bytes, average payload bytes, QoS 0/1/2 counts, and latest received time.
  - The panel uses private `TopicExplorerPanel` helpers and a private helper record; no services, metric resources, saved fields, runtime wiring, or schema changes were added.
  - Preserved monitor behavior, publish behavior, saved app schema, explorer schema, storage format, runtime components, node ids, ports, component contracts, publish semantics, and lower-history filter behavior.
  - Verification passed:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~TopicExplorerPanel" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
- Workspace Logs Detail and Export result:
  - Added compact copy and JSON export actions for the currently visible workspace log rows in the first-level `Logs` tab.
  - Copy/export uses the post-filter `FilteredLogs` view, so scope, level, search, fixed scope, and max-entry limits are preserved.
  - Export reuses the app's `SaveAsDialog` pattern and writes indented JSON with timestamp, severity, scope, artifact, workflow, source, code, node, port, message, and context fields.
  - Helpers stay private to `WorkspaceLogPanel`; no runtime logging behavior, log collection semantics, saved app schema, storage model, component contracts, node ids, ports, or workspace routing changed.
  - Verification passed:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~WorkspaceLogPanel" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
- Workspace Setup Dialogs result:
  - Removed hidden/explicit readiness status chrome from the Add Connection and Start Recording dialogs.
  - Add Connection still uses the same broker/client/keep-alive/TLS/certificate/clean-start controls, certificate picker behavior, validation, and result projection.
  - Start Recording still uses the same project autocomplete, session name defaulting, Enter handling, project summary, and blank project/session normalization.
  - Runtime behavior, MQTT connection behavior, recording behavior, saved app schema, storage model, monitor semantics, workspace routing, services, node ids, ports, and component contracts are unchanged.
  - Verification passed:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~AddConnectionDialog|FullyQualifiedName~StartRecordingDialog" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
- Remaining Setup Dialogs result:
  - Removed hidden/explicit readiness status chrome from New App, New Pipeline, Save As, Metric Create, Metric Rename, Metric Duplicate, and Metric Type Change dialogs.
  - Kept useful non-readiness state intact: metric type/search counts, validation/error rows, destructive metric confirm/delete tone status, and runtime/dashboard/node/scenario status surfaces.
  - Preserved app creation, pipeline creation, save path, metric create/rename/duplicate/type-change validation, metric id generation, default parameter previews, dashboard binding behavior, and type reset warnings.
  - Runtime behavior, saved app schema, storage model, metric model, dashboard bindings, services, node ids, ports, and component contracts are unchanged.
  - Verification passed:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~NewAppDialog|FullyQualifiedName~NewPipelineDialog|FullyQualifiedName~SaveAsDialog|FullyQualifiedName~MetricCreateDialog|FullyQualifiedName~MetricActionDialogs" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
- Current design rules learned from visual review:
  - View mode must show useful operational facts, not decorative contract dumps.
  - Edit mode must use one clean flat form/workspace surface, not nested panels.
  - Code-heavy nodes need stable full-height editor surfaces and predictable columns.
  - Save readiness must come from validation state, not status labels like `Ready to save`.
  - Dialog footers should sit in a consistent padded full-width footer panel.
  - Header should be node name plus component subtitle, without decorative header icon/category chip noise.
- State Reducer Summary Copy result:
  - Replaced the remaining State Reducer view-summary `Contract` label and contract-named token group with neutral data-type wording.
  - Kept the same compact operational facts, expression preview, code editor workspace, sidecar controls, validation, saved configuration shape, node id, ports, and runtime behavior.
  - Updated the focused guard to require `State reducer data types` and reject the old State Reducer contract summary hooks.
  - Verification passed:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~StateReducerNodeWidget" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
- Validation Node Summary Hook result:
  - Renamed the Flow Assertion compact output summary hooks from contract wording to neutral output-field wording while keeping the same visible labels, tokens, expression preview, editor layout, validation, node id, ports, saved configuration, and runtime behavior.
  - Renamed the JSON Schema Validator compact input/output summary hooks from contract wording to neutral field wording while preserving schema source/id controls, inline/file editor behavior, validation, node id, ports, saved configuration, and runtime behavior.
  - Updated the focused guards to require the neutral output/field hooks and reject the old validation-node contract summary hooks.
  - Verification passed:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~FlowAssertionNodeWidget|FullyQualifiedName~JsonSchemaValidatorNodeWidget" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
- Dynamic Mapper Summary Copy result:
  - Renamed Dynamic Mapper compact summary hooks from contract wording to neutral field wording while keeping input-variable and output-field facts unchanged.
  - Changed the editor label from `Result contract` to `Result mode`; the existing `OutputContract` model/API names, saved `outputContract` value, schema controls, preview workbench, validation, node id, ports, and runtime behavior are unchanged.
  - Updated the focused guard to require the neutral mapper field hooks and reject the old mapper contract hooks.
  - Verification passed:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~DynamicMapperNodeWidget" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
- Routing Summary Hook result:
  - Renamed compact summary label hooks from contract wording to neutral summary-label wording for Routing Switch, Correlation, Join, Fork, and Merge widgets.
  - Kept all visible routing facts, route/port chips, expression previews, editor layouts, validation, saved configuration, node ids, ports, and runtime behavior unchanged.
  - Updated the focused routing guards to require the neutral summary hooks and reject the old routing contract-label hooks.
  - Verification passed:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~RoutingSwitchNodeWidget|FullyQualifiedName~RoutingFanNodeWidgets|FullyQualifiedName~RoutingCorrelationAndJoinNodeWidgets" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
- Next implementation order:
  1. Continue the next small designer-polish backlog item from remaining high-use workspace noise, starting with remaining compact node-summary naming noise where a focused guard already exists.

- Flow Designer Canvas Chrome verification result:
  - Confirmed the existing Flow Designer canvas header uses `Pipeline loaded` for the loaded-pipeline subtitle while preserving `Unsaved changes` and `No active pipeline`.
  - Confirmed the focused guard requires the neutral loaded subtitle path and rejects the generic `Ready` canvas chrome.
  - Workflow JSON, saved app schema, node ids, ports, diagram behavior, runtime behavior, services, logs, diagnostics, metrics, navigator, and link-condition editing are unchanged.
  - Verification passed:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~FlowDesigner_UsesFlatCompactCanvasChrome" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
  - Next target: continue scanning high-use workspace chrome for remaining custom-control accessibility gaps or stale selectors without broadening runtime or schema scope.

- Metric Designer parameter toggle accessibility result:
  - Added explicit horizontal orientation semantics to the custom Default/On/Off metric-parameter radio group.
  - Preserved existing radio option roles, `aria-checked` selection state, validation, metric resource data, dashboard bindings, saved app schema, runtime behavior, services, ids, ports, and contracts.
  - Updated the focused Metric Designer guard to require the radio-group orientation and existing option semantics.
  - Verification passed:
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~MetricDesigner" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
  - Next target: continue scanning high-use workspace chrome for remaining custom-control accessibility gaps or stale selectors without broadening runtime or schema scope.

- App Structure active item accessibility result:
  - Added `aria-current` to active pipeline, dashboard, metric-designer, and test menu items in the compact app-structure menu.
  - Reused the existing active-artifact logic through a small `ArtifactMenuItemCurrent` helper and shared `IsArtifactActive` helper.
  - Preserved active classes, MudMenuItem selection behavior, delete actions, broker controls, saved app schema, runtime behavior, services, ids, ports, and contracts.
  - Verification passed:
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~AppStructureMenu_UsesCompactInlineArtifactActions" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
  - Next target: continue scanning high-use workspace chrome for remaining custom-control accessibility gaps or stale selectors without broadening runtime or schema scope.

- Topic Tree placeholder accessibility result:
  - Added `aria-hidden="true"` to the static leaf chevron placeholder in `TopicTreeNode`.
  - Preserved row-click isolation for the placeholder, expandable branch chevron buttons, topic selection, keyboard activation, compact mode, message counts, saved app schema, runtime behavior, services, ids, ports, and contracts.
  - Updated the focused Topic Tree guard to require the hidden static placeholder.
  - Verification passed:
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~TopicTreeNode_UsesCompactBranchLineChrome" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
  - Next target: continue scanning high-use workspace chrome for remaining custom-control accessibility gaps or stale selectors without broadening runtime or schema scope.

- Topic Tree decorative marker accessibility result:
  - Added `aria-hidden="true"` to the recursive topic-tree branch guide and filtered-result topic icon wrapper.
  - Preserved row labels, treeitem semantics, selection, keyboard activation, branch expansion, compact mode, message counts, saved app schema, runtime behavior, services, ids, ports, and contracts.
  - Updated the focused Topic Tree guard to require hidden decorative markers in recursive and filtered topic rows.
  - Verification passed:
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~TopicTreeNode_UsesCompactBranchLineChrome" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
  - Next target: continue scanning high-use workspace chrome for remaining custom-control accessibility gaps or stale selectors without broadening runtime or schema scope.
- Topic Explorer decorative icon accessibility result:
  - Added `aria-hidden="true"` to the decorative Topics title icon wrapper and broker row icon wrapper in `TopicExplorerPanel`.
  - Preserved broker selection button labels, broker monitor actions, topic tree selection, publish controls, payload views, history filtering/export, stats, saved app schema, monitor behavior, MQTT behavior, services, schemas, ids, ports, and contracts.
  - Updated the focused Topics guard to require the hidden decorative icon wrappers.
  - Verification passed:
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~TopicExplorerPanel" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
  - Next target: continue scanning high-use workspace chrome for remaining custom-control accessibility gaps or stale selectors without broadening runtime or schema scope.
- Live Publisher decorative icon accessibility result:
  - Added `aria-hidden="true"` to the decorative MQTT publisher title icon wrapper in `LiveInspectorPanel`.
  - Preserved the visible title, active app label, connection marker, publish form controls, retain toggle, diagnostics panel, manual publish recording, MQTT behavior, saved app schema, services, schemas, ids, ports, and contracts.
  - Updated the focused Live Inspector guard to require the hidden decorative publisher icon wrapper.
  - Verification passed:
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~LiveInspectorPanel" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
  - The first parallel build attempt hit the known transient XAML compiler file lock; the serial rerun passed.
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
  - Next target: continue scanning high-use workspace chrome for remaining custom-control accessibility gaps or stale selectors without broadening runtime or schema scope.
- App JSON decorative icon accessibility result:
  - Added `aria-hidden="true"` to the decorative App JSON title icon wrapper in `AppJsonPanel`.
  - Preserved the visible title, file label, JSON summary, unsaved indicator, copy action, Monaco viewer configuration/sync, app JSON generation, saved app schema, services, schemas, ids, ports, and contracts.
  - Updated the focused App JSON guard to require the hidden decorative title icon wrapper.
  - Verification passed:
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~AppJsonPanel_UsesFlatCompactCodeViewerChrome" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
  - Next target: continue scanning high-use workspace chrome for remaining custom-control accessibility gaps or stale selectors without broadening runtime or schema scope.
- Workspace tab decorative icon accessibility result:
  - Added `aria-hidden="true"` to the seven decorative project tab icons in `WorkspacePage`.
  - Preserved visible tab labels, button semantics, keyboard activation, active `aria-current` markers, delete/close actions, artifact routing, JSON view toggling, diagnostics-to-logs navigation, saved app schema, runtime behavior, services, ids, ports, and contracts.
  - Updated the focused WorkspacePage guard to require all project tab icons to use the hidden decorative treatment.
  - Verification passed:
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~WorkspacePage_RoutesPipelineDiagnosticsToFilteredLogs" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
  - Source-only tab-icon scan, neutral added-text scan, and `git diff --check` passed.
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
  - Next target: continue scanning high-use workspace chrome for remaining decorative icon/accessibility gaps or stale selectors without broadening runtime or schema scope.
- Metric dialog decorative icon accessibility result:
  - Added `aria-hidden="true"` to the title icon wrappers in MetricCreateDialog, MetricRenameDialog, MetricDuplicateDialog, MetricTypeChangeDialog, MetricConfirmDialog, and MetricDeleteDialog.
  - Preserved visible title/subtitle copy, metric type search/counts, validation rows, tone chips, destructive warnings, binding/reference details, button labels, dialog results, metric model behavior, dashboard binding behavior, saved app schema, runtime behavior, services, ids, ports, and contracts.
  - Updated the focused metric creation and metric action dialog guards to require hidden decorative title icons.
  - Verification passed:
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~MetricCreateDialog_UsesFlatCompactCreationChrome|FullyQualifiedName~MetricActionDialogs_UseFlatCompactModalChrome" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
  - Source-only title-icon scan, neutral added-text scan, and `git diff --check` passed.
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
  - Next target: continue scanning high-use workspace chrome for remaining decorative icon/accessibility gaps or stale selectors without broadening runtime or schema scope.
- App Tree decorative icon accessibility result:
  - Added `aria-hidden="true"` to decorative empty, app row, pipeline, metric, dashboard, empty-test, and section header icons in `AppTreePanel`.
  - Preserved visible labels, row/button semantics, active `aria-current` markers, section `aria-expanded` and `aria-controls`, connection state markers, test run summaries, action buttons, artifact routing, saved app schema, runtime behavior, services, ids, ports, and contracts.
  - Updated the focused App Tree guard to require the hidden decorative icon treatment for the updated icon surfaces.
  - Verification passed:
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~AppTreePanel_UsesCompactTestManagementRows" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
  - Source-only app-tree icon scan, neutral added-text scan, and `git diff --check` passed.
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
  - Next target: continue scanning high-use workspace chrome for remaining decorative icon/accessibility gaps or stale selectors without broadening runtime or schema scope.
- Dashboard widget decorative icon accessibility result:
  - Added `aria-hidden="true"` to dashboard widget header icon wrappers in the shared widget view plus line, area, bar, donut, latest-event, event-table, metric-value, topic-activity, and topic-tree widget render surfaces.
  - Preserved visible widget titles/subtitles, dashboard widget layout/styling, live/edit rendering, metric/event/topic data display, saved dashboard schema, runtime behavior, services, ids, ports, and contracts.
  - Added the focused `DashboardWidgets_HideDecorativeHeaderIcons` guard to scan all widget files with `dashboard-widget-icon` and reject unhidden decorative wrappers.
  - Verification passed:
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~DashboardWidgets_HideDecorativeHeaderIcons" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
  - Source-only dashboard widget icon scan, neutral added-text scan, and `git diff --check` passed.
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
  - Next target: continue scanning high-use workspace chrome for remaining decorative icon/accessibility gaps or stale selectors without broadening runtime or schema scope.
- Metric Designer decorative icon accessibility result:
  - Added hidden decorative semantics to the Metric Designer heading icon, create/filter/editor empty-state icons, and dashboard-reference row icons.
  - Preserved visible labels, empty-state copy/actions, dashboard binding rows, reference open actions, metric list filtering, metric editing, validation, saved app schema, runtime behavior, services, ids, ports, and contracts.
  - Updated the focused `MetricDesigner_UsesNeutralMetricMarkerHooks` guard to require hidden decorative icon treatment and reject the old unhidden snippets.
  - Verification passed:
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~MetricDesigner_UsesNeutralMetricMarkerHooks" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
  - Source-only Metric Designer icon scan, neutral added-text scan, and `git diff --check` passed.
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
  - Next target: continue scanning high-use workspace chrome for remaining decorative icon/accessibility gaps or stale selectors without broadening runtime or schema scope.
- Dashboard Designer decorative icon accessibility result:
  - Added hidden decorative semantics to the selection summary icon, edit-mode drag hint icon, and live fallback widget marker in `DashboardDesigner`.
  - Preserved visible text/status copy, toolbar commands, grid editing, drag/drop hints, live preview fallback rows, saved dashboard schema, runtime behavior, services, ids, ports, and contracts.
  - Updated focused Dashboard Designer guards to require the hidden decorative treatment and reject the old unhidden selection/drag snippets.
  - Verification passed:
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~DashboardDesigner_UsesFlatCompactDashboardToolbar|FullyQualifiedName~DashboardDesigner_UsesFlatLivePreviewChrome|FullyQualifiedName~DashboardDesigner_EditGridUsesFlatEditingStateAffordances" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
  - Source-only Dashboard Designer icon scan, neutral added-text scan, and `git diff --check` passed.
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
  - Next target: continue scanning high-use workspace chrome for remaining decorative icon/accessibility gaps or stale selectors without broadening runtime or schema scope.
- Connection Panel decorative icon accessibility result:
  - Added hidden decorative semantics to the Connections title cable icon and empty-state cable icon in `ConnectionPanel`.
  - Preserved visible title/count copy, add/connect/disconnect/remove actions, row labels, live connection filtering, state markers, errors, connection behavior, saved app schema, runtime behavior, services, ids, ports, and contracts.
  - Updated the focused `ConnectionPanel_UsesFlatCompactConnectionRows` guard to require hidden decorative cable icon treatment and reject the old unhidden snippets.
  - Verification passed:
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~ConnectionPanel_UsesFlatCompactConnectionRows" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
  - Source-only Connection Panel icon scan, neutral added-text scan, and `git diff --check` passed.
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
  - Next target: continue scanning high-use workspace chrome for remaining decorative icon/accessibility gaps or stale selectors without broadening runtime or schema scope.
- Session Panel decorative icon accessibility result:
  - Added hidden decorative semantics to the recording strip icon, Recordings title icon, selected-session strip icon, and empty-state icon in `SessionPanel`.
  - Preserved visible recording/session copy, search, live switching, session grouping, row selection, markers, recording actions, session behavior, saved app schema, runtime behavior, services, ids, ports, and contracts.
  - Updated the focused `SessionPanel_UsesFlatGroupedSessionRows` guard to require hidden decorative icon treatment and reject the old unhidden snippets.
  - Verification passed:
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~SessionPanel_UsesFlatGroupedSessionRows" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
  - Source-only Session Panel icon scan, neutral added-text scan, and `git diff --check` passed.
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
  - Next target: continue scanning high-use workspace chrome for remaining decorative icon/accessibility gaps or stale selectors without broadening runtime or schema scope.
- Payload Inspector decorative icon accessibility result:
  - Added hidden decorative semantics to the payload format marker and payload view tab icons in `PayloadInspectorPanel`.
  - Preserved visible title/topic/meta copy, tab labels, tab/panel ids, active view switching, formatted/raw/hex/meta payload views, payload inspection behavior, saved app schema, runtime behavior, services, ids, ports, and contracts.
  - Updated the focused `PayloadInspectorPanel_UsesFlatCompactInspectorChrome` guard to require hidden decorative icon treatment and reject the old unhidden snippets.
  - Verification passed:
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~PayloadInspectorPanel_UsesFlatCompactInspectorChrome" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
  - Source-only Payload Inspector icon scan, neutral added-text scan, and `git diff --check` passed.
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
  - Next target: continue scanning high-use workspace chrome for remaining decorative icon/accessibility gaps or stale selectors without broadening runtime or schema scope.
- Shared EmptyView decorative icon accessibility result:
  - Added hidden decorative semantics to the shared `EmptyView` fallback inbox icon.
  - Preserved custom content rendering, fallback message text, Topic Tree empty-state usage, saved app schema, runtime behavior, services, ids, ports, and contracts.
  - Added the focused `EmptyView_HidesDecorativeInboxIcon` guard to require hidden decorative icon treatment and reject the old unhidden snippet.
  - Verification passed:
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~EmptyView_HidesDecorativeInboxIcon" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
  - Source-only EmptyView icon scan, neutral added-text scan, and `git diff --check` passed.
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
  - Next target: continue scanning high-use workspace chrome for remaining decorative icon/accessibility gaps or stale selectors without broadening runtime or schema scope.
- App shell command accessibility result:
  - Added explicit accessible labels to the icon-only topbar project commands, theme command, and MQTT publisher panel toggle in `MainLayout`.
  - Hid the decorative stopping-spinner and drag-preview icons from assistive output.
  - Preserved project creation/open/save/save-as handlers, theme cycling, live publisher panel visibility, drag preview behavior, saved app schema, runtime behavior, services, ids, ports, and contracts.
  - Updated the focused `MainLayout_RemovesSessionOnlyLeftRail` guard to require the shell command labels and hidden decorative icon treatment.
  - Verification passed:
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~MainLayout_RemovesSessionOnlyLeftRail" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
  - Source-only MainLayout accessibility scan, neutral added-text scan, and `git diff --check` passed.
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
  - Next target: continue scanning high-use workspace chrome for remaining custom-control accessibility gaps or stale selectors without broadening runtime or schema scope.
- App shell marker accessibility result:
  - Added hidden decorative semantics to the topbar app runtime dot, topbar live connection dot, and bottom-bar live dot in `MainLayout`.
  - Preserved adjacent visible app/runtime/live labels, tooltips, state classes, footer facts, saved app schema, runtime behavior, services, ids, ports, and contracts.
  - Updated the focused `MainLayout_RemovesSessionOnlyLeftRail` guard to require hidden marker dots and reject the old unhidden snippets.
  - Verification passed:
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~MainLayout_RemovesSessionOnlyLeftRail" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
  - Source-only MainLayout marker scan, neutral added-text scan, and `git diff --check` passed.
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
  - Next target: continue scanning high-use workspace chrome for remaining custom-control accessibility gaps or stale selectors without broadening runtime or schema scope.
- Setup dialog decorative icon accessibility result:
  - Added hidden decorative semantics to title, section, and summary `MudIcon` instances in Add Connection, New App, New Pipeline, Save As, Start Recording, and Topic Explorer Setup dialogs.
  - Preserved dialog validation, Enter handling, file/certificate pickers, default naming, dialog results, saved app schema, runtime behavior, MQTT behavior, services, ids, ports, and contracts.
  - Updated the focused setup dialog guards to require the hidden decorative icon treatment and reject old unhidden snippets.
  - Verification passed:
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~NewAppDialog|FullyQualifiedName~AddConnectionDialog|FullyQualifiedName~NewPipelineDialog|FullyQualifiedName~SaveAsDialog|FullyQualifiedName~StartRecordingDialog|FullyQualifiedName~TopicExplorerSetupDialog" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
  - Source-only setup-dialog icon scan, neutral added-text scan, and `git diff --check` passed.
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
  - Next target: continue scanning high-use workspace chrome for remaining custom-control accessibility gaps or stale selectors without broadening runtime or schema scope.
- Dashboard editor dialog decorative icon accessibility result:
  - Added hidden decorative semantics to title, section, preview, empty-state, and note `MudIcon` instances in `DashboardWidgetEditorDialog` and `DashboardTrackEditorDialog`.
  - Preserved visible labels, widget settings, filter fields, topic-tree settings, track sizing preview, reset/apply behavior, dashboard schema, runtime behavior, services, ids, ports, and contracts.
  - Updated the focused dashboard dialog guards to require the hidden decorative icon treatment and reject old unhidden snippets.
  - Verification passed:
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~DashboardTrackEditorDialog|FullyQualifiedName~DashboardWidgetEditorDialog" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
  - Source-only dashboard-dialog icon scan, neutral added-text scan, and `git diff --check` passed.
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
  - Next target: continue scanning high-use workspace chrome for remaining custom-control accessibility gaps or stale selectors without broadening runtime or schema scope.
- Scenario dialog decorative icon accessibility result:
  - Added hidden decorative semantics to title, validation, report-summary, metric, issue, and empty-state `MudIcon` instances in `ScenarioStepEditorDialog` and `ScenarioRunReportDialog`.
  - Preserved visible titles, subtitles, validation copy, report metadata, copy/export actions, close behavior, scenario definitions, report content, runtime behavior, services, ids, ports, schemas, and contracts.
  - Updated the focused scenario dialog guards to require hidden decorative icon treatment and reject old unhidden snippets.
  - Verification passed:
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~ScenarioStepEditorDialog|FullyQualifiedName~ScenarioRunReportDialog" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
  - Source-only scenario-dialog icon scan passed.
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
  - Next target: continue scanning high-use workspace chrome for remaining custom-control accessibility gaps or stale selectors without broadening runtime or schema scope.
- Test Studio shell decorative icon accessibility result:
  - Added hidden decorative semantics to the Test Studio title and mode-tab `MudIcon` instances.
  - Preserved visible title/subtitle, test/run summary text, tablist semantics, designer/runner switching, scenario designer and runner composition, project state, saved app schema, runtime behavior, services, ids, ports, schemas, and contracts.
  - Updated the focused Test Studio guard to require hidden decorative icon treatment and reject old exposed snippets.
  - Verification passed:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~TestStudio_UsesFlatCompactWorkspaceChrome" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
  - Source-only Test Studio icon scan passed.
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
  - Next target: continue scanning Test Runner and Test Scenario Designer chrome for remaining decorative icon/accessibility gaps without broadening runtime or schema scope.
- Test Runner decorative icon accessibility result:
  - Added hidden decorative semantics to direct status, history, preflight, result, first-run, timeline, activity, event, and log `MudIcon` markers in `TestRunnerConsole`.
  - Preserved visible labels, row-level aria labels, report actions, history menu behavior, scenario run behavior, runtime/log streams, project state, saved app schema, runtime behavior, services, ids, ports, schemas, and contracts.
  - Updated the focused Test Runner guard to reject exposed direct `MudIcon` lines in runner markup.
  - Verification passed:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~TestRunnerConsole_UsesFlatCompactRunnerChrome" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
  - Source-only Test Runner icon scan passed.
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
  - Next target: continue scanning Test Scenario Designer chrome for remaining decorative icon/accessibility gaps without broadening runtime or schema scope.
- Test Scenario Designer decorative icon accessibility result:
  - Added hidden decorative semantics to direct empty, heading, run-context, history, builder, starter, phase, step-result, action, scope, and event `MudIcon` markers in `TestScenarioDesigner`.
  - Added explicit labels to the custom latest-run reset and step move/edit/delete icon buttons.
  - Preserved visible labels, row/card aria labels, scenario build/run actions, step ordering/edit/delete behavior, report actions, run history, project state, saved app schema, runtime behavior, services, ids, ports, schemas, and contracts.
  - Updated the focused Test Scenario Designer guard to reject exposed direct `MudIcon` lines and require explicit labels for custom icon buttons.
  - Verification passed:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~TestScenarioDesigner_UsesFlatCompactScenarioChrome" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
  - Source-only Test Scenario Designer icon scan, neutral added-text scan, and `git diff --check` passed.
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
  - Next target: continue scanning high-use workspace chrome for remaining custom-control accessibility gaps or stale selectors without broadening runtime or schema scope.
- Dashboard Inspector/property-grid decorative icon accessibility result:
  - Added hidden decorative semantics to direct inspector, group-toggle, help, select-arrow, color-picker, segmented-option, metric/open, reset, duplicate/delete, visual-metric, add-card, and empty-state `MudIcon` markers in `DashboardInspector` and the shared property-grid controls.
  - Preserved visible labels, button labels, property group ownership, select/listbox ownership, color popover ownership, metric card ordering, widget actions, dashboard editing behavior, saved dashboard schema, runtime behavior, services, ids, ports, schemas, and contracts.
  - Updated the focused Dashboard Inspector guard to scan the inspector/property-grid markups and reject exposed direct `MudIcon` lines.
  - Verification passed:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~DashboardInspector_UsesDensePropertyGridAndIconMetricControls" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
  - Source-only Dashboard Inspector/property-grid icon scan, neutral added-text scan, and `git diff --check` passed.
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
  - Next target: continue scanning high-use workspace chrome for remaining custom-control accessibility gaps or stale selectors without broadening runtime or schema scope.
- Flow Designer decorative icon accessibility result:
  - Added hidden decorative semantics to direct canvas-title, empty-canvas, and drop-highlight `MudIcon` markers in `FlowDesigner`.
  - Preserved visible labels, the neutral `Pipeline loaded` subtitle path, unsaved/no-pipeline states, runtime marker, metrics, diagnostics action, diagram canvas, navigator, link-condition editor, drag/drop behavior, workflow JSON, saved app schema, runtime behavior, services, ids, ports, schemas, and contracts.
  - Updated the focused Flow Designer guard to reject exposed direct `MudIcon` lines.
  - Verification passed:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~FlowDesigner_UsesFlatCompactCanvasChrome" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
  - Source-only Flow Designer icon scan, neutral added-text scan, and `git diff --check` passed.
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
  - Next target: continue scanning high-use workspace chrome for remaining custom-control accessibility gaps or stale selectors without broadening runtime or schema scope.
- App Structure menu decorative icon accessibility result:
  - Added hidden decorative semantics to direct empty/app, broker, artifact, test-result, empty-row, command-add, and command-cue `MudIcon` markers in `AppStructureMenu`.
  - Preserved visible labels, broker state text, active `aria-current` markers, delete labels/actions, add commands, test run summaries, app selection/routing, saved app schema, runtime behavior, services, ids, ports, schemas, and contracts.
  - Updated the focused App Structure menu guard to reject exposed direct `MudIcon` lines.
  - Verification passed:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~AppStructureMenu_UsesCompactInlineArtifactActions" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
  - Source-only App Structure menu icon scan, neutral added-text scan, and `git diff --check` passed.
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
  - Next target: continue scanning high-use workspace chrome for remaining custom-control accessibility gaps or stale selectors without broadening runtime or schema scope.
- Metric Designer decorative icon accessibility result:
  - Added hidden decorative semantics to direct filter, command, empty-state, list-row, editor-header, section, preview, reference, validation, select-cue, and parameter-help `MudIcon` markers in `MetricDesigner`.
  - Preserved visible labels, button labels, tooltips, metric filtering, metric creation/editing, type changes, preview/readout facts, reference summaries, validation links, dashboard bindings, saved app schema, runtime behavior, services, ids, ports, schemas, and contracts.
  - Updated the focused Metric Designer guard to reject exposed direct `MudIcon` lines.
  - Verification passed:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~MetricDesigner" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
  - Source-only Metric Designer icon scan, neutral added-text scan, and `git diff --check` passed.
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
  - Next target: continue scanning high-use workspace chrome for remaining custom-control accessibility gaps or stale selectors without broadening runtime or schema scope.
- Topic Explorer decorative icon accessibility result:
  - Added hidden decorative semantics to direct title, stored-session, empty-state, broker, no-traffic, payload-view, publish, history-empty, and selected-message empty `MudIcon` markers in `TopicExplorerPanel`.
  - Preserved visible labels, button labels, tooltips, broker tree, latest payload controls, publish composer, publish reuse actions, history filters/export, payload diff, stats, monitor resolution, publish behavior, saved app schema, runtime behavior, services, ids, ports, schemas, and contracts.
  - Updated the focused Topic Explorer guard to reject exposed direct `MudIcon` lines.
  - Verification passed:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~TopicExplorerPanel|FullyQualifiedName~TopicExplorerMonitorResolver|FullyQualifiedName~LiveMqttWorkspaceService" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
  - Source-only Topic Explorer icon scan, neutral added-text scan, and `git diff --check` passed.
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
  - Next target: continue scanning high-use workspace chrome for remaining custom-control accessibility gaps or stale selectors without broadening runtime or schema scope.
- Dashboard Designer decorative icon accessibility result:
  - Added hidden decorative semantics to direct heading, empty-state, track-handle, grid-empty, widget-action, live-preview, live-empty, and drop-marker `MudIcon` markers in `DashboardDesigner`.
  - Preserved visible labels, button labels, track editing, widget edit/simulate/delete actions, drag/drop affordances, live preview, grid sizing, dashboard schema, runtime behavior, services, ids, ports, schemas, and contracts.
  - Updated the focused Dashboard Designer guard to reject exposed direct `MudIcon` lines.
  - Verification passed:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~DashboardDesigner" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
  - Source-only Dashboard Designer icon scan, neutral added-text scan, and `git diff --check` passed.
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
  - Next target: continue scanning high-use workspace chrome for remaining custom-control accessibility gaps or stale selectors without broadening runtime or schema scope.
- App Tree decorative icon accessibility result:
  - Added hidden decorative semantics to direct test artifact and test-run marker `MudIcon` instances in `AppTreePanel`; existing hidden app, pipeline, metric, dashboard, empty, section, and add-row icons remain covered.
  - Preserved visible labels, row aria labels, selection/routing, keyboard activation, section toggles, test creation/deletion, test run summaries, project state, saved app schema, runtime behavior, services, ids, ports, schemas, and contracts.
  - Updated the focused App Tree guard to reject exposed direct `MudIcon` tags across multi-line attributes.
  - Verification passed:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~AppTreePanel_UsesCompactTestManagementRows" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
  - Source-only App Tree icon scan, neutral added-text scan, and `git diff --check` passed.
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
  - Next target: continue scanning high-use workspace chrome for remaining custom-control accessibility gaps or stale selectors without broadening runtime or schema scope.
- Metric dialog direct icon accessibility result:
  - Added hidden decorative semantics to all direct `MudIcon` glyphs in Metric create, rename, duplicate, type-change, confirm, and delete dialogs, extending the earlier title-icon wrapper treatment to search, clear, type, empty, id, validation, note, warning, reference, and reset markers.
  - Preserved visible labels, button labels, metric type search/counts, validation rows, destructive warnings, tone markers, binding/reference details, dialog results, metric model behavior, dashboard binding behavior, saved app schema, runtime behavior, services, schemas, ids, ports, and contracts.
  - Updated the focused metric creation and metric action dialog guards to reject exposed direct `MudIcon` tags across complete tags.
  - Verification passed:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~MetricCreateDialog_UsesFlatCompactCreationChrome|FullyQualifiedName~MetricActionDialogs_UseFlatCompactModalChrome" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
  - Source-only Metric dialog icon scan passed.
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
  - Next target: continue scanning high-use workspace chrome for remaining decorative icon/accessibility gaps or stale selectors without broadening runtime or schema scope.
- Component Catalog decorative icon accessibility result:
  - Added hidden decorative semantics to direct catalog title, empty-state, item tile, drag-grip, and add-affordance `MudIcon` glyphs in `ComponentCatalogPanel`.
  - Preserved visible catalog titles, meta strip, count, search, empty labels, item aria labels, drag/click/keyboard add behavior, dashboard widget requirement rows, test step metadata, saved app schema, runtime behavior, services, schemas, ids, ports, and contracts.
  - Updated the focused Component Catalog guard to reject exposed direct `MudIcon` tags across complete tags.
  - Verification passed:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~ComponentCatalogPanel" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
  - Source-only Component Catalog icon scan, neutral added-text scan, and `git diff --check` passed.
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
  - Next target: continue scanning high-use workspace chrome for remaining decorative icon/accessibility gaps or stale selectors without broadening runtime or schema scope.
- Workspace Log decorative icon accessibility result:
  - Added hidden decorative semantics to direct title, fixed-scope, empty-state, and row severity `MudIcon` glyphs in `WorkspaceLogPanel`.
  - Preserved visible title/subtitle copy, scope/level/search filters, copy/export/reset/clear actions, visible-row export scope, row aria labels, log details/context, saved app schema, runtime logging behavior, services, schemas, ids, ports, and contracts.
  - Updated the focused Workspace Log guard to reject exposed direct `MudIcon` tags across complete tags.
  - Verification passed:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~WorkspaceLogPanel" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
  - Source-only Workspace Log icon scan passed.
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
  - Next target: continue scanning high-use workspace chrome for remaining decorative icon/accessibility gaps or stale selectors without broadening runtime or schema scope.
- App JSON direct icon accessibility result:
  - Added hidden decorative semantics to direct title and empty-state `MudIcon` glyphs in `AppJsonPanel`.
  - Preserved visible title/file copy, JSON summary, unsaved indicator, copy action, Monaco viewer setup/sync, app JSON generation, saved app schema, runtime behavior, services, schemas, ids, ports, and contracts.
  - Updated the focused App JSON guard to reject exposed direct `MudIcon` tags across complete tags.
  - Verification passed:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~AppJsonPanel" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
  - Source-only App JSON icon scan passed.
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
  - Next target: continue scanning high-use workspace chrome for remaining decorative icon/accessibility gaps or stale selectors without broadening runtime or schema scope.
- Apps and Live Publisher panel direct icon accessibility result:
  - Added hidden decorative semantics to direct title, empty-state, app-row, publish-button, and diagnostic `MudIcon` glyphs in `AppsPanel` and `LiveInspectorPanel`.
  - Preserved visible app rows, row labels, active/unsaved markers, close app actions, MQTT publisher form controls, retain toggle, diagnostics copy, manual publish recording, MQTT behavior, saved app schema, runtime behavior, services, schemas, ids, ports, and contracts.
  - Updated the focused Apps and Live Inspector guards to reject exposed direct `MudIcon` tags across complete tags.
  - Verification passed:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~AppsPanel|FullyQualifiedName~LiveInspectorPanel" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
  - Source-only Apps and Live Inspector icon scan passed.
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
  - Next target: continue scanning dashboard widget surfaces and remaining node chrome for decorative icon/accessibility gaps without broadening runtime or schema scope.
- Dashboard display direct icon accessibility result:
  - Added hidden decorative semantics to direct refresh, widget header, metric tile, empty topic, and visualization `MudIcon` glyphs across `DashboardQueryPreviewFrame`, `DashboardWidgetView`, and dashboard widget components.
  - Preserved visible preview action text, widget titles/subtitles, metric values, chart/table/topic rendering, dashboard data/query behavior, dashboard schema, runtime behavior, services, schemas, ids, ports, and contracts.
  - Updated the focused dashboard query preview and dashboard widget guards to reject exposed direct `MudIcon` tags across complete tags and all dashboard widget Razor files.
  - Verification passed:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~DashboardQueryPreviewFrame|FullyQualifiedName~DashboardWidgets" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
  - Source-only dashboard display icon scan passed.
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
  - Next target: continue scanning remaining node chrome for decorative icon/accessibility gaps without broadening runtime or schema scope.
- Dynamic Mapper node direct icon accessibility result:
  - Added hidden decorative semantics to the direct input JSON error `MudIcon` glyph in `DynamicMapperNodeWidget`.
  - Preserved alert text, editor workspaces, sample reload, schema controls, preview behavior, saved node configuration, workflow JSON, runtime behavior, services, schemas, ids, ports, and contracts.
  - Updated the focused Dynamic Mapper guard to reject exposed direct `MudIcon` tags across complete tags.
  - Verification passed:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~DynamicMapperNodeWidget" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
  - Source-only Dynamic Mapper icon scan and workspace-wide direct `MudIcon` scan passed.
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
  - Next target: audit the workspace for the next concrete accessibility or stale-selector polish gap now that direct exposed `MudIcon` glyphs are cleared.
- MudIconButton command label accessibility result:
  - Added explicit accessible labels to the Dynamic Mapper schema-file picker and sample reload icon commands, plus the Topic Explorer clear-topic-selection icon command.
  - Preserved existing tooltips, click handlers, schema selection, sample reload, topic selection clearing, workflow JSON, saved app schema, runtime behavior, services, schemas, ids, ports, and contracts.
  - Updated the focused Topic Explorer and Dynamic Mapper guards to require accessible labels on complete `MudIconButton` command tags.
  - Verification passed:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~TopicExplorerPanel|FullyQualifiedName~DynamicMapperNodeWidget" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~FlowDesigner_UsesFlatCompactCanvasChrome" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
  - Workspace-wide `MudIconButton` label scan passed; the initial parallel focused-test attempt hit the known XAML intermediate-file lock, then both guards passed on serial rerun.
  - Flow Designer canvas chrome was rechecked as already accepted: `FlowDesigner` still uses `Pipeline loaded`, and the focused guard still rejects generic readiness canvas chrome.
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
  - Next target: continue scanning high-use workspace chrome for custom-control accessibility gaps or stale selectors without broadening runtime or schema scope.
- Shared node shell accessibility result:
  - Added explicit accessible labels to the shared diagram node collapse/expand and edit icon commands in `NodeWidgetShell`.
  - Hid the decorative node-type header glyph from assistive output and gave the diagnostic glyph a meaningful accessible diagnostic label instead of treating it as decorative.
  - Preserved node selection/collapse behavior, edit dialog opening, diagnostic tooltip text, node title/display/category chrome, ports, link routing, workflow JSON, saved app schema, runtime behavior, services, schemas, ids, ports, and contracts.
  - Updated the focused shared node shell guard to require labeled icon commands and accessible direct node-shell icons.
  - Verification passed:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~NodeWidgetShell_UsesCompactNodeChrome" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
  - Source-only `NodeWidgetShell` icon command/direct-icon scan passed.
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
  - Next target: continue the direct-icon audit for remaining high-use workspace surfaces such as project tab commands and topic tree rows without broadening runtime or schema scope.
- Workspace tab and topic tree direct icon accessibility result:
  - Hid decorative delete/close glyphs inside already labeled workspace project-tab buttons and app close command.
  - Hid decorative topic-tree chevron, empty-search, and filtered-topic glyphs while preserving the surrounding chevron button labels, treeitem labels, selection, keyboard activation, topic filtering, project tab routing, delete/close actions, saved app schema, runtime behavior, services, schemas, ids, ports, and contracts.
  - Updated focused WorkspacePage and TopicTree guards to reject direct `MudIcon` tags without an explicit hidden or labeled accessibility treatment.
  - Verification passed:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~WorkspacePage|FullyQualifiedName~TopicTreeNode" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
  - Full `src\FluxMq.UI` direct `MudIcon` accessibility scan and `MudIconButton` label scan passed.
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
  - Next target: audit remaining custom role/button surfaces for accessible keyboard semantics and stale selectors now that direct icon scans pass across UI Razor files.
- Topic Tree static placeholder accessibility result:
  - Removed the no-op click handler from the non-interactive static topic chevron placeholder while keeping it hidden from assistive output and propagation-isolated inside the tree row.
  - Preserved branch chevron button behavior, treeitem selection, keyboard activation, compact branch-line chrome, topic filtering, saved app schema, runtime behavior, services, schemas, ids, ports, and contracts.
  - Updated the focused TopicTree guard to reject the stale `IgnoreChevronClick` placeholder handler.
  - Verification passed:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~TopicTreeNode_UsesCompactBranchLineChrome" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
  - Strict custom-button source scan passed after the placeholder cleanup.
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
  - Next target: continue stale-selector and custom-control audit across high-use workspace chrome without broadening runtime or schema scope.
- Metric Designer row accessibility result:
  - Added explicit row-level accessible labels to Metric Designer metric row buttons so dense table-like rows announce the metric display name, id, type, reference count, and latest reading state.
  - Preserved row selection, metric filtering, metric editing, latest-value display, reference counts, saved app schema, runtime behavior, services, schemas, ids, ports, and contracts.
  - Updated the focused Metric Designer guard to require the row label helper and row `aria-label` binding.
  - Verification passed:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~MetricDesigner_UsesNeutralMetricMarkerHooks" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
  - Source-only Metric row label scan passed.
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
  - Next target: continue auditing high-use workspace control surfaces for concise accessible names and stale selectors without broadening runtime or schema scope.
- Workspace Log filter accessibility result:
  - Added explicit accessible labels to the scope and level segmented filter buttons so short visible options such as `All` announce their filter context.
  - Preserved scope/level filtering, search, visible-row copy/export, reset/clear actions, row rendering, saved app schema, runtime logging behavior, services, schemas, ids, ports, and contracts.
  - Updated the focused Workspace Log guard to require the filter label helpers and bindings.
  - Verification passed:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~WorkspaceLogPanel_UsesFlatCompactWorkspaceChrome" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
  - Source-only Workspace Log filter label scan passed.
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
  - Next target: continue auditing high-use workspace control surfaces for concise accessible names and stale selectors without broadening runtime or schema scope.
- Publish retain control accessibility result:
  - Added explicit accessible labels to the Live Publisher and Topics publish retain toggles so their pressed state is announced with publish context instead of the short visible `Retain` text alone.
  - Preserved retain toggling, QoS controls, publish submit behavior, app-broker targeting, manual publish recording, MQTT behavior, saved app schema, runtime behavior, services, schemas, ids, ports, and contracts.
  - Updated the focused Live Inspector and Topic Explorer guards to require the retain toggle accessible label while keeping existing pressed-state assertions.
  - Verification passed:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~LiveInspectorPanel|FullyQualifiedName~TopicExplorerPanel" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
  - Source-only publish retain label scan passed.
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
  - Next target: continue auditing high-use workspace controls for concise accessible names and stale selectors without broadening runtime or schema scope.
- Component Catalog keyboard accessibility result:
  - Expanded catalog item keyboard activation to accept the legacy `Spacebar` key value in addition to `Enter` and the literal space key, matching the advertised `aria-keyshortcuts="Enter Space"` and the custom role-button behavior used elsewhere.
  - Preserved catalog item click/drag behavior, dashboard/test/pipeline add behavior, availability gating, labels, saved app schema, runtime behavior, services, schemas, ids, ports, and contracts.
  - Updated the focused Component Catalog guard to require the complete activation-key expression.
  - Verification passed:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~ComponentCatalogPanel" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
  - The first focused build attempt timed out under local process contention; the longer serial rerun passed cleanly.
  - Source-only Component Catalog keyboard scan passed.
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
  - Next target: continue auditing high-use workspace controls for keyboard semantics, concise accessible names, and stale selectors without broadening runtime or schema scope.
- App Tree empty-test keyboard accessibility result:
  - Routed the empty test-scenario row keyboard activation through App Tree's shared `IsActivationKey` helper so `Spacebar`, `Enter`, and the literal space key all work consistently across custom role-buttons.
  - Preserved empty test-row click behavior, test creation dialog behavior, section expansion, app/project routing, labels, saved app schema, runtime behavior, services, schemas, ids, ports, and contracts.
  - Updated the focused App Tree guard to require the shared activation helper in the empty-test keyboard path.
  - Verification passed:
    - `dotnet build src\FluxMq.UI\FluxMq.UI.csproj --no-restore /m:1 /nodeReuse:false -p:UseSharedCompilation=false -v:minimal`
    - `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore --filter "FullyQualifiedName~AppTreePanel_UsesCompactTestManagementRows" --verbosity minimal /nodeReuse:false -p:UseSharedCompilation=false`
  - Source-only App Tree keyboard scan passed.
  - Desktop manual check was not run because native desktop automation was not reauthorized for this slice.
  - Next target: continue auditing high-use workspace controls for keyboard semantics, concise accessible names, and stale selectors without broadening runtime or schema scope.
