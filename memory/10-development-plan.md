---
name: FluxMQ development plan
description: Living step-by-step implementation plan and progress tracker for FluxMQ.
type: project
---
# FluxMQ Development Plan

This is the active implementation plan. Keep it updated after every meaningful development slice so project memory stays in sync with the app.

## Operating Rules

- Build in small vertical slices from [09-feature-list.md](09-feature-list.md), not broad rewrites.
- Keep components standalone: sources emit events, mappers transform, filters/routers decide, actors consume explicit command input, observers emit projections.
- Do not hide request-model bridges. If a source emits `MqttEnvelope` and an actor expects `MqttPublishRequest`, the graph should show a `flow.mapper`.
- Keep request models as actor input contracts, not user-facing catalog components.
- Keep runtime definitions and UI diagrams round-trippable through JSON.
- Keep live, stored, replayed, imported, and generated messages on the same runtime/projection path.
- Prefer Dynamic Expresso for C#-style scalar expressions and Jsonata for JSON payload mapping.
- Use structured build errors and `FlowError` outputs instead of letting ordinary user/config failures escape runtime boundaries.
- Run `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -m:1` before calling a runtime slice complete.
- For UI slices, also run `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj --no-restore -p:UseSharedCompilation=false` and visually inspect the desktop workspace when the change affects layout or interaction.
- During development, tell the user exactly what to check and how to check it before treating a feature/update as accepted.
- After each meaningful feature/update, wait for user confirmation before committing or merging. Commits should be scoped to the confirmed slice; merges should only happen after the user explicitly approves the merge.

## Current Target

**Phase:** V1 critical path
**Active feature:** Package alignment, scenario model completion, dashboard runtime binding, designer polish, and release readiness
**Status:** In progress
**Started:** 2026-06-02

The current plan is to finish V1 by draining the remaining critical path as small mergeable slices:

1. Keep FluxFlow package references and runtime registrations aligned with the latest explicit component packages.
2. Keep project memory current after meaningful direction changes.
3. Finish the test/scenario model around runner-owned normal components plus narrow test-only blocks.
4. Finish dashboard runtime/widget binding so dashboard layout cells can show data, not only layout structure.
5. Polish designer interactions and empty states that block everyday use.
6. Add release readiness checks: packaging, sample app definition, smoke documentation, and repeatable validation commands.
7. Remove stale internal code and stale project-visible wording before the first release.

Current package alignment status: storage now uses the explicit `FluxFlow.Components.Storage.FileSystem` adapter package with the base storage package version required by that adapter. The old broad storage adapter package is no longer referenced by source or tests.

Dashboard runtime binding is now useful enough for first-release validation: dashboard cells can show filtered event totals, latest matching events, and rolling event rates over the same runtime event stream used by logs and scenarios. The scenario plane already owns `mqtt.publisher`, `mqtt.trigger`, `when.event`, and `expect.event`; continue keeping normal-component reuse explicit while reserving special behavior for expectation and control blocks.

The V1 readiness gate has local command, Windows package, docs cleanup, and packaged desktop smoke coverage. The largest remaining V1 gaps are candidate testing, focused designer/editor polish found during candidate testing, and any stale wording/code cleanup that appears in the final review. Dashboard work should now focus on missing practical widget types only when a real validation workflow needs them.

Ops-team feedback from 2026-06-03: the topic tree is one of the first visual structures operations users expect to matter. Treat the MQTT topic tree as a first-class workspace visualization, not a narrow incidental panel. Before the next major desktop layout/design pass, make an explicit layout decision for the topic tree: larger dockable explorer, dedicated topic explorer artifact/view, or an ops monitoring/dashboard surface with topic hierarchy as a primary object.

The mapper workbench now follows a JSONata Exerciser-like shape: Monaco JSON input on the left, Monaco mapper expression in the middle, and live JSON result on the right. MQTT envelope samples use `{ topic, qos, retain, receivedAt, payload }`; arbitrary JSON input is treated as payload for quick experimentation. Mapper output configuration now separates the runtime target type from the result contract: `typed`, `any`, or `json-schema-file`. `json.schema-validator` exists as a standalone runtime/UI component backed by `FluxFlow.Components.Validation`, so schema validation is a reusable runtime capability rather than mapper-only UI behavior.

Actor editors are in place for `mqtt.publisher`, `mqtt.recorder`, and `file.writer`. Source editors are in place for `generated.source` and `replay.source`. Live broker traffic stays on the existing `mqtt.connection` plus `mqtt.trigger` path; `generated.source` owns its fixed MQTT message list; `replay.source` selects a recorded session and playback speed.

The current diagnostics slice attaches validation, runtime build, and node startup failures to the responsible node where possible. Diagram nodes now show error/warning/info status in their border and header tooltip, while detailed diagnostic review belongs in the first-level Logs tab. The pipeline header's error/warning count should navigate to Logs with the matching level filter instead of opening a canvas overlay that can hide nodes.
The desktop shell now exposes active-app `Validate`, `Run`, and `Stop` actions in the top bar, plus an app runtime state pill, so validation/run feedback is separate from live broker connection state.
The current logging slice keeps diagrams clean by collecting all runtime component `FlowError` outputs into the workspace `Logs` tab by default, without adding hidden definition nodes or visible links. `Flow Logger` remains an explicit observer component for flows that need log entries as stream data. Runtime linking now keeps multi-source input ports open until every linked source completes, so explicit multi-input observers still behave correctly.

