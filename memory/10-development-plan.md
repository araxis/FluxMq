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

**Phase:** 6 - metrics, assertions, and scenarios
**Active feature:** `F-031 - Assertions And Expectations`
**Status:** In progress
**Started:** 2026-05-24

The mapper workbench now follows a JSONata Exerciser-like shape: Monaco JSON input on the left, Monaco mapper expression in the middle, and live JSON result on the right. MQTT envelope samples use `{ topic, qos, retain, receivedAt, payload }`; arbitrary JSON input is treated as payload for quick experimentation. Mapper output configuration now separates the runtime target type from the result contract: `typed`, `any`, or `json-schema-file`. `json.schema-validator` exists as a standalone runtime/UI component backed by JsonSchema.Net, so schema validation is a reusable runtime capability rather than mapper-only UI behavior.

Actor editors are in place for `mqtt.publisher`, `mqtt.recorder`, and `file.writer`. Source editors are in place for `generated.source` and `replay.source`. Live broker traffic stays on the existing `mqtt.connection` plus `mqtt.trigger` path; `generated.source` owns its fixed MQTT message list; `replay.source` selects a recorded session and playback speed.

The current diagnostics slice attaches validation, runtime build, and node startup failures to the responsible node where possible. Diagram nodes now show error/warning/info status in their border and header tooltip while the existing workspace diagnostic list remains available for global errors.
The desktop shell now exposes active-app `Validate`, `Run`, and `Stop` actions in the top bar, plus an app runtime state pill, so validation/run feedback is separate from live broker connection state.
The current logging slice keeps diagrams clean by collecting all runtime component `FlowError` outputs into the workspace `Logs` tab by default, without adding hidden definition nodes or visible links. `Flow Logger` remains an explicit observer component for flows that need log entries as stream data. Runtime linking now keeps multi-source input ports open until every linked source completes, so explicit multi-input observers still behave correctly.

The current visual-link slice makes the diagram a real editor for workflow links. Dragging a compatible output port to an input port writes the target port reference into the active workflow JSON, deleting a rendered workflow link removes that reference, and dynamic mapper output ports use their configured result type for type feedback. Plain target inputs are rerouted on new connections; `Flow Logger` keeps append-style behavior for message/log collection inputs.
Deleting a workflow node from the designer now removes it from the active workflow JSON and cleans downstream references to that node, so deleted components do not return after the next add/rebuild action.
MQTT Publisher now emits successful publish log entries to the workspace `Logs` tab by default and updates node activity with published count plus the last topic. This keeps actor execution observable without wiring every actor to an explicit logger block.
Runtime linking now drains unconnected non-diagnostic outputs automatically, so observer/mapper/result outputs left unwired do not block runtime completion or cause Stop to time out. Diagnostic `FlowError` outputs stay available for the workspace `Logs` tab.
The current routeable-decision slice makes decision nodes executable and branchable: `mqtt.condition-router` is registered in the runtime, and `json.schema-validator` exposes `Result`, `Valid`, and `Invalid` outputs so validation details and envelope branches are distinct.
Runtime `OutputPort<T>` now enforces the first-class broadcast contract for every component output. Component internals may use buffers, transforms, or action blocks, but every runtime-facing output is presented as a broadcast stream, so one output wired to multiple inputs delivers the same message to each target.
Runtime log collection now subscribes to all `FlowLogEntry` output ports by default, not just hand-picked logger/publisher components. `mqtt.condition-router` emits route entries for `WhenTrue` and `WhenFalse`, so a false branch with no downstream wire is still visible in the workspace Logs tab while testing.
MQTT trigger subscription QoS is now explicit in the node editor. Short-form subscriptions such as `"#"` default to QoS 1, which keeps the default router expression `qos >= 1` useful during live broker tests while still allowing QoS 0 or QoS 2 per subscription.
Pipeline MQTT triggers are now treated as configured subscriber blocks: each row owns topic filter, QoS, retained-message delivery, and retain-flag preservation. Broker-wide live monitoring is separate from pipeline design and auto-subscribes each broker monitor to `#,$SYS/#` for topic tree, counts, rates, and payload inspection.
The metrics slice aligned the UI with the runtime component contract. `mqtt.metrics` observes only its `Input` stream, and the metrics node face now renders snapshots produced by that node's runtime `Snapshots` output rather than broker-wide live monitor messages.
The payload inspector projection slice applies the same rule to `mqtt.payload-inspector`: broker-wide payload inspection stays in the Live Inspector panel, while the diagram node activity renders the latest inspection produced by that node's own runtime `Output` stream.
The current payload type slice improves inspection semantics: MQTT payloads remain bytes, but UTF-8 payloads that decode to common literal values now expose human-facing payload kinds so scalar payloads such as `1`, `true`, `"text"`, `null`, and `[1,2]` are not mislabeled as plain text or JSON objects.
The current trigger activity slice applies the same input-driven rule to `mqtt.trigger`: the node card activity should count envelopes emitted by that trigger's configured subscriptions, while broker-wide message counts remain in the Live Inspector panel.
The current metrics-rate slice adds both current and since-start message-per-second data to `mqtt.metrics` snapshots. Current rate uses a configurable rolling window; average rate uses all messages observed since the metrics component started. Both rates are based on the stream feeding the metrics component, not broker-wide traffic. The metrics card display is configurable per node with selectable cards such as messages, current rate, average rate, payload, topics, retained count, or average payload size; card columns control the node grid, and rows are calculated from the selected-card count.
Future metrics refactor note: the current implementation remains MQTT-envelope-specific, which is acceptable for this slice. Longer term, split generic stream metrics from MQTT-specific topic metrics so outputs from dynamic mappers and future protocol components can feed metrics observers without meaningless topic fields.
The current assertion slice starts `F-031` with a standalone `flow.assertion` component. It evaluates a configurable expression over a configured input type, emits a typed `FlowAssertionResult`, routes the original value to `Passed` or `Failed`, and emits a log entry for each evaluation so pass/fail behavior is visible in the workspace Logs tab without extra wiring. It defaults to `MqttEnvelope` because current source nodes commonly emit MQTT envelopes, but the component is not MQTT-specific.
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
The current scoped-log slice gives logs a clearer observability boundary. Workspace logs now have explicit `Scope`, `ArtifactKind`, and `ArtifactName` metadata so app runtime rows, test-runner rows, and system/validation rows can be filtered without parsing message text. A first-level Logs tab uses a MudBlazor-native `WorkspaceLogPanel` with compact scope, level, and search filters; the duplicate right-inspector Logs tab was removed. This is still one in-memory project log stream, but it is ready for later per-runtime sinks such as service-hosted app logs, dashboard logs, and external integration-test runner logs without mixing ownership.
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
The current scenario event-field catalog slice makes `when.event` and `expect.event` catalog-described test steps. Event filter keys, attribute filter keys, and default values now live in `ScenarioStepCatalog`, and `FlowDefinitionComposer` uses the catalog for default configuration across all known scenario step types. The existing MudBlazor editor behavior stays the same, but future test-specific blocks have a cleaner path for field metadata.

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
- Keep request-specific mapper classes internal to `flow.mapper`; do not expose saved-definition aliases.
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
  - registered `mqtt.condition-router` in the runtime factory registry
  - condition router nodes now get a default `qos >= 1` expression when added from the designer
  - `json.schema-validator` now emits `Result: JsonSchemaValidationResult`, `Valid: MqttEnvelope`, and `Invalid: MqttEnvelope`
  - UI catalog/designer ports expose those routeable branches for visual linking
  - repeated actor additions now use unique node names such as `publisher2` instead of replacing the existing actor
  - error ports are styled separately and ordered after normal input/output ports in node widgets
