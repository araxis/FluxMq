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
**Active feature:** `F-033 - Input-driven Trigger Activity`
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

- Implement connection profiles and MQTT session lifecycle.
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
- Keep request-specific mapper aliases hidden for older definitions only.
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

Status: Not started

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
- Corrected the mapper configuration model: `expression` is now the primary shape and returns the whole output object such as `MqttPublishRequest`, `FileWriteRequest`, or `MqttRecordingRequest`; per-field `map` is only a runtime compatibility fallback.
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
  - `Run` marks only the live broker sessions it had to start
  - `Stop` disconnects those app-started sessions and preserves manually connected broker sessions
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

## Next Action

Review the Payload Inspector value-kind slice:

1. Open `C:\Users\meisa\OneDrive\Documents\FluxMQ\app1.json`.
2. Run the app and publish payloads `1`, `true`, `"hello"`, `null`, `{"x":1}`, `[1,2]`, and `plain text`.
3. Confirm inspector labels show `Number`, `Boolean`, `String`, `Null`, `JSON`, `Array`, and `Text`.
4. Confirm JSON formatted/raw/hex/meta tabs still behave normally.
5. Confirm payload inspector node activity and the publish panel payload meta use the same value-kind labels.
6. After confirmation, commit this slice and open a PR.