The current visual-link slice makes the diagram a real editor for workflow links. Dragging a compatible output port to an input port writes the target port reference into the active workflow JSON, deleting a rendered workflow link removes that reference, and dynamic mapper output ports use their configured result type for type feedback. Plain target inputs are rerouted on new connections; `Flow Logger` keeps append-style behavior for message/log collection inputs.
Deleting a workflow node from the designer now removes it from the active workflow JSON and cleans downstream references to that node, so deleted components do not return after the next add/rebuild action.
MQTT Publisher now emits successful publish log entries to the workspace `Logs` tab by default and updates node activity with published count plus the last topic. This keeps actor execution observable without wiring every actor to an explicit logger block.
Runtime linking now drains unconnected non-diagnostic outputs automatically, so observer/mapper/result outputs left unwired do not block runtime completion or cause Stop to time out. Diagnostic `FlowError` outputs stay available for the workspace `Logs` tab.
The current routeable-decision slice makes decision nodes executable and branchable: `flow.when` is registered in the runtime, and `json.schema-validator` exposes `Result`, `Valid`, and `Invalid` outputs so validation details and envelope branches are distinct.
Runtime `OutputPort<T>` now enforces the first-class broadcast contract for every component output. Component internals may use buffers, transforms, or action blocks, but every runtime-facing output is presented as a broadcast stream, so one output wired to multiple inputs delivers the same message to each target.
Runtime log collection now subscribes to all `FlowLogEntry` output ports by default, not just hand-picked logger/publisher components. `flow.when` emits route entries for `WhenTrue` and `WhenFalse`, so a false branch with no downstream wire is still visible in the workspace Logs tab while testing.
MQTT trigger subscription QoS is now explicit in the node editor. Short-form subscriptions such as `"#"` default to QoS 1, which keeps the default router expression `qos >= 1` useful during live broker tests while still allowing QoS 0 or QoS 2 per subscription.
Pipeline MQTT triggers are now treated as configured subscriber blocks: each row owns topic filter, QoS, retained-message delivery, and retain-flag preservation. Broker-wide live monitoring is separate from pipeline design and auto-subscribes each broker monitor to `#` for the Topics tab and live workspace message projection.
The metrics slice aligned the UI with the runtime component contract. `mqtt.metrics` observes only its `Input` stream, and the metrics node face now renders snapshots produced by that node's runtime `Snapshots` output rather than broker-wide live monitor messages.
The payload inspector projection slice applies the same rule to `mqtt.payload-inspector`: broker-wide payload inspection stays in the Live Inspector panel, while the diagram node activity renders the latest inspection produced by that node's own runtime `Output` stream.
The current payload type slice improves inspection semantics: MQTT payloads remain bytes, but UTF-8 payloads that decode to common literal values now expose human-facing payload kinds so scalar payloads such as `1`, `true`, `"text"`, `null`, and `[1,2]` are not mislabeled as plain text or JSON objects.
The current trigger activity slice applies the same input-driven rule to `mqtt.trigger`: the node card activity should count envelopes emitted by that trigger's configured subscriptions, while broker-wide message counts remain in the Live Inspector panel.
The current metrics-rate slice adds both current and since-start message-per-second data to `mqtt.metrics` snapshots. Current rate uses a configurable rolling window; average rate uses all messages observed since the metrics component started. Both rates are based on the stream feeding the metrics component, not broker-wide traffic. The metrics card display is configurable per node with selectable cards such as messages, current rate, average rate, payload, topics, retained count, or average payload size; card columns control the node grid, and rows are calculated from the selected-card count.
Future metrics refactor note: the current implementation remains MQTT-envelope-specific, which is acceptable for this slice. Longer term, split generic stream metrics from MQTT-specific topic metrics so outputs from dynamic mappers and future protocol components can feed metrics observers without meaningless topic fields.
The current assertion slice starts `F-031` with a standalone `flow.assert` component. It evaluates a configurable expression over a configured input type, emits a typed `FlowAssertionResult`, routes the original value to `Passed` or `Failed`, and emits a log entry for each evaluation so pass/fail behavior is visible in the workspace Logs tab without extra wiring. It defaults to `MqttEnvelope` because current source nodes commonly emit MQTT envelopes, but the component is not MQTT-specific. Assertion input type names now live in one shared app contract, and `StateReducerResult` can be asserted with reducer-specific variables such as `key`, `newState`, and `version`.
The current event-foundation slice intentionally stops before UI. It adds a generic `FlowEvent` stream contract for the debug/tracing/testing/monitoring era. App design/run components keep their normal workflow ports; events are collected by `ApplicationRuntime.Events` as an external side-channel so dashboards, monitors, and field tests can observe behavior without mutating the application definition. Events currently cover meaningful facts such as MQTT message received/published/recorded, file written, JSON schema validated, and assertion evaluated. Expectations should be rebuilt on top of event correlation instead of adding narrow one-purpose components.
App artifact boundary note: an app should contain pipelines, dashboards, and later tests/scenarios as separate artifact families. The pipeline designer remains for app design/run components such as triggers, mappers, validators, routers, publishers, recorders, and writers. The dashboard designer should have its own grid/layout surface and component panel for monitoring blocks. Assertion and expectation tooling belongs to the test/scenario or monitoring plane, not the pipeline design palette; any current pipeline-facing assertion UI should be treated as migration debt after the event foundation is stable.
The current app-artifact model slice adds definition-layer support for dashboards and test/scenario artifacts without adding UI. `ApplicationDefinition` now keeps `dashboards` and `tests` beside `resources` and `workflows`; dashboards have a grid layout plus named widgets, and tests have named steps. Dashboard rows and columns use WPF-like track sizing strings such as `320`, `25%`, `*`, and `2*`, so the future designer can support fixed, percent, and weighted star layouts. Runtime execution still builds from workflows only.
Future logging architecture note: keep the current `FlowLogEntry` stream for graph-visible log data, but plan a standard `Microsoft.Extensions.Logging` bridge so runtime components can use normal .NET logging and the workspace Logs view can subscribe through a single provider/sink instead of component-specific log plumbing.
Future object-stream architecture note: dynamic mappers make it hard to keep every runtime edge permanently static. Move toward object streams as the underlying runtime model, with typed ports acting as schema/contract metadata for designer validation, editor help, and runtime coercion. This should be done as a dedicated runtime refactor, not hidden inside one component.
The current artifact navigation slice moves app structure controls into first-level top-bar menus for connections, pipelines, dashboards, and tests. The left side now stays focused on the active artifact tool panel, so pipeline components, future dashboard widgets, and future test steps can each have their own panel without an always-open app tree taking workspace width.
The current dashboard layout designer slice turns dashboard tabs from placeholders into a real layout editor. The UI reads and writes the dashboard JSON model, renders a full-size grid surface, and can add/remove dashboard cells while keeping widgets separate for a later monitoring-block slice. The grid interaction is inspired by the OpenGarden surface editor: users can choose a row/column layout from a picker, add/remove rows and columns, select cells, merge rectangular selections, split merged cells, and subdivide a selected cell into rows, columns, or a 2 x 2 region. Row and column handles are now the track editors: clicking a handle opens a sizing dialog for fixed, percent, or star sizing plus per-track padding; the old raw row/column text fields are gone.
The current scenario expectation foundation slice starts the test/scenario plane below the UI. `ScenarioRunner` executes registered scenario step runners against the external `FlowEvent` stream, and `expect.event` waits for matching runtime events using event type, topic prefix, subject prefix, status, source, payload preview, and attribute filters. Runtime events now broadcast to multiple observers, so dashboards and scenarios can observe the same app run without stealing events from each other.
The current scenario host slice wires test/scenario execution into `FlowApplicationHost`. The host keeps the loaded app definition, can run a named scenario via `RunScenarioAsync`, starts the runtime when needed, and feeds the scenario runner from `ApplicationRuntime.Events`. This creates the boundary future desktop and CLI test runners should call instead of reaching into runtime internals.
The current scenario action slice adds a generic `ScenarioStepServices` boundary and the first app-level action step, `mqtt.publisher`. Scenario steps can now ask for runtime capabilities through typed services, and the publish step resolves a named MQTT connection resource from the running app and publishes a configured message without inserting test behavior into workflow nodes.
Testing-era boundary: tests/scenarios are a separate plane from app design/run pipelines. Their extra value is integration testing: a test can start or use an app runtime, run arrange/action steps against real or fake resources, observe runtime events, and assert behavior across MQTT, files, HTTP, databases, and future transports. This means we should support different test scenarios and, when needed, test-only orchestration pipelines without polluting production workflow components.
The current CLI scenario runner slice makes tests/scenarios executable outside the desktop UI. `fluxmq scenario --config <path> --name <scenario>` loads the app, starts the host through `RunScenarioAsync`, and returns text or JSON results with per-step status. This is the first command-line entry point for integration-test workflows.
The current scenario step field slice extends `ScenarioStepCatalog` so each step descriptor can own editable field metadata. MQTT publish step fields, defaults, and options now come from the catalog; the editor renders publish controls from descriptors, and the composer uses the same descriptors for new-step defaults. Field descriptors normalize missing option lists to empty lists. Short finite publish choices use MudBlazor `MudToggleGroup`/`MudToggleItem` single-selection groups instead of dropdown popovers, which keeps the scenario editor out of custom z-index/overflow CSS. Toggle labels use the documented `MudToggleItem.Text` parameter rather than child content. The editor also has built-in MQTT fallback options so finite fields cannot render as labels without choices if descriptor options are empty. This is the reusable path for future scenario action/expectation step editors without spreading property-specific controls across each UI surface.
The current scenario result history slice keeps a bounded in-memory list of recent scenario runs in the workspace service. Completed runs are stored newest-first, switching between test artifacts restores the latest result for the selected test, and the test scenario header exposes the latest five selected-test runs through a compact MudBlazor history menu. This is still session-local history; persisted reports/exports remain a future slice.
The current scenario report export slice adds a stable desktop reporting shape for scenario runs. `ScenarioRunReportFormatter` turns a run result into JSON or text with root schema/run/generation metadata, scenario status, duration, aggregate issue summaries, planned-but-not-run steps, per-step configuration, per-step sequence/timing, per-step status/message, matched event details, and matched event offsets relative to the scenario and owning step. Text reports now include matched event source, subject, payload size/preview, and attributes when present, so `.scenario-report.txt` remains useful without opening JSON. The test scenario header can preview the selected scenario report in a MudBlazor tabs dialog, copy the selected scenario report JSON to the clipboard, or save it to disk through MudBlazor tooltip/icon actions. The report preview now creates one report snapshot and renders Summary/JSON/save from that same object, so the displayed report and saved JSON agree on `runId` and `generatedAt`; the same dialog can also save the readable Summary tab as `.scenario-report.txt`. File export reuses the existing `SaveAsDialog` with report-specific parameters and writes through `FlowWorkspaceService` helpers, so the Razor component stays a composition surface instead of a file writer. Scenario history rows are now clickable MudBlazor menu items; selecting a prior run restores it as the active result for review and reporting, guarded so another test's run cannot be selected into the current test.
The current scoped-log slice gives logs a clearer observability boundary. Workspace logs now have explicit `Scope`, `ArtifactKind`, and `ArtifactName` metadata so app runtime rows, test-runner rows, and system/validation rows can be filtered without parsing message text. A first-level Logs tab uses `WorkspaceLogPanel` with compact scope, level, and search filters; the duplicate right-inspector Logs tab was removed. This is still one in-memory project log stream, but it is ready for later per-runtime sinks such as service-hosted app logs, dashboard logs, and external integration-test runner logs without mixing ownership.
The current dashboard event-rate slice adds an `event.rate` widget to the dashboard widget catalog. It reuses the same event filters as `event.counter` and `event.latest`, calculates a one-minute rolling rate from workspace runtime events, renders a compact live card, and keeps isolated test-runner events out of app dashboard counts unless an app runtime is explicitly the observed target.
The current MQTT publisher dock slice repurposes the right workspace panel as one app-level `MQTT Publisher` surface. It is available whenever an app is active, independent of the selected artifact, and scopes its MQTT client selector to the active app's connection resources. The publisher can connect the selected client before publishing, records successful manual publishes into the app event/log projection, and no longer hosts inspect, topic-browser, recordings, or payload-inspection tabs.
The old session-only left rail is removed and should not return. Recorded-session browsing/capture also should not be restored as a tab inside the MQTT publisher dock; future recording/session polish needs its own deliberate home in live traffic tools, replay/source-node selection, or topic/inspection workflows.
The current release-smoke slice adds `eng/verify-samples.ps1` as a repeatable broker-free sample verification path. It builds the CLI once, validates the metrics-only and generated-traffic samples, and runs the generated-traffic sample for a bounded duration so release checks exercise a real source-to-observer flow without requiring an external broker.
The current release-readiness checklist slice adds a durable `docs/release-readiness.md` gate. It collects local tests, sample verification, release-shaped Windows packaging commands, manual UI checks, and blocker rules into one pre-V1 checklist linked from the README and docs index.
The current docs cleanup slice aligns durable docs with the current app/resource boundary and CLI smoke path. App definitions own resources, workflows, dashboards, and tests before executable resources/workflows are projected into the package runtime; bounded CLI runtime examples now use the generated-traffic sample instead of the no-source metrics-only sample.
The current Windows validation workflow slice keeps PR/main feedback fast by running release-shaped restore and tests only. Package artifact generation is still available from manual workflow dispatch, but no longer runs automatically on every pull request while the project is in active development.
The current workflow engine preparation slice moves expression engine implementations into FluxMQ-owned mapping adapters and passes the default expression engine to runtime build paths for link `when` conditions. Keep the shared workflow engine package on the current component-compatible version until all consumed component adapter packages are published against the same node-id namespace.
The current reconnect robustness slice fixes a pre-release race in `MqttConnectionManager`: canceling a reconnect no longer disposes the reconnect cancellation source while the reconnect task may still be using it, and completed reconnect tasks only remove their own dictionary entry instead of deleting a newer reconnect attempt for the same profile.
The current test-runner resource boundary refactor keeps app-level resources as the only connection definition surface while giving scenarios their own reusable MQTT client factory. `IMqttScenarioClientFactory` resolves shared app MQTT resources and creates short-lived runner-owned MQTT clients with separate profile/client ids for saved-definition, CLI, and running-runtime scenario paths. `mqtt.publisher` consumes that boundary through the normal `MqttPublisherComponent`, and future test-runner `mqtt.trigger`, `expect`, and `when` blocks should reuse the same factory instead of introducing a parallel broker-probe engine or tying tests to the app runtime's live MQTT clients.
Naming rule: use "MQTT client" for live connected MQTT objects in developer-facing and domain-facing code. The core MQTT abstraction is `IMqttBrokerClient` with concrete wrapper `MqttBrokerClient`, avoiding a collision with MQTTnet's own `IMqttClient`. Scenario/test-runner boundaries should not introduce new "session" wording for live MQTT clients.
Scenario step naming rule: use the same component vocabulary across pipelines and tests where the behavior is the same. The MQTT publish action is `mqtt.publisher`, matching the normal pipeline actor. The product is still pre-release, so do not add saved-project compatibility aliases.
Scenario definition validation now checks known step ids and step configuration shape before app runtime build. `mqtt.publisher` steps must point at app-level `mqtt.connection` resources, and `expect.event` filters must use the same field contracts the runner reads. Keep this validator in sync when adding test-specific blocks such as `when` or a future scenario MQTT trigger.
Scenario runners now have a run lifetime boundary. Steps can register async cleanup for resources that must outlive a single step, and the runner reports cleanup failures as `scenario.cleanup` results. Use this for long-lived test-runner components such as runner-owned `mqtt.trigger`, not for ordinary short action steps such as `mqtt.publisher`.
The current scenario-owned MQTT trigger slice adds `mqtt.trigger` to the scenario/test plane using the normal `MqttConnectionComponent` and `MqttTriggerComponent`. The step creates a runner-owned MQTT client from the app-level connection resource, starts the trigger for the configured topic filter/QoS/retained flags, appends received trigger events into the scenario journal, and cleans up through `ScenarioRunLifetime`. This is the preferred pattern for future runner-owned normal components: reuse runtime blocks, then add narrow test-only blocks such as `expect.event` and later `when`.
Future UI portability note: keep workspace workflow logic, app/test/dashboard state, validation, report formatting, and command orchestration outside Razor/MudBlazor-specific components where practical. Blazor/MudBlazor remains the current UI for this version, but future Avalonia/Linux support should be able to reuse the application services and view-model-like state instead of reimplementing behavior from Razor files.
Isolated scenario runs must start a runner-owned event source before event-observing steps. Both `expect.event` and `when.event` need an event stream, so a scenario run without an attached app runtime that uses either before `mqtt.publisher` or `mqtt.trigger` should fail fast with guidance instead of timing out or silently skipping.
The current test-designer display slice keeps the scenario UI aligned with the runner-owned normal-component model. `mqtt.trigger`, `when.event`, `expect.event`, and `mqtt.publisher` now have distinct test-card display states, skipped `when.event` guards have a first-class non-error visual state, and scenario step display formatting lives in a small tested UI service instead of being embedded in Razor-only code.
The current diagram cleanup slice removes the last explicit `NotImplementedException` from the UI flow designer. The diagram link factory still supports the same node/port source shapes from Z.Blazor.Diagrams 3.0.4.1, but unexpected source shapes now produce a clear invalid-operation diagnostic instead of looking like unfinished code.
The current control package migration adds `FluxFlow.Components.Control` `0.1.0-alpha.1` and moves the designer/runtime vocabulary to `flow.filter`, `flow.when`, and `flow.assert`. FluxMQ keeps thin wrappers only where product behavior still matters: filter pass-count activity, route log entries, assertion log entries, and assertion events. The old internal predicate/filter/router/assertion evaluation classes are removed, and runtime definitions no longer carry pre-release compatibility aliases for the old control node ids.
The current scenario event-field catalog slice makes `when.event` and `expect.event` catalog-described test steps. Event filter keys, attribute filter keys, and default values now live in `ScenarioStepCatalog`, and `FlowDefinitionComposer` uses the catalog for default configuration across all known scenario step types. The existing MudBlazor editor behavior stays the same, but future test-specific blocks have a cleaner path for field metadata.
The current manual desktop gate launched the packaged portable desktop build and found no obvious release-blocking shell issue. Richer component configuration dialogs and edit views remain a known designer-polish track under `F-022`; treat that as focused polish unless candidate testing exposes a concrete blocker.
The current artifact lifecycle slice adds delete support for pipeline, dashboard, and test artifacts from both top artifact tabs and the app structure menus. Deletions use confirmation dialogs, active-artifact selection falls back cleanly, and deleted pipeline layout positions are removed from stored designer state.
The current metrics-designer polish slice redesigns the app-level Metrics tab as a two-pane resource workbench. Metric creation is catalog-driven, editing is draft-based with save/cancel and dirty prompts, type changes reset parameters only after confirmation, dashboard references are visible from the editor, and live preview reuses the existing metric reading path without changing the app JSON or runtime metric schema. Follow-up polish made list rows, type labels, latest-value states, editor actions, live preview, and reference rows more visually deliberate, then flattened the toolbar and editor input styling so the screen reads like an app workflow rather than a busy form. The remaining acceptance check is manual desktop visual smoke across light/dark and narrow windows.