- Hardened routeable runtime semantics:
  - every runtime `OutputPort<T>` wraps its component source with a broadcast stream as a first-class port contract
  - one source output linked to inspector, metrics, router, and validator now reaches all targets instead of behaving like competing consumers
  - workspace runtime log collection attaches to any `FlowLogEntry` output port automatically
  - `mqtt.condition-router` now emits route log entries for true and false decisions
  - MQTT trigger subscriptions now expose QoS in the editor and short-form subscriptions default to QoS 1
  - MQTT trigger subscription rows now include retained-message delivery and retain-flag preservation settings
  - broker live monitoring no longer derives its topic filters from pipeline triggers; it uses its own `#,$SYS/#` monitor subscription per broker
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
FluxMQ is now on `FluxFlow.Engine` `0.5.0-alpha.1`, so production workflows can use package-owned conditional links through per-link `when` expressions. README and durable docs describe the package-backed runtime boundary and the current active solution layout.

## Next Action

Working rule:

- After each fix or feature slice, update the progress/task notes before handing it back for review.
- Tell the reviewer exactly what to check in the running app when visual or runtime confirmation matters.
- Wait for confirmation before commit, push, PR, or merge steps.
- For every review or verification step, write both what we are going to do and the expected result.

Review the conditional-link designer editor slice:

1. Do: select a workflow link in the pipeline designer. Expected: a compact `When` field appears next to the canvas toolbar.
2. Do: enter a condition such as `input.Topic.StartsWith("factory/")` and apply it. Expected: the link turns into a conditional link, is highlighted, and the JSON stores the condition on that link only.
3. Do: select the link again and clear the condition. Expected: the link returns to a normal unconditional link without changing unrelated links.
4. Do: run the UI tests for this slice. Expected: focused composer tests pass with 73 tests, and the UI test project passes with 204 tests.

Next implementation slice: continue scenario/test composition around normal components plus narrow test-specific `expect.event`/`when.event` blocks. Prefer catalog/runner/shared-service changes over adding one-off logic inside Razor components.

Latest slice: neutral scenario step field metadata types now live in `FluxMq.Scenarios`, leaving UI-only descriptor pieces such as icons and editor kind in the UI layer. Keep moving descriptor defaults, options, summaries, and validation into shared catalog/services so test-step UI can stay thin and portable.
