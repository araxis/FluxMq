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

**Phase:** 5 - desktop workspace authoring polish
**Active feature:** `F-022 - Node Editors`
**Status:** Review
**Started:** 2026-05-23

The mapper workbench now follows a JSONata Exerciser-like shape: Monaco JSON input on the left, Monaco mapper expression in the middle, and live JSON result on the right. MQTT envelope samples use `{ topic, qos, retain, receivedAt, payload }`; arbitrary JSON input is treated as payload for quick experimentation. Mapper output configuration now separates the runtime target type from the result contract: `typed`, `any`, or `json-schema-file`. `json.schema-validator` exists as a standalone runtime/UI component backed by JsonSchema.Net, so schema validation is a reusable runtime capability rather than mapper-only UI behavior.

Actor editors are in place for `mqtt.publisher`, `mqtt.recorder`, and `file.writer`. Source editors are now in review for `generated.source` and `replay.source`. Live broker traffic stays on the existing `mqtt.connection` plus `mqtt.trigger` path; `generated.source` owns its fixed MQTT message list; `replay.source` selects a recorded session and playback speed.

The next desktop authoring slice should move toward runtime/build diagnostics on nodes after the source editor slice is accepted.

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
  - `Output: JsonSchemaValidationResult`
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

## Next Action

Continue Phase 5 after review of the source editor slice:

1. Manually inspect the new source edit dialogs in the desktop workspace.
2. Build and validate a visual flow: `mqtt.trigger -> message filter -> dynamic mapper -> mqtt.publisher`.
3. Confirm generated and replay sources save meaningful JSON and validate through the runtime.
4. Next slice: surface validation/runtime errors on graph nodes.
5. Add pass/fail routing or assertion components only after the plain validation-result output feels right.