## Step-by-Step Plan

### Phase 0 - Product And Architecture Memory

Status: Done

Goal: Preserve the product direction and architecture decisions outside chat history.

Tasks:

- Capture original platform proposal.
- Record architecture, roadmap, source-agnostic update direction, dashboard plan, standalone component plan, and dynamic mapping/ops vision.
- Add this feature list and living development plan.

Done when:

- The memory index links the active planning documents.
- The next implementer can identify the active feature and next slice without reading chat history.

### Phase 1 - Core MQTT, Storage, And Inspector

Status: Done

Goal: Make FluxMQ useful as a local MQTT workbench.

Tasks:

- Implement connection profiles and MQTT client lifecycle.
- Receive and publish MQTT messages.
- Persist connection profiles, sessions, and messages in LiteDB.
- Add payload inspection for common payload formats.
- Add recording and stored-session browsing.

Done when:

- Users can connect, subscribe, publish, inspect payloads, record sessions, and load stored messages.
- Core/component tests cover the local domain behavior.

### Phase 2 - Flow Application Runtime

Status: Done

Goal: Run user-defined workflows from typed configuration.

Tasks:

- Add `ApplicationDefinition`, workflow definitions, shared resources, and typed links.
- Add runtime factory registry and typed runtime ports.
- Add lifecycle start/stop/dispose and phase-based startup.
- Add CLI validate/run paths through `FluxMq.App`.

Done when:

- Definitions can be validated and run from CLI and desktop host boundary.
- Missing factories, missing ports, and type mismatches are structured build errors.

### Phase 3 - Standalone Components And Dynamic Mapper

Status: Done

Goal: Clean up the component model so graph intent is visible.

Tasks:

- Replace vague side-effect naming with actor names.
- Keep `mqtt.trigger` as the live broker entry point and add explicit non-live source nodes: `session.source`, `generated.source`, `replay.source`.
- Add `mqtt.publisher`, `mqtt.recorder`, `file.writer`.
- Add runtime mapper/predicate/expression abstractions.
- Add `flow.mapper` as the user-facing mapper component.
- Add Dynamic Expresso and Jsonata mapper support.
- Use the package-backed `flow.mapper` runtime and keep request-shape coercion in FluxMQ's expression adapter; do not expose request-specific mapper node aliases.
- Update UI catalog/composer so actors are not auto-wired to envelope sources.

Done when:

- New UI catalog exposes Dynamic Mapper plus actors, not request pseudo-components.
- Runtime tests prove mapped publish and file-write flows.
- Full solution tests pass.

### Phase 4 - Mapper UI And Validator Hardening

Status: In progress

Goal: Make dynamic mapping and validation strong enough for real developer and ops workflows.

Tasks:

- Add `flow.mapper` JSONata workbench UI inspired by industrial integration tools:
  - input sample tree
  - editable Monaco JSON sample input
  - output request shape
  - single expression editor that returns the target request object
  - insert-field helpers
  - live JSON result/errors
  - saved configuration round-trip
  - Monaco editor theme sync with the app and a JSONata language definition
- Add `json.schema-validator` component.
- Define `JsonSchemaValidationResult` with valid/invalid status, errors, schema id/name, and original message context.
- Add schema configuration options:
  - inline schema JSON
  - schema file path
  - schema id/name
  - fail-open/fail-closed behavior where useful
- Add typed ports:
  - `Input: MqttEnvelope`
  - `Result: JsonSchemaValidationResult`
  - optional pass/fail envelope ports later if needed
  - `Errors: FlowError`
- Harden Jsonata mapping:
  - JSON payload variables
  - binary payload fallback behavior
  - consistent null handling
  - clear mapping errors
- Add node catalog entry and basic node editor support.

Done when:

- JSONata mapper configuration can be authored from the UI without raw JSON editing.
- Mapper preview runs against selected/recent/sample or manually edited `MqttEnvelope` input.
- JSON Schema Validator can be added from the UI, configured with inline schema or schema file path, and run through the runtime factory.
- Runtime tests cover valid JSON, invalid JSON, non-JSON payload, bad schema config, and continued processing after validation failure.
- UI catalog exposes JSON Schema Validator with clear ports.
- `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -m:1` passes.

### Phase 5 - Desktop Workspace Authoring Polish

Status: In progress

Goal: Make the visual workspace understandable and efficient for real flow editing.

Tasks:

- Finish source, mapper, actor, observer, and validator node editors.
- Improve diagram link/type feedback.
- Keep App JSON panel and diagram synchronized.
- Surface runtime build errors near nodes where possible.
- Preserve dense work-focused layout and avoid marketing-style pages.
- Fix known MudBlazor layout issues only when they affect current workflow.

Done when:

- A user can create a flow visually:
  `mqtt.trigger -> message filter -> dynamic mapper -> mqtt.publisher`
- The same flow validates/runs from the desktop and round-trips to JSON.
- UI tests and visual review pass.

### Phase 6 - Metrics, Assertions, And Scenarios

Status: In progress

Goal: Add the ops/QA layer on top of the same runtime primitives.

Tasks:

- Expand metrics beyond snapshot totals into rates, counters, summaries, and fault counts.
- Add assertion components with pass/fail result streams.
- Add expectation windows/timeouts.
- Add scenario runner for publish-and-expect workflows.
- Add result storage and report/export path.

Done when:

- A user can define: publish message X, expect response Y, validate payload schema Z.
- Scenario results are explainable and storable.
- Live, replayed, stored, and generated data can drive the same assertion logic.

### Phase 7 - Protocol Expansion And Extensibility

Status: Not started

Goal: Grow beyond MQTT without changing the graph model.

Tasks:

- Add HTTP sender/request actor.
- Add email sender actor.
- Investigate AMQP source/publisher.
- Define internal module contracts for component descriptors, configuration schema, node editors, and projections.
- Defer external plugin loading until internal modules prove the boundaries.

Done when:

- At least one non-MQTT actor works through the same mapper-to-actor pattern.
- New components can be added without editing every UI surface.

## Progress Log

### 2026-05-23

- Added [09-feature-list.md](09-feature-list.md) as the followable product and implementation backlog.
- Added this living development plan.
- Set the current target to `F-014 - JSON Schema Validator`, following the completed Dynamic Mapper and actor cleanup.
- Recorded the quality gate command for runtime slices:
  - `dotnet test FluxMq.sln --no-restore -p:UseSharedCompilation=false -m:1`
- Added [11-opc-router-ui-inspiration.md](11-opc-router-ui-inspiration.md) as an industrial ETL UX reference.
- Updated the current target to `F-015 - JSONata Mapper Workbench UI`, so the mapper editor gets the input-tree/output-shape/preview treatment before deeper ops components.
- Added the first `F-015` implementation slice:
  - `MqttEnvelopeExpressionContextFactory` now exposes `payloadJson` for JSONata payload mapping.
  - `DynamicMapperWorkbenchPreview` builds input variables, output request fields, and runtime preview results through the real mapper engines.
  - `DynamicMapperNodeWidget` now uses a three-pane workbench with input sample, expression editor, output shape, and preview/error feedback.
  - Added focused UI and component tests for payload JSON variables and mapper preview behavior.
- Corrected the mapper configuration model: `expression` returns the whole output object such as `MqttPublishRequest`, `FileWriteRequest`, or `MqttRecordingRequest`; saved definitions do not use per-field `map` shapes.
- Replaced the mapper expression textarea with BlazorMonaco/Monaco Editor so mapping expressions get a real code editor surface.
- Added FluxMQ Monaco themes and a JSONata language definition, so the mapper editor follows the app theme and highlights JSONata expressions directly.
- Reworked the mapper input sample into a Monaco JSON editor and the output preview into a read-only Monaco JSON result editor, matching the JSONata Exerciser mental model.
- Fixed mapper live preview state handling: parent node redraws no longer reload the draft expression, and editor/sample changes explicitly request a render after recomputing preview.
- Moved the active Phase 4 target to `F-014 - JSON Schema Validator`.
- Moved the active target to `F-022 - Actor Node Editors And Contract Hardening`.
- Added typed actor node models and editor widgets for `mqtt.publisher`, `mqtt.recorder`, and `file.writer`.
- `mqtt.publisher` now exposes its broker resource and input buffer in the designer while clearly showing that message topic, payload, QoS, and retain come from `MqttPublishRequest`.
- `mqtt.publisher` keeps broker selection in the editor but does not expose a `Connection` canvas port.
- `mqtt.recorder` and `file.writer` now show the command fields they consume and expose only input-buffer settings on the actor.
- The composer now writes default actor configuration when actors are added from the catalog.
- The runtime recorder factory now honors `boundedCapacity` from node configuration.
- Added typed source node models and editor widgets for `generated.source` and `replay.source`.
- Removed the separate live MQTT source path; live broker input remains `mqtt.connection` plus `mqtt.trigger`.
- `generated.source` now exposes its configured MQTT message list instead of a generic JSON editor.
- `replay.source` now exposes session selection, playback speed, and output buffer settings.
- Registered `replay.source` in the runtime factory registry and covered it with runtime tests.
- The composer now writes default configuration for generated and replay sources when they are added from the catalog; live flows continue to use the shared broker resource plus trigger.
- Moved the active target to `F-023 - Runtime Control And Diagnostics`.
- Validation errors now retain workflow, node, and port scope where possible.
- Runtime build errors and node startup failures now retain node scope.
- Workspace diagnostics now carry optional workflow/node/port metadata.
- Diagram nodes now render node-scoped diagnostics as border status plus a header tooltip.
- The desktop top bar now exposes active-app `Validate`, `Run`, and `Stop` actions and an app runtime state pill, instead of leaving validation/run paths hidden behind service methods.
- Added the first runtime logger slice:
  - `flow.logger` observes MQTT envelopes and `FlowError` inputs and emits structured `FlowLogEntry` values.
  - the runtime factory, component catalog, composer, and generic node icon now treat the logger as a first-class observer.
  - active project workspaces keep bounded log history and collect validation/run/stop diagnostics into that history.
  - the right inspector has a `Logs` tab with filtering and clear actions.
  - log rows now show explicit level labels, severity icons, source/code, scope, and context with level-specific formatting.
- Fixed runtime MQTT startup ordering so `mqtt.connection` awaits broker connection before `mqtt.trigger` subscriptions run; trigger logs should no longer show the disconnected-client race.
- Tightened the desktop multi-broker model:
  - app broker resources are tracked by resource name in the live workspace
  - `Run` connects configured broker resources before starting the runtime
  - the right-side Publish panel can target a selected broker
  - MQTT Trigger, Connection State Trigger, and MQTT Publisher editors select broker resources by app resource key with endpoint labels
- Fixed app-run broker ownership:
  - `Run` marks only the live broker clients it had to start
  - `Stop` disconnects those app-started clients and preserves manually connected broker clients
  - workspace stop has a bounded timeout so the top toolbar does not stay busy if runtime completion stalls
- Improved control feedback and publisher defaults:
  - `Stop` shows a compact spinner beside the `Stop` label during the async stop path
  - the right-side Publish panel defaults to the first broker resource from the active app
  - MQTT Publisher nodes default to the first app broker resource when added or when their editor opens with the generic fallback
- Removed the Live MQTT Trigger `Connection` canvas port; broker selection is handled through node configuration and the broker resource dropdown.
- Added the first visual link editing slice:
  - diagram-created workflow links now write back to the active workflow JSON
  - deleted workflow links remove their matching JSON reference
  - port compatibility now checks source/output direction and value type before accepting a new link
  - dynamic mapper output ports expose the configured result type, so actor inputs only accept the correct mapped request type
  - logger inputs that are meant to collect streams append links instead of replacing the existing reference
  - deleting a workflow node from the canvas now persists to JSON and removes downstream links to that node
  - MQTT Publisher now logs successful publishes and surfaces publish count/topic activity in the node
- Added runtime drain handling for unconnected data outputs:
  - unlinked transform/source outputs are connected to a discard target during runtime build
  - diagnostic `FlowError` outputs are not drained, preserving default workspace log collection
  - regression coverage proves both the generic runtime shape and an unlinked Payload Inspector output complete cleanly
- Started routeable decision outputs:
  - registered `flow.when` in the runtime factory registry
  - condition router nodes now get a default `qos >= 1` expression when added from the designer
  - `json.schema-validator` now emits `Result: JsonSchemaValidationResult`, `Valid: MqttEnvelope`, and `Invalid: MqttEnvelope`
  - UI catalog/designer ports expose those routeable branches for visual linking
  - repeated actor additions now use unique node names such as `publisher2` instead of replacing the existing actor
  - error ports are styled separately and ordered after normal input/output ports in node widgets
- Hardened routeable runtime semantics:
  - every runtime `OutputPort<T>` wraps its component source with a broadcast stream as a first-class port contract
  - one source output linked to inspector, metrics, router, and validator now reaches all targets instead of behaving like competing consumers
  - workspace runtime log collection attaches to any `FlowLogEntry` output port automatically
  - `flow.when` now emits route log entries for true and false decisions
  - MQTT trigger subscriptions now expose QoS in the editor and short-form subscriptions default to QoS 1
  - MQTT trigger subscription rows now include retained-message delivery and retain-flag preservation settings
  - broker live monitoring no longer derives its topic filters from pipeline triggers; it uses its own `#` monitor subscription per broker
- Started input-driven metrics polish:
  - `MqttMetricsSnapshot` now carries per-topic counts produced by the metrics component itself
  - the workspace collects each runtime metrics node's `Snapshots` output by workflow/node key
  - the MQTT Metrics node widget and node activity text render the node-local runtime snapshot instead of `Live.RecentMessages`
  - the old live-message metrics helper was removed so the UI path matches the standalone component contract
- Started input-driven payload inspector polish:
  - the workspace collects each runtime Payload Inspector node's `Output` stream by workflow/node key
  - Payload Inspector node activity now renders the node-local inspected message instead of the broker-wide latest message
- Started payload inspector JSON value-kind polish:
  - object payloads continue to display as `JSON`
  - arrays and scalar JSON literals display as `Array`, `Number`, `Boolean`, `String`, or `Null`
  - Payload inspection results still retain the parsed JSON value kind for runtime/editor use
- Started event-foundation support:
  - added a generic `FlowEvent` contract and shared event type names in the pipeline layer
  - MQTT trigger, MQTT publisher, MQTT recorder, file writer, JSON schema validator, and flow assertion now implement an event-source side-channel for important runtime facts
  - `ApplicationRuntime.Events` collects those side-channel streams outside the workflow graph; `Events` is not a normal component output port and should not appear in app definitions
  - component and runtime factory tests cover event emission and the app/trace boundary before any UI work is added
- Recorded app artifact direction:
  - apps contain separate pipelines, dashboards, and future tests/scenarios
  - dashboards should use their own grid designer, layout model, and monitoring component panel
  - assertions and expectations should move to the test/scenario or monitoring designer instead of the pipeline component palette
- Started app-artifact model support:
  - `ApplicationDefinition` now has `dashboards` and `tests` artifact collections in addition to `resources` and `workflows`
  - dashboard definitions include WPF-like row/column tracks, cells, spans, and named widgets
  - test/scenario definitions include named steps with type and configuration
  - JSON, validator, and configuration loader tests cover the new artifacts before any designer UI is added
- Started dashboard live widget support:
  - dashboard widgets are separate from pipeline components and are added from the dashboard tool panel
  - `event.counter` and `event.latest` read the runtime event stream from `ApplicationRuntime.Events`
  - widget definitions stay under `dashboards.widgets`, while cells only reference widget keys
  - dashboard widgets can be clicked into the selected/next cell or dragged directly onto a dashboard cell
  - Live mode renders the dashboard grid with a small overall padding and neutral app-border styling, leaving only widget cards as the main visible containers
- Started dashboard widget settings:
  - assigned dashboard cells now open a settings dialog for the widget title and runtime event filters
  - widget configuration updates are persisted under `dashboards.widgets.<widget>.configuration`
  - event counter and latest-event widgets now share the same title, event type, topic prefix, and status filter shape
  - filter controls are now event-type aware: topic, subject/path/name, and status options follow the selected event type
  - event filter metadata now lives in `DashboardEventFilterCatalog`, which drives both the settings dialog and runtime dashboard matching
  - `Any event` stays generic and does not show event-specific topic or subject filters
- Started scenario event expectation foundation:
  - added `ScenarioRunner`, scenario run/step result models, and a step-runner registry
  - added `expect.event` as the first scenario step runner over the external runtime event stream
  - event expectations can match event type, topic prefix, subject prefix, status, source, payload preview text, and string attributes
  - matched events advance an event offset so later expectations do not reuse the same event
  - unknown scenario step types fail with a clear step result instead of throwing through the runner
  - `ApplicationRuntime.Events` now broadcasts runtime events to multiple observers, allowing dashboards and scenarios to share one app run
  - verified with 66 pipeline tests and 392 full solution tests
- Started scenario host integration:
  - `FlowApplicationHost` keeps the loaded `ApplicationDefinition`
  - `RunScenarioAsync` runs a named test/scenario against the host runtime events
  - the host starts the app runtime if a scenario is requested before the app is running
  - missing scenarios fail early with a clear exception
  - app-host tests cover scenario execution and missing scenario names
- Started scenario action integration:
  - `ScenarioStepServices` passes typed runtime services into scenario step runners
  - `mqtt.publisher` publishes a configured MQTT message through a named runtime connection resource
  - MQTT scenario actions live in the app layer, while the pipeline runner stays generic
  - tests cover service passing, successful publish, and missing connection failure
  - scenario MQTT clients now come from `IMqttScenarioClientFactory`, which resolves shared app-level connection resources and creates runner-owned MQTT clients for both saved-definition and running-runtime paths
- Started CLI scenario execution:
  - added `fluxmq scenario --config <path> --name <scenario>`
  - scenario command uses `FlowApplicationHost.RunScenarioAsync`
  - text and JSON outputs include scenario and step status details
  - scenarios that run but fail return `ScenarioFailed`
  - CLI tests cover pass, fail, JSON, and missing scenario cases
- Started scenario visibility in the test tab:
  - test scenarios now expose ordered step snapshots to the workspace UI
  - the test tab renders loaded scenario steps as cards instead of a hardcoded empty placeholder
  - the test tab can run the active scenario and displays scenario/step result status after execution
  - step cards are rendered as an explicit sequence rather than unrelated blocks
  - test scenario steps can now be added, edited, and deleted from the test tab
  - the test tab now has a dedicated test-step palette with click and drag/drop support
  - expectation step editing uses event-type metadata so only relevant filter fields are visible
  - the test scenario row now fills the available tab height, with horizontal scrolling anchored at the bottom for long step sequences
  - `app1.json` has a sample `t1` MQTT round-trip scenario for manual testing
- Started scenario step ordering in the test tab:
  - test scenario cards can move earlier/later without changing step names or settings
  - moving a step updates the ordered `tests.<name>.steps` object and clears the previous run result
  - cards keep natural content height while the test surface fills the available tab height
- Started event-specific attribute filters:
  - the shared event filter catalog can now describe fields backed by `FlowEvent.Attributes`
  - JSON schema validation events expose a Schema id filter for dashboards and test expectations
  - scenario expectation editing writes attribute filters into the nested `attributes` step configuration
- Started scenario step catalog cleanup:
  - step type ids now have a shared scenario contract
  - the test-step palette, card labels, edit dialog title, and scenario step naming use one descriptor catalog
  - composer defaults now route through the descriptor model instead of repeating publish/expect strings
- Started scenario step field descriptors:
  - `mqtt.publisher` step fields now live in `ScenarioStepCatalog`
  - the publish step editor renders broker, topic, payload, encoding, QoS, and retain from field descriptors
  - scenario step selects that remain, such as broker/event/status, use ordinary MudBlazor `MudSelect`; dialogs no longer close on backdrop click
  - lower finite publish choices avoid popovers completely by using `MudToggleGroup`/`MudToggleItem` segmented toggles for Payload encoding and QoS
  - the publish step editor caches descriptor fields and option lists during initialization before rendering controls
  - composer defaults for new publish scenario steps use the same descriptor defaults as the editor
  - tests cover field order, select options, generated publish defaults, and missing field options
- Fixed dashboard widget cleanup:
  - dashboard cells now have a delete action for assigned widgets
  - deleting a widget removes the widget definition and clears the cell assignment
- Fixed the lower scenario-step control cleanup:
  - Payload encoding and QoS now use plain `MudToggleGroup`/`MudToggleItem` single-selection controls instead of dropdowns, removing the failing popover path from the lower half of the MQTT publish step dialog.
  - The scenario-specific dropdown popover CSS was removed from `app.css`; Broker, event type, and status use plain MudBlazor select behavior.
- Added scenario run history:
  - completed runs are kept newest-first in a bounded workspace history
  - switching active tests restores each test's latest result rather than leaking the previous test's status
  - the test scenario header now has a compact MudBlazor history menu for recent runs
- Added scenario report copy:
  - latest scenario run reports can be copied as stable JSON from the test scenario header
  - report formatting has both JSON and text paths for later file export or CLI alignment
- Added scenario report file export:
  - latest scenario run reports can be saved as JSON from the test scenario header
  - the existing MudBlazor save-as dialog is parameterized for report-specific title/helper text
  - report writing lives in `FlowWorkspaceService.SaveScenarioReportAsync`
- Added scenario history selection:
  - run history rows can restore an older run as the active displayed result
  - copy and save report actions target the restored historical run
  - service-level guards prevent selecting a run from another active test
- Added scenario report input capture:
  - report JSON now includes each step's saved configuration when the scenario snapshot is available
  - copied reports and saved files both include those inputs
  - text report output includes compact configuration lines for configured steps
- Added scenario report preview:
  - selected scenario reports can be inspected before copy/save
  - the preview dialog uses MudBlazor tabs for Summary and JSON without custom CSS
- Added scenario report preview copy actions:
  - the preview dialog can copy either the Summary or JSON report directly with MudBlazor action buttons
- Added scenario history active-run indicators:
  - the test scenario header shows whether the displayed result is the latest run or a selected historical run
  - history menu rows label the active row as Viewing, while report action tooltips follow the active run scope
- Added scenario history return-to-latest action:
  - when a historical run is active, the history menu includes `Show latest run`
  - choosing the active row no longer emits misleading restore feedback
- Added scenario report dialog metadata:
  - the report preview dialog title shows latest/history scope, status, finish timestamp, and duration for the selected run
- Added scenario report dialog save action:
  - the report preview dialog can initiate the same JSON save flow as the header save action
- Added scenario report step summary:
  - report JSON/text now includes aggregate step status counts, and the preview dialog shows a step-count chip
- Added scenario report first-issue summary:
  - non-passing reports now identify the first failed, timed-out, or canceled step in JSON/text and in the preview dialog
- Added scenario report matched-event offsets:
  - matched event JSON now includes offsets relative to the scenario start and matched step start
  - text report matched lines include those offsets for quick timeline diagnosis
- Added scenario report metadata:
  - report JSON now includes root `schemaVersion` and `generatedAt`
  - text reports start with a report metadata line before the scenario summary
- Added scenario report issue list:
  - non-passing reports now include all failed, timed-out, and canceled steps in a root `issues` array
  - text reports include an `Issues:` section before step details
- Added scenario report run identity:
  - report JSON now includes root `runId` derived from the scenario name and UTC run start time
  - text reports include the same run id in the report metadata line
  - re-exporting the same selected historical run keeps `runId` stable even when `generatedAt` changes
- Added scenario report snapshot consistency:
  - formatter overloads can render JSON and text from an already-created `ScenarioRunReport`
  - report preview creates one report snapshot, shows the run id in a MudBlazor chip, and saves the same JSON shown in the preview
  - `FlowWorkspaceService` can write pre-rendered scenario report JSON without regenerating metadata
- Added scenario report text export:
  - report preview can save the readable Summary tab as `.scenario-report.txt`
  - text export uses the same selected latest or historical report snapshot shown in the dialog
  - `FlowWorkspaceService` can write pre-rendered scenario report text
- Added scenario report text matched-event details:
  - Summary text now includes matched event source, subject, payload byte count, payload preview, and sorted attributes when present
  - key/value formatting is stable for both step configuration and event attributes
- Added scenario report not-run planned steps:
  - report JSON now includes root `notRunSteps` for planned scenario steps that did not execute
  - Summary text includes a `Not run:` section with planned sequence, step name, type, and configuration
- Added scenario report planned/executed step summary:
  - report JSON `stepSummary` now includes `planned`, `executed`, and `notRun` counts while preserving the existing executed `total`
  - Summary text and the MudBlazor report preview chip show planned-versus-run counts when a scenario stops early
- Added scenario report planned-step snapshot:
  - report JSON now includes root `plannedSteps` with planned sequence, step name, step type, and configuration
  - `notRunSteps` is derived from that same planned-step snapshot so exported plan/result data stays consistent
- Added scenario expectation timeout diagnostics:
  - timed-out `expect.event` messages now report whether any scenario/app events were observed while waiting
  - non-matching observed events are summarized so users can see whether they produced the wrong type/topic/status/payload
  - `mqtt.message.published` timeouts explain that the match can come from a scenario `mqtt.publisher` event or a running app MQTT publisher node event, and finished scenario runs do not keep listening
- Fixed scenario expectation event ordering:
  - scenario expectations now track consumed matched event indexes instead of advancing past every earlier non-matching event
  - this prevents a downstream publish event from being skipped when it reaches the journal before the receive event that a previous step was waiting for
  - identical later expectations still cannot reuse the same matched event
  - MQTT triggers accept their `mqtt.message.received` event before forwarding the envelope downstream, improving causal event order
  - `app1.json` `t1` is valid for this flow: the publish step sends `fluxmq/sample/request`, the app trigger receives it, the mapper/publisher emits `mqtt.message.published` on `test`
- Fixed scenario MQTT publish ownership and saved JSON step order:
  - `mqtt.publisher` test steps now publish through a separate short-lived scenario MQTT client cloned from the selected connection, so the test runner acts like an external publisher instead of reusing the running app MQTT client
  - this matters for `app1.json` because the app's own `pip1.trigger` must receive `fluxmq/sample/request`; a broad `pip2` subscription can otherwise make a receive expectation pass while masking that `pip1` never saw the test message
  - the UI workspace now builds the runtime from a raw `ApplicationDefinition` parsed with `System.Text.Json`, preserving saved JSON object order for test steps instead of round-tripping through `IConfiguration`
  - the regression shape for `t1` is: step 1 scenario client publishes `fluxmq/sample/request`, step 2 observes app `mqtt.message.received`, step 3 observes app `mqtt.message.published` on `test`
- Split test-runner execution from app runtime startup:
  - `Run test` no longer builds or starts the current app runtime as a side effect
  - the CLI `scenario` command uses the same isolated scenario-runner direction instead of host-bound runtime startup
  - publish-only scenarios can execute from the saved definition using the test runner's own MQTT client and connection profile resolver
  - host-bound `RunScenarioAsync` now requires an explicitly running host runtime
  - current `expect.event` still represents an app/runtime event stream; when using the local app as a target, run it explicitly first
  - next integration-test slice should move toward compiling scenario/test steps into a small normal pipeline that reuses existing runtime blocks such as `mqtt.publisher`, `mqtt.trigger`, condition/router behavior, and narrow test-specific blocks like `expect` and `when`; avoid a separate `mqtt.expect` broker-probe side channel
- Mirrored runtime events into workspace logs:
  - runtime `FlowEvent`s now appear in the Logs tab in addition to dashboards/tests
  - visible log rows include source, event type, workflow/node scope when known, topic, status, payload bytes, sorted attributes, and payload preview
  - this makes app-emitted `mqtt.message.received` and `mqtt.message.published` events visible even when the flow has no explicit logger node
- Added scoped workspace logs:
  - log rows now carry scope metadata for `App`, `Test runner`, and `System`
  - the app-level `Logs` tab can filter by scope, level, and search text using MudBlazor controls
  - the duplicate right inspector Logs tab was removed so log review has one clear home
- Isolated scenario runs from pre-run event replay:
  - each scenario event journal ignores runtime events with timestamps before the run started
  - this prevents retained/stale broadcast replay from being treated as evidence for a fresh test run
- Made MQTT delivery flags explicit in expectations:
  - MQTT received/published/recorded expectations now expose `QoS` and `Retain` filters
  - retain matching accepts config `true/false` and event `True/False`
  - expectation JSON attributes can use strings, booleans, or numbers, so `attributes: { "retain": false, "qos": 1 }` is valid
- Started normal-component scenario publish execution:
  - `mqtt.publisher` test steps now reuse `MqttPublisherComponent` instead of a duplicate scenario-only publisher service
  - scenario runs still use a short-lived test-runner MQTT client cloned from the selected app-level connection resource
  - app, CLI, and UI scenario services now share the same publish-step path through `IMqttScenarioClientFactory`
  - keep moving in this direction: normal MQTT publisher/trigger/condition components plus narrow test-specific `expect`/`when` blocks, with no special `mqtt.expect` side channel
- Cleaned pre-release compatibility aliases from the project:
  - canonical metrics nodes are `mqtt.metrics`; `app1.json` no longer uses `mqtt.metrics-sink`
  - direct request mapper node ids are not part of the runtime/UI surface; use `flow.mapper` plus actor nodes instead
  - request mapper component classes remain as internal typed mapper implementations for `flow.mapper`
- Applied remaining robustness follow-ups from the scenario/report review:
  - scenario event waits cancel their internal timeout delay after early match/completion/cancellation
  - runtime projection notification failures are contained inside the background refresh task
  - test-runner MQTT client ids keep the full GUID-based value
- Made `mqtt.connection-state-trigger` buildable from app-level connection resources:
  - it now uses `configuration.connection`, like the normal MQTT trigger/publisher resource reference direction
  - the UI no longer exposes a dangling graph `Connection` input port for that node
- Added a designer catalog/runtime guard test so catalog components cannot silently drift away from registered runtime factories again.
- Added a scenario step catalog/app-runner guard so test designer palette steps cannot silently drift away from executable scenario runners.
- Made scenario runner registries explicit: scenario tests use the event-expectation-only registry, while app/UI/CLI paths use the app default runner registry with both `expect.event` and `mqtt.publisher`.
- Added scenario step type validation so saved test definitions with unknown step types fail validation before scenario execution.
- Extended scenario step alignment guards so validation, app runners, and the UI palette must agree on supported step types.
- Added scenario-owned `mqtt.trigger`:
  - the test runner now starts a normal `MqttConnectionComponent` plus `MqttTriggerComponent` from an app-level connection resource
  - received trigger events are appended into the scenario journal for later `expect.event` steps
  - lifetime cleanup stops the trigger/client after passed or failed scenario runs
  - the UI test-step palette/editor can add and edit broker, topic filter, QoS, receive-retained, and retain-as-published settings
- Relaxed the CLI scenario guard for runner-owned event sources:
  - `expect.event` is allowed in CLI scenarios after a scenario-owned `mqtt.trigger`
  - expectations before any runner-owned event source still fail fast because the CLI is not attached to an external app runtime event stream
- Added `when.event` as the first narrow test-specific condition step:
  - it uses the same event filters/editor path as `expect.event`
  - a match passes and continues the scenario
  - a timeout marks the step `Skipped`, stops the remaining flat scenario steps, and keeps the run successful
  - scenario reports count skipped steps and do not treat skipped guards as issues
- Added scenario-owned publisher events to the runner journal:
  - `mqtt.publisher` test steps now append their normal `mqtt.message.published` component event
  - CLI scenarios can run `mqtt.publisher` followed by `expect.event` without attaching to an app runtime or adding a trigger
- Mirrored runner-owned scenario events into scoped workspace logs:
  - scenario-owned MQTT publisher/trigger events now appear in Logs with `Test runner` scope and `Test` artifact metadata
  - app/source events still stay on the existing app runtime event path, so dashboard counters are not incremented by isolated test-runner events
- Cleaned live MQTT fake-client naming in tests:
  - component, app-runtime, app-host, and UI workspace tests now use `mqttClient` for live fake MQTT clients
  - stored recording session terminology and `session.source` ids remain unchanged because those are persisted message sessions
- Tightened the CLI runner-owned event-source guard:
  - `when.event` now follows the same fast-fail rule as `expect.event` when it appears before any scenario-owned `mqtt.publisher` or `mqtt.trigger`
  - this prevents a CLI-only conditional guard from silently skipping because there is no event stream attached
  - CLI coverage now also confirms `mqtt.publisher` followed by `when.event` passes when the guard matches the runner-owned publish event
- Moved the event-source requirement into a shared scenario helper and applied it to isolated desktop test runs:
  - running a UI test with `when.event` or `expect.event` before any scenario-owned event source now fails immediately with a diagnostic if the app runtime is not running
  - isolated UI tests can still run `mqtt.publisher` followed by `when.event` because the publisher appends its normal event into the scenario journal
  - scenario setup failures in the desktop test runner stay scoped to scenario diagnostics/logs and no longer mark the app runtime state as faulted
  - skipped `when.event` guards remain successful, are shown as skipped, and stop later flat scenario steps in both CLI and desktop test paths
  - running against an explicitly started app runtime still lets event-observing steps watch app runtime events
  - scenario tests cover the helper directly so CLI/UI callers share one rule

The engine package migration has removed FluxMQ's local workflow runtime and definition copies. `FluxMq.App` now owns workspace validation and projects executable resources/workflows into `FluxFlow.Engine`; `FluxMq.Scenarios` owns the test/scenario runner primitives; normal runtime components stay in `FluxMq.Components`.
FluxMQ is now on `FluxFlow.Engine` `1.0.0` with consumed `FluxFlow.Components.*` packages aligned to the compatibility rebuild versions. Production workflows can use package-owned conditional links through per-link `when` expressions, while concrete expression engine implementations remain FluxMQ-owned adapters. README and durable docs describe the package-backed runtime boundary and the current active solution layout.

## Next Action

Working rule:

- Continue in small PR-backed slices from the V1 critical path.
- Keep memory current after each merged slice.
- For visual/runtime changes, state the exact manual check and expected result.
- Before staging, scan changed files for neutral naming and run the smallest useful tests plus the release-shaped validation command when packaging can be affected.

Latest readiness state:

1. Local command gate passed.
2. Windows package gate passed and produced portable zip plus MSI artifacts.
3. Durable docs cleanup passed for generated-traffic smoke commands, current CLI wording, and app-owned resources before package-runtime projection.
4. Packaged portable desktop smoke gate was launched and manually checked with no obvious release-blocking shell issue.
5. Candidate readiness recheck passed: `git diff --check`, full solution tests, and broker-free sample verification are green.

Latest release-readiness docs state:

1. `docs/release-readiness.md` defines the repeatable command, Windows package, manual UI, and blocker gates.
2. `docs/v1-candidate-notes.md` records the current candidate status, validated commands, local artifacts, manual testing focus, and repackage rules.
3. Docs-only and memory-only changes do not require repackaging unless they change release commands or candidate artifact expectations.

Next implementation slice: run focused candidate workflows from `docs/v1-candidate-notes.md` and fix only concrete blockers. Keep richer component edit views on the designer-polish track unless a candidate scenario proves one is blocking normal use.

Latest focused candidate workflow state: CLI validation passed for the repo samples and local `app1.json`; `generated-traffic-inspect.json` ran for a bounded duration; local `app1.json` ran against Mosquitto on `localhost:1883`; an external MQTT publish to `fluxmq/sample/request` produced the expected mapped publish on topic `test`. No runtime or broker-backed candidate blocker was found. Remaining candidate checks are desktop-visual checks for dashboard layout/widget refresh, desktop test artifact execution, and Logs filtering.

Latest slice: MQTT publish/trigger components now use `FluxFlow.Components.Mqtt` `0.2.2-alpha.1`, runtime `flow.mapper` execution now uses `FluxFlow.Components.Mapping` `0.1.1-alpha.1`, `json.schema-validator` now delegates schema loading/evaluation to `FluxFlow.Components.Validation` `0.1.1-alpha.1`, `file.writer` now delegates disk writes to `FluxFlow.Components.FileSystem` `0.4.1-alpha.1`, `flow.logger` now delegates neutral log-entry creation to `FluxFlow.Components.Observability` `0.1.1-alpha.1`, recording/stored replay now use `FluxFlow.Components.Sessions` `0.1.1-alpha.1` through `FluxMqSessionStore`, `mqtt.metrics` now delegates neutral count/size/group aggregation to `FluxFlow.Components.Metrics` `0.1.1-alpha.1`, `mqtt.payload-inspector` now delegates neutral payload classification to `FluxFlow.Components.Payloads` `0.1.1-alpha.1`, serialization transforms now use `FluxFlow.Components.Serialization` `0.1.1-alpha.1`, timer nodes now use `FluxFlow.Components.Timers` `0.4.2-alpha.1`, `state.reducer` now uses `FluxFlow.Components.State` `0.1.1-alpha.1`, and FluxMQ now registers package-backed `http.request` plus generic `payload.inspect` nodes. FluxMQ keeps app-level connection resources, `mqtt.publisher`/`mqtt.trigger`/`file.writer`/`flow.logger`/`mqtt.metrics`/`mqtt.payload-inspector` surface names, `MqttEnvelope` context variables, timer tick context variables, request-shape coercion, validation result projection, workspace log projection, product events, retained-message counts, idle metric-rate refresh, session persistence, Core/UI payload projection, HTTP request configuration, state reducer defaults, and transform catalog defaults through small adapters, while duplicated request-specific mapper, schema validation, file write, generic log-entry, local replay-source, generic metric aggregation, and payload classification internals have been removed from FluxMQ component execution.

Latest package alignment slice: FluxMQ now consumes `FluxFlow.Engine` `1.1.0`, `FluxFlow.Components.Designer` `1.0.1`, `FluxFlow.Components.Secrets`/`Storage`/`Storage.FileSystem` `1.1.0`, and the remaining consumed `FluxFlow.Components.*` packages at `1.2.0`. The MQTT publisher and trigger adapters were updated to consume package `Errors` outputs through normal Dataflow `LinkTo` sinks because the new package fanout source is no longer `IReceivableSourceBlock<FlowError>`. Restore, serial build, full solution tests, and FluxFlow outdated scan are green.

Latest candidate validation slice: the operations dashboard/test-studio sample now uses current metric resource ids (`message.count.windowed`, `event.rate`, `topic.count.windowed`) and focused dashboard widget ids (`status.value`, `qos.breakdown`), with the docs-site sample copy kept in sync. App tests now load and validate the repo-contained operations sample through the configuration loader. Local sample verification, docs-site build, Debug solution tests, Release/win-x64 solution tests, and Windows packaging are green; refreshed portable zip and MSI artifacts were produced. The remaining candidate gap is a focused manual packaged-desktop smoke pass against the refreshed package.

Latest Metrics tab UI polish: the Metrics editor is now a compact app workspace instead of a stacked form page. The editor uses one dense edit panel for identity/type/parameters, a narrow read-only details rail for live preview, references, and error-only validation surfaces. Always-visible helper copy and the success validation card were removed to reduce scroll and noise. The follow-up density pass shortened the command bar/editor header, gave the editor more desktop width, reduced metric row height and grid minimums, softened list pills, tightened panel gaps/input typography, and removed stale card-layout CSS. The `New metric` dialog now uses flat compact search/name controls, shorter type rows, a selected details pane, resource id preview, and default-parameter preview without changing the created metric schema. The delete flow now uses a compact custom confirmation dialog that shows the metric id plus dashboard/widget reference count and rows before the destructive action. Dirty-discard prompts use a compact Metrics confirmation dialog instead of plain message boxes. Rename is now an explicit clean-draft workflow: the main editor shows metric id as read-only metadata with one contextual rename action in the id row, hides rename while dirty, and uses a compact rename dialog that validates the new id while preserving existing dashboard-binding rename behavior. Duplicate is now explicit too: a compact dialog shows the source id, lets the user set the copy display name, previews the generated id, and then selects the copied metric. The selected type picker now carries the type id, unit, and parameter count inline, while type changes open a compact searchable picker with selected-type details, current-type marking, default-parameter preview, and one explicit reset action. Parameter editing now uses compact inspector controls with wider spans for topic/text fields, compact spans for short values, tooltip-only help, and inline min/max/required chips instead of full form helper text. The editor header now uses a flat title/action bar with resource id metadata, an unsaved-only status chip, native icon actions, and cancel/save actions only while draft changes exist. The read-only side rail now excludes the redundant type details card, uses quieter heading badges, a one-line idle live-preview status with no duplicate idle badge, reference count badges only when bindings exist, smaller live preview/reference rows, and softer separators while keeping the same latest-reading/reference data paths. The metric list now has tighter row rhythm, stable keyed rows, softer latest/reference indicators, and compact local empty/zero-result states with flat actions. The command bar now uses the same native flat control language for search, type filter, reset, and `New metric`, with tighter padding/gaps. Display name and description now use the same compact native inspector fields as the parameter editor, so the identity area no longer mixes heavier form styling into the flat editor. The id, display name, type, and description cells now share the same stretched compact grid rhythm. Validation is no longer shown as a full-width banner or header error pill plus side details; the side rail is the single detailed validation area, with the header pill reserved for normal saved/unsaved states. Invalid identity/type/parameter fields now get only subtle control/label tint plus `aria-invalid`, sharing validation helpers with the side-rail list, so there is no second visible error sentence. Side-rail validation rows now link to the affected compact editor field when a field target exists, linked targets get a flat outline after navigation, and invalid fields reference the existing side-rail message through `aria-errormessage`. Side-rail reference rows now separate widget and dashboard/location text, keep type/primary metadata quiet, include a small open-dashboard action that uses the existing workspace dashboard selection path after the dirty-draft prompt, use narrow-width rules that move metadata below the main line instead of forcing horizontal squeeze, and use the readable location line in hover/action wording; reference summaries also carry the dashboard cell id when available, so opening a reference lands on the referenced widget cell in the dashboard designer, and Metrics reference/delete rows show readable location text like `ops · Cell R1 C1` while retaining the raw cell id for navigation. Verification used temporary artifact output folders because the running desktop app can lock the normal UI output: UI build passed, focused UI tests passed with 413 tests, and whitespace checks are run per polish slice.

Latest workspace chrome polish continuation: after the Metrics tab workbench, the same compact flat treatment was applied to adjacent high-noise surfaces. The live inspector publish controls, component catalog metadata, and test scenario step metadata were de-cluttered without changing runtime behavior or saved app definitions. Test scenario step rows now use metadata naming instead of badge naming, and old pill-style metadata radius is covered by UI guard tests. The App JSON tab uses the existing read-only Monaco JSON viewer instead of a flat `<pre>` body while keeping the same generated full-definition JSON source and copy/metadata toolbar; `App JSON` is now a normal artifact tab in the workspace tab strip, clicking a regular artifact tab returns to the visual workspace, and non-critical Monaco/JS interop failures are contained so the workspace is not trapped in code view. The app structure toolbar now keeps the app identity to the app name only and does not repeat the active artifact/page label beside the selected tab. The active app identity and no-app state are now flat text/icon rather than bordered pills, app-structure menus are compact anchored non-modal dropdowns with no outer border or selected-button treatment instead of heavy modal-like popovers, and the breadcrumb/app/menu labels share one neutral typography style; global MudBlazor overlay styling is scoped to dialog scrims so menu/popover overlays stay transparent. The no-app startup/closed-app state now hides the app-scoped top command bar and bottom status bar entirely, leaving the centered `New app` / `Open file` actions on a full-height canvas until an app is active. The pipeline designer shell no longer draws a full green content-region focus border around the canvas; focus indication stays on specific controls and the tool panel. The latest slice passed the focused scenario/app-structure/workspace-shell chrome guards, the UI test project with 462 tests, the full solution test pass, and remote Windows validation. Next implementation slice: keep scanning high-use workspace panels for duplicated status, badge-heavy metadata, and disabled no-op controls; polish only the elements that reduce workflow noise without broadening scope or changing app/runtime schemas.

Latest Topics tab broker-monitor slice: the Topics explorer column is wider, the tab auto-starts active-app MQTT broker monitors independent of app runtime state, and the workspace monitor subscription is the app-wide `#` filter. Live messages are stamped with their managed broker/resource name before projection and recording, stored messages preserve that optional broker label, and the Topics tree groups messages by broker with broker state/count while the latest-state and history panes include broker context. Verification passed focused Topics/live-service guards, focused storage round-trip, live MQTT workspace service tests, storage-focused component tests, and the full UI test project with 466 tests.

Current Topics direction: keep `Topics` as the app-owned tab, but make that tab behave like a full MQTT Explorer for the app's configured MQTT brokers. Opening the tab should create separate monitor clients for each app broker, subscribe those monitor clients to `#` plus `$SYS/#`, and keep that monitoring independent from app runtime state without adding a separate no-app explorer surface. The broker-scoped publish composer is now in the existing `Topics` detail area and targets app MQTT broker resources only, filtering out internal `topics:` monitor clients while reusing the existing live publish and manual-publish recording paths. Latest and selected history payload detail now have local `Formatted`, `Raw`, `Hex`, and `Meta` views plus copy actions for the visible view, using the existing payload inspection output. The selected history detail also has a selected-only `Diff` view that compares the selected row against the current latest message for the active broker/topic scope. The lower history grid now has compact displayed-field text filtering, QoS filtering, retain-state filtering, reset, and visible-row JSON export through the app's Save As pattern. The publish composer now has text-only `Use latest` / `Use selected` assists plus a component-local recent publish list with load/clear actions. The Topics detail area now has a compact stats strip for the active broker/topic scope; stats use scoped `HistoryMessages` and stay independent from lower-history filters. Next implementation slice: choose the next small designer-polish backlog item from remaining high-use workspace noise.

Latest Topics publish-composer slice: the existing app-owned `Topics` tab now includes a compact flat publish composer between latest state and history. It syncs app MQTT broker resources into the live workspace, filters out internal `topics:` monitor clients from publish choices, preselects the publish broker/topic from the selected broker/topic when there is a clean match, falls back to the first app broker, and keeps the topic editable. Publishing reuses `LiveMqttWorkspaceService.PublishAsync`, connects the selected app broker when needed, and records successful manual publishes through the active project without changing runtime components, saved app schema, explorer schema, storage format, node ids, ports, or contracts. Verification passed the focused UI build and focused Topics/live-service/monitor-resolver tests; desktop manual verification is not claimed because native desktop automation was not reauthorized. Next implementation slice: continue with richer selected-topic payload/history controls inside the existing `Topics` tab.

Latest Topics payload-controls slice: the existing app-owned `Topics` tab now gives both the latest message and selected history detail local payload views for formatted, raw, hex, and metadata output, plus compact copy actions for the currently visible view through the existing clipboard/snackbar feedback pattern. The broker tree, latest metadata rail, publish composer, virtualized history grid, row selection, selected-message detail, monitor behavior, publish behavior, saved app schema, explorer schema, storage format, runtime components, node ids, ports, and contracts are unchanged. Verification passed the focused UI build and focused `TopicExplorerPanel` guard; desktop manual verification is not claimed because native desktop automation was not reauthorized. Next implementation slice: continue with lower-history filtering/export, payload diffing, publish templates/history, topic stats, or the next small designer-polish backlog item.

Latest Topics history-controls slice: the existing app-owned `Topics` tab lower history area now has compact displayed-field text filtering, QoS filtering, retain-state filtering, reset, and visible-row JSON export through the existing Save As dialog. The latest panel remains broker/topic scoped while the selected history detail follows the visible lower grid. Export includes broker, topic, timestamp, QoS, retain, payload byte count, payload type, base64 payload, optional text payload, and hex dump without changing monitor behavior, publish behavior, saved app schema, explorer schema, storage format, runtime components, node ids, ports, or contracts. Verification passed the focused UI build and focused `TopicExplorerPanel` guard; desktop manual verification is not claimed because native desktop automation was not reauthorized. Next implementation slice: continue with payload diffing, publish templates/history, topic stats, or the next small designer-polish backlog item.

Latest Topics payload-diff slice: the existing app-owned `Topics` tab selected history payload controls now include a selected-only `Diff` view. It compares the selected history message against the current latest message for the active broker/topic scope, shows compact latest-row and unchanged-payload states, emits a bounded unified text diff for text payloads, and uses metadata plus first differing byte offset for binary/non-text payloads. The selected payload copy action copies the visible diff text when `Diff` is active. Latest payload controls remain formatted/raw/hex/meta only, and broker tree, latest panel, publish composer, history filters/export, virtualized grid, monitor behavior, publish behavior, saved app schema, explorer schema, storage format, runtime components, node ids, ports, and contracts are unchanged. Verification passed the focused UI build and focused `TopicExplorerPanel` guard; desktop manual verification is not claimed because native desktop automation was not reauthorized. Next implementation slice: continue with publish templates/history, topic stats, or the next small designer-polish backlog item.

Latest Topics publish-presets slice: the existing app-owned `Topics` tab publish composer now has compact text-only `Use latest` and `Use selected` actions that load topic, payload, QoS, retain, and a clean matching app broker from the current message context without coercing binary payloads into text. Successful composer publishes are kept in a bounded component-local recent list with load and clear actions. Broker targeting, monitor-client filtering, `Live.ConnectAsync`, `Live.PublishAsync`, manual publish recording, selected broker/topic prefill, saved app schema, explorer schema, storage format, runtime components, node ids, ports, and contracts are unchanged. Verification passed the focused UI build and focused `TopicExplorerPanel` guard; desktop manual verification is not claimed because native desktop automation was not reauthorized. Next implementation slice: continue with topic stats or the next small designer-polish backlog item.

Latest Topics stats-panel slice: the existing app-owned `Topics` tab now has a compact stats strip for the active broker/topic scope. It derives message count, unique topic count, retained count, total payload bytes, average payload bytes, QoS 0/1/2 counts, and latest received time from scoped `HistoryMessages`, while lower-history filters continue to affect only `VisibleHistoryMessages` and selected detail. The implementation stays private to `TopicExplorerPanel` with no service, metric resource, saved field, runtime wiring, schema, explorer storage, node id, port, contract, monitor, publish, or MQTT semantics changes. Verification passed the focused UI build and focused `TopicExplorerPanel` guard; desktop manual verification is not claimed because native desktop automation was not reauthorized. Next implementation slice: choose the next small designer-polish backlog item from remaining high-use workspace noise.

Latest workspace log-actions slice: the first-level `Logs` tab now has compact copy and JSON export actions for the currently visible rows after scope, level, search, fixed-scope, and max-entry filtering. Export uses the existing Save As pattern and writes timestamp, severity, scope, artifact, workflow, source, code, node, port, message, and context fields without changing runtime logging behavior, log collection semantics, saved app schema, storage model, component contracts, node ids, ports, or workspace routing. Verification passed the focused UI build and focused `WorkspaceLogPanel` guard; desktop manual verification is not claimed because native desktop automation was not reauthorized. Next implementation slice: choose the next small designer-polish backlog item from remaining high-use workspace noise.

Latest workspace setup-dialog slice: the Add Connection and Start Recording dialogs no longer render hidden/explicit readiness status chrome. Add Connection keeps the same broker/client/keep-alive/TLS/certificate picker/clean-start controls, validation, and result projection; Start Recording keeps project autocomplete, session name defaulting, Enter handling, project summary, and blank project/session normalization. Runtime behavior, MQTT connection behavior, recording behavior, saved app schema, storage model, monitor semantics, services, workspace routing, node ids, ports, and contracts are unchanged. Verification passed the focused UI build and focused Add Connection/Start Recording dialog guards; desktop manual verification is not claimed because native desktop automation was not reauthorized. Next implementation slice: continue the remaining high-use workspace noise cleanup, with broader creation/save dialog status chrome as a separate small slice if selected.

Latest remaining setup-dialog slice: New App, New Pipeline, Save As, Metric Create, Metric Rename, Metric Duplicate, and Metric Type Change no longer render hidden/explicit readiness status chrome. Metric type/search counts, validation/error rows, destructive metric confirm/delete tone status, runtime/dashboard/node/scenario statuses, app creation, pipeline creation, save path handling, metric validation/id generation/default previews/dashboard binding behavior, and metric type reset warnings are unchanged. Verification passed the focused UI build and focused setup/metric dialog guards; desktop manual verification is not claimed because native desktop automation was not reauthorized. Next implementation slice: continue scanning remaining high-use workspace noise without broadening runtime or schema scope.

Current local design-system branch state: work is on `work/pipeline-node-ui-system`, focused on Pipeline node view/edit UI consistency and adjacent designer polish rather than runtime behavior. The current accepted direction is to finish complex editor nodes first, using useful view-mode facts and stable full-height code-editor workspaces where the node authoring task is code-heavy. Dynamic Mapper, JSON Schema Validator, State Reducer, Flow Assertion, Message Filter, Condition Router, Routing Switch, Routing Correlation, Routing Join, Routing Fork, Routing Merge, Routing Window, MQTT Trigger, Connection State Trigger, Generated Source, Replay Source, Stored Session Source, Timer nodes, MQTT Publisher, MQTT Recorder, File Writer, Metric Source, MQTT Metrics, HTTP Request, Payload Inspect, legacy MQTT Payload Inspector, and the generic/fallback widgets have been brought onto this direction, with local commits through `Polish generic node editors`. Flow Assertion kept runtime behavior and saved configuration unchanged, passed focused UI build/test, and visual review confirmed the left expression workspace plus compact right sidecar layout. Message Filter keeps topic-pattern validation and blank-condition behavior unchanged while moving the optional condition into a full-height code-editor workspace and the topic patterns into a compact sidecar; focused UI build/test passed, and the desktop app opened a temporary filter project though that session could not drive the WebView settings button reliably enough to complete the edit-dialog manual path. Condition Router keeps input-type normalization, type-change default expression reset, blank-condition validation, branch ports, and saved configuration unchanged while moving the route condition into a full-height code-editor workspace and sidecar controls. Routing Switch keeps expression validation, route validation, duplicate match-key validation, route parsing, ports, and saved configuration unchanged while moving the route expression into a full-height code-editor workspace and all support controls into a compact sidecar. Routing Correlation and Join keep required-expression validation, side-name validation, normalization, ports, and saved configuration unchanged while moving paired matching expressions into full-height code-editor workspaces and support controls into compact sidecars. Routing Fork, Merge, and Window keep port/boundary editing, validation, ports, and saved configuration unchanged while moving support controls into compact sidecars; focused UI build/tests passed, while desktop verification is not claimed because native desktop automation was stopped with Escape during file-open setup. Source and trigger editors now omit view-mode output-field contract rows, keep compact operational facts/previews only, and keep configuration-heavy editors flat; MQTT Trigger and Generated Source use primary table workspaces plus compact support sidecars, while replay, stored session, connection-state, and timer editors remain dense flat forms. Source/trigger validation, saved configuration shape, node ids, ports, runtime semantics, and normalization behavior are unchanged; focused UI build/test passed, and desktop verification is not claimed because native desktop automation was not reauthorized. Actor/sink editors now omit view-mode request-field contract rows and keep compact broker/target plus input-buffer facts only; MQTT Publisher broker sync, Recorder/File Writer buffer editing, saved configuration shape, node ids, ports, request contracts, runtime behavior, and normalization are unchanged; focused UI build/test passed, and desktop verification is not claimed because native desktop automation was not reauthorized. Metric Source now omits its view-mode output-field contract row while keeping selected metric, latest value, start mode, output buffer, and parameter preview facts; MQTT Metrics keeps rate/readout/topic/last-topic operational summaries without contract clutter. Metric validation, saved configuration shape, node ids, ports, resources, snapshots, runtime behavior, and normalization are unchanged; focused UI build/test passed, and desktop verification is not claimed because native desktop automation was not reauthorized. Generic/fallback node polish removes field-dump rows from HTTP and payload widgets, keeps fallback port details neutral, preserves registry/runtime/schema behavior, and passed the focused UI build/test; desktop verification is not claimed because native desktop automation was not reauthorized. Topics publish composer polish adds broker-scoped publish controls inside the existing app-owned `Topics` tab, filters out internal monitor clients, reuses existing publish/recording services, preserves runtime/schema behavior, and passed focused build/tests; desktop verification is not claimed because native desktop automation was not reauthorized. Topics payload-controls polish adds local formatted/raw/hex/meta payload views and visible-view copy actions for latest and selected history messages while preserving monitor/publish/schema/runtime behavior; focused build/test passed, and desktop verification is not claimed because native desktop automation was not reauthorized. Topics history-controls polish adds lower-grid filtering and visible-row JSON export while preserving monitor/publish/schema/runtime behavior; focused build/test passed, and desktop verification is not claimed because native desktop automation was not reauthorized. Topics payload-diff polish adds a selected-only diff view against the current latest broker/topic message, with text/binary compact states and visible diff copy, while preserving monitor/publish/schema/runtime behavior; focused build/test passed, and desktop verification is not claimed because native desktop automation was not reauthorized. Topics publish-presets polish adds text-only latest/selected message reuse plus a component-local recent publish list while preserving monitor/publish/schema/runtime behavior; focused build/test passed, and desktop verification is not claimed because native desktop automation was not reauthorized. Topics stats-panel polish adds scoped message/topic/retain/payload/QoS/latest facts from `HistoryMessages` while preserving monitor/publish/schema/runtime behavior; focused build/test passed, and desktop verification is not claimed because native desktop automation was not reauthorized. Workspace log-actions polish adds visible-row copy plus JSON export to the first-level Logs tab while preserving runtime/schema/log collection behavior; focused build/test passed, and desktop verification is not claimed because native desktop automation was not reauthorized. Next implementation slice: choose the next small designer-polish backlog item from remaining high-use workspace noise.

Latest scenario step editor dialog slice: `ScenarioStepEditorDialog` no longer renders hidden or explicit readiness chrome; the compact validation row appears only when validation issues exist, while the `Apply` disabled state remains driven by `HasValidationIssues`. MQTT publish, MQTT trigger, event, and generic step field bindings, normalization, saved scenario definitions, runtime behavior, services, node ids, ports, and contracts are unchanged. Verification passed the focused UI build and focused `ScenarioStepEditorDialog` guard; desktop manual verification is not claimed because native desktop automation was not reauthorized. Next implementation slice: continue scanning remaining high-use workspace noise, keeping operational run/report/dashboard statuses out of scope unless a concrete polish slice selects them.

Latest Test Studio empty-state slice: the Test Scenario Designer and Test Runner Console no longer show no-test readiness cues, and the first-run runner strip now uses neutral first-run facts plus a runnable class/title instead of readiness wording. Scenario runner behavior, saved scenario definitions, runtime events/logs, report generation, dashboard state, services, schemas, node ids, ports, and contracts are unchanged. Verification passed the focused UI build and focused `TestRunnerConsole` / `TestScenarioDesigner` guards; desktop manual verification is not claimed because native desktop automation was not reauthorized. Next implementation slice: continue scanning high-use workspace noise, with component catalog use-state and report export wording as possible candidates while operational statuses remain out of scope.

Latest component catalog use-state slice: `ComponentCatalogPanel` now uses neutral `Available` / `catalog-use-state available` wording for the active artifact catalog instead of the prior state wording, while inactive-tab wording, `CanUseItem`, catalog contents, drag/drop affordances, node/widget/step definitions, saved schemas, runtime behavior, services, ids, ports, and contracts are unchanged. Verification passed the focused UI build and focused `ComponentCatalogPanel` guards; desktop manual verification is not claimed because native desktop automation was not reauthorized. Next implementation slice: continue scanning high-use workspace noise, with scenario report export wording and App Tree/Menu first-run labels as possible candidates while operational statuses remain out of scope.

Latest app navigation test-metadata slice: App Tree and App Structure menu test artifact metadata now uses neutral `No run yet` copy before a scenario has run. Latest-run lookup, run status classes, timestamps, issue counts, aria labels, no-history state, selection, delete, keyboard behavior, scenario runner behavior, saved scenario definitions, runtime events/logs, dashboard state, services, schemas, ids, ports, and contracts are unchanged. Verification passed the focused UI build and focused `AppTreePanel` / `AppStructureMenu` guards; desktop manual verification is not claimed because native desktop automation was not reauthorized. Next implementation slice: continue scanning high-use workspace noise, with scenario report export wording as a possible candidate while operational statuses remain out of scope.

Latest scenario report actions slice: `ScenarioRunReportDialog` no longer shows an explicit export status strip or format-count action label; the compact Summary/JSON copy and save actions, disabled-state binding, run facts, preview tabs, empty report states, file export payloads, formatter output, scenario runner behavior, saved scenario definitions, runtime events/logs, dashboard state, services, schemas, ids, ports, and contracts are unchanged. Verification passed the focused UI build and focused `ScenarioRunReportDialog` guard; desktop manual verification is not claimed because native desktop automation was not reauthorized. Next implementation slice: continue scanning high-use workspace noise without broadening runtime or schema scope.

Latest dashboard inspector chrome slice: `DashboardInspector` no longer shows the header immediate-edit status chip, and a selected empty cell now uses neutral `Empty cell` copy. Inspector title, selection facts, group/property counts, widget reset, property groups, cell style editing, widget editing, metric binding rows, dashboard layout behavior, saved dashboard schema, runtime events, services, ids, ports, and contracts are unchanged. Verification passed the focused UI build and focused `DashboardInspector` guard; desktop manual verification is not claimed because native desktop automation was not reauthorized. Next implementation slice: continue scanning high-use workspace noise without broadening runtime or schema scope.

Latest Metrics empty-state slice: the app-level Metrics editor now uses neutral `No metrics yet` copy when no metric resources exist, matching the list empty state. Existing no-selection text, create/edit behavior, validation, dashboard bindings, saved metric resources, runtime readings, services, ids, ports, and contracts are unchanged. Verification passed the focused UI build and focused `MetricDesigner` guard; desktop manual verification is not claimed because native desktop automation was not reauthorized. Next implementation slice: continue scanning high-use workspace noise without broadening runtime or schema scope.
