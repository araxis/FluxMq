---
name: FluxMQ feature list
description: Followable product and implementation feature backlog for FluxMQ.
type: project
---
# FluxMQ Feature List

This is the followable backlog for FluxMQ. Each feature should be implemented as a small, testable vertical slice. The first strong release should favor a clear local-first MQTT workflow, explicit flow components, reliable mapping, and useful diagnostics before broad protocol expansion.

## Strategic Product Goals

- **Local-first MQTT workbench:** users can connect, subscribe, publish, inspect, record, replay, and debug MQTT traffic from the desktop app without external infrastructure.
- **Developer integration flows:** users can build ELT-style message flows with explicit sources, filters, routers, dynamic mappers, validators, and actor components.
- **Ops and QA testing:** users can assert message expectations, validate payloads, measure rates, count failures, and replay or simulate traffic for system checks.
- **Protocol growth without redesign:** MQTT is the first protocol, but the runtime shape should support AMQP, HTTP, files, email, Bluetooth, and composed multi-protocol workflows later.
- **Trust and clarity:** side-effecting components must expose explicit command inputs. FluxMQ should not hide auto-mappers or unclear shared-resource behavior behind visual convenience.

## Feature Format

Each feature should carry:

- **Goal:** what the user or developer can do after it exists.
- **UI/UX:** where the capability appears and how it should feel.
- **Data/Logic:** runtime, storage, service, or schema changes.
- **Acceptance:** concrete checks before calling it done.

Priority:

- **P0:** already-required foundation for a useful app.
- **P1:** core developer flow builder and local MQTT workflow.
- **P2:** ops/QA observability, validation, and test workflows.
- **P3:** extensibility, polish, and protocol expansion.

## P0 - Runtime And Local MQTT Foundation

### F-001 - Core MQTT Session

**Goal:** Connect, disconnect, subscribe, receive, and publish MQTT messages through a reusable domain boundary.

**UI/UX:** Connection state should be visible and boring: clear connected/disconnected/reconnecting states, explicit publish controls, no hidden broker actions.

**Data/Logic:** `MqttConnectionProfile`, `MqttSession`, `MqttEnvelope`, topic subscriptions, publish API, lifecycle state.

**Acceptance:**

- Session connects and disconnects deterministically.
- Subscriptions receive matching `MqttEnvelope` values.
- Publish calls surface failures without crashing the host.

### F-002 - Local Storage And Recording

**Goal:** Record sessions and messages locally so live traffic can be inspected, replayed, and tested later.

**UI/UX:** Users can see saved sessions, select a session, inspect stored messages, and understand what was recorded.

**Data/Logic:** LiteDB repositories for connection profiles, sessions, messages, and app settings.

**Acceptance:**

- Recording writes ordered messages with session identity.
- Stored sessions can be loaded without a broker connection.
- Storage errors become user-visible errors, not silent data loss.

### F-003 - Payload Inspector

**Goal:** Inspect raw and formatted payloads for common formats.

**UI/UX:** The inspector should work for live, stored, replayed, and generated messages using the same component path.

**Data/Logic:** Payload format detection for JSON, XML, Base64, text, and binary payloads; inspected payload model.

**Acceptance:**

- JSON/text payloads display formatted and raw views.
- Binary payloads remain inspectable without corrupting bytes.
- Inspector output is available as a runtime mapper/projection output.

### F-004 - Flow Application Runtime

**Goal:** Build and run typed flow graphs from configuration.

**UI/UX:** Users should be able to validate, run, stop, and debug flow application definitions from CLI and desktop.

**Data/Logic:** `ApplicationDefinition`, workflow definitions, typed ports, factory registry, structured build errors, lifecycle, and phase-based startup.

**Acceptance:**

- Invalid node types, missing ports, and type mismatches return structured build errors.
- Runtime starts and drains linked Dataflow graphs predictably.
- CLI and UI use the same `FluxMq.App` host boundary.

## P1 - Developer Flow Builder

### F-010 - Explicit Source Nodes

**Goal:** Feed workflows from clear source components.

**UI/UX:** The component catalog exposes concrete source nodes instead of a generic mode-driven source.

**Data/Logic:** Source nodes:

- `mqtt.live-source`
- `session.source`
- `replay.source`
- `generated.source`
- `mqtt.trigger`
- `mqtt.connection-state-trigger`

**Acceptance:**

- Each source emits typed output ports.
- Downstream components do not need to know whether messages came from live MQTT, stored sessions, replay, or generated data.
- New definitions use explicit source names.

### F-011 - Expression Filters And Routers

**Goal:** Let users route message streams with real predicates.

**UI/UX:** Filter/router nodes should expose predicate fields with examples and validation feedback.

**Data/Logic:** Dynamic Expresso predicates over stable message variables such as `envelope`, `topic`, `payloadText`, `qos`, `retain`, and `receivedAt`.

**Acceptance:**

- `mqtt.message-filter` supports expressions such as `qos >= 1`.
- Predicate failures publish `FlowError` and do not stop later messages.
- Router/filter ports remain type-safe.

### F-012 - Dynamic Mapper

**Goal:** Make mapping a first-class visible component.

**UI/UX:** The graph shows `flow.mapper` between source/filter/router nodes and actor nodes. Request models are not shown as separate catalog components.

**Data/Logic:** `flow.mapper` configuration includes:

- input type
- output type
- output contract (`typed`, `any`, `json-schema-file`)
- engine (`dynamic-expresso`, `jsonata`)
- expression that returns the whole output command/request object

Initial output targets:

- `MqttPublishRequest`
- `MqttRecordingRequest`
- `FileWriteRequest`

**Acceptance:**

- UI catalog exposes `flow.mapper`, not request-specific mapper aliases.
- Actors are not automatically wired to `MqttEnvelope` sources when their input type requires a request.
- Dynamic Expresso and Jsonata mappings are covered by runtime tests.

### F-013 - Actor Components

**Goal:** Side-effecting components act only on explicit command/request input.

**UI/UX:** Actor node names should describe the action: MQTT Publisher, MQTT Recorder, File Writer.

**Data/Logic:** Initial actors:

- `mqtt.publisher`: `MqttPublishRequest -> publish`
- `mqtt.recorder`: `MqttRecordingRequest -> repository write`
- `file.writer`: `FileWriteRequest -> file write`

**Acceptance:**

- Actors do not infer commands from raw envelopes.
- Actor failures publish `FlowError` and continue when possible.
- UI descriptions name required input types and recommend adding a Dynamic Mapper upstream.

### F-014 - JSON Schema Validator

**Goal:** Validate payloads against JSON Schema as an explicit runtime component.

**UI/UX:** Users can configure schema source, validation mode, and result routing. Invalid payloads should be obvious in both graph output and inspector views.

**Data/Logic:** Add a validator component with typed output such as `JsonSchemaValidationResult`, plus error output for processing failures.

Initial implementation uses JsonSchema.Net and supports inline schema JSON or a schema file path. The component validates `MqttEnvelope.Payload` as JSON and emits `JsonSchemaValidationResult` with the original envelope and issue list.

**Acceptance:**

- Valid and invalid JSON payloads produce typed validation results.
- Schema parse/configuration errors are reported before runtime where possible.
- Validator works for live, stored, replayed, and generated message streams.

### F-015 - JSONata Mapper Workbench UI

**Goal:** Make JSONata mapping feel like an industrial integration mapper instead of a raw text box.

**UI/UX:** Inspired by OPC Router-style visual integration tools and the JSONata Exerciser shape: show editable JSON input, one object-expression editor, and live JSON result in one focused mapper editor. Do not copy OPC Router terminology; keep FluxMQ concepts visible.

Expected mapper editor layout:

- left Monaco JSON input sample from a recent/sample upstream message; MQTT envelopes use `{ topic, qos, retain, receivedAt, payload }`, and arbitrary JSON can be treated as payload
- middle expression editor that returns the whole target request object
- right output request shape for `MqttPublishRequest`, `MqttRecordingRequest`, `FileWriteRequest`, and future command types
- right live JSON result/errors panel for test evaluation
- helper actions to insert selected input fields into the expression

**Data/Logic:** Mapper output schemas, sample message provider, Monaco-backed expression editor, FluxMQ light/dark editor themes, JSONata language definition, preview evaluation service, shared runtime/UI expression evaluation path where practical. `expression` is the primary mapper configuration; old per-field `map` shapes are only a compatibility/fallback detail while the codebase migrates.

**Acceptance:**

- A user can configure a JSONata `flow.mapper` without editing raw JSON.
- Required output fields show validation status.
- Preview runs live against a selected/recent/sample or manually edited `MqttEnvelope`.
- The saved configuration is the same flow definition shape used by runtime execution.

## P2 - Desktop Workspace And Flow Authoring

### F-020 - Desktop Workspace Shell

**Goal:** Provide a real operational desktop workspace, not a landing page.

**UI/UX:** Use MudBlazor for dense, work-focused panels: app tree, component catalog, diagram, JSON editor, live inspector, session panel, payload inspector.

**Data/Logic:** Workspace state, selected app, selected workflow, selected node, recent messages, stored sessions, validation/run state.

**Acceptance:**

- Users can create/open/save flow app JSON.
- Layout stays usable at desktop and smaller widths without text overlap.
- UI state does not mutate runtime definitions accidentally.

### F-021 - Visual Flow Diagram

**Goal:** Users can compose workflows visually.

**UI/UX:** Diagram uses typed ports, clear node categories, stable link styling, and specialized widgets where useful.

**Data/Logic:** Blazor.Diagrams node models, port descriptors, link composer, component catalog descriptors.

**Acceptance:**

- Adding a component creates a valid definition node.
- Links are typed and can round-trip to JSON.
- Hidden compatibility aliases still load, but new catalog choices are clean.

### F-022 - Node Editors

**Goal:** Each important node has an editor that exposes only meaningful configuration.

**UI/UX:** Use focused dialogs or panels. Dynamic Mapper editor should expose engine, fixed `MqttEnvelope` input, result contract, typed output target when needed, and the object expression editor. Source and actor editors should explain required resource/input relationships.

The mapper input type is currently fixed to `MqttEnvelope` in the editor; the editable output side is a result contract, not an input box. JSON Schema Validator has a focused editor for inline schema JSON, schema file path, and schema id.

**Data/Logic:** Configuration serialization through `FlowDefinitionComposer`, validation before save/run.

**Acceptance:**

- Editing a node updates JSON and diagram state together.
- Invalid config is visible before runtime when possible.
- Dynamic Mapper defaults are useful but not hidden magic.

### F-023 - Runtime Control And Diagnostics

**Goal:** Users can validate, run, stop, and inspect flow apps from the desktop.

**UI/UX:** Run state, build errors, component errors, and latest activity should be visible without reading logs.

**Data/Logic:** `FluxMq.App` host integration, runtime build results, flow errors, node activity projections.

**Acceptance:**

- Validate/run/stop paths use the same host boundary as CLI.
- Build/runtime errors are attached to the responsible node where possible.
- Running a flow does not require hidden UI-specific behavior.

## P2 - Ops, QA, And Observability

### F-030 - Metrics Observer And Dashboard Blocks

**Goal:** Measure message count, payload sizes, topic activity, and message rates from any input stream.

**UI/UX:** Metrics should appear both as graph outputs and dashboard/projection widgets.

**Data/Logic:** `mqtt.metrics`, snapshots, rate/counter/summary components, projection services.

**Acceptance:**

- Metrics do not care whether input is live, stored, replayed, or generated.
- Dashboard blocks consume runtime/projection streams.
- Message-rate and fault-count metrics are configurable.

### F-031 - Assertions And Expectations

**Goal:** Let ops/QA users express expected message behavior.

**UI/UX:** Scenario-style components should support statements like: publish request X, expect response Y, validate payload schema Z.

**Data/Logic:** Assertion components, expectation windows/timeouts, typed pass/fail result outputs.

**Acceptance:**

- Assertion results are streamable and storable.
- Timeouts, mismatches, and validation failures are distinguishable.
- Assertions can be run against generated, replayed, or live traffic.

### F-032 - Scenario Runner

**Goal:** Run repeatable protocol/system tests.

**UI/UX:** Users can start a scenario, see steps, inspect messages, and export results.

**Data/Logic:** Scenario definition model, runner lifecycle, result storage, optional report output.

**Acceptance:**

- A scenario can publish a message and wait for expected response traffic.
- Results include input, observed messages, assertions, timing, and errors.
- Failed scenarios are explainable without digging through raw logs.

## P3 - Integrations And Extensibility

### F-040 - OpenTelemetry Export

**Goal:** Export selected runtime signals to external observability systems.

**UI/UX:** Optional configuration, never required for local use.

**Data/Logic:** Safe metric names, units, attributes, and topic-cardinality strategy.

**Acceptance:**

- Local metrics work without OpenTelemetry.
- Exported telemetry avoids unbounded topic labels by default.
- Runtime/node IDs and flow error codes are stable dimensions.

### F-041 - Plugin And Module Contracts

**Goal:** Let FluxMQ grow through internal modules first, then stable public plugin contracts.

**UI/UX:** New modules can contribute components, node editors, projections, and dashboard blocks.

**Data/Logic:** Component descriptors, property schemas, module registry, future plugin abstractions.

**Acceptance:**

- Adding a component does not require editing every UI panel.
- Component contracts include typed ports and configuration schema.
- Dynamic external loading waits until internal boundaries are proven.

### F-042 - Multi-Protocol Expansion

**Goal:** Add protocols beyond MQTT without changing the core graph model.

**UI/UX:** Protocol-specific sources and actors should still follow source/mapper/actor vocabulary.

**Data/Logic:** Candidate additions:

- HTTP sender/source
- AMQP source/publisher
- email sender
- file watcher/reader
- Bluetooth source/actor

**Acceptance:**

- New protocols expose typed events/commands.
- Mappers bridge protocol event types to actor command types.
- Shared runtime validation remains type-safe.

## Suggested Implementation Order

1. F-015 JSONata Mapper Workbench UI.
2. F-014 JSON Schema Validator.
3. F-022 Node editor completeness for sources, mapper, actors, validator.
4. F-030 Metrics observer/dashboard blocks.
5. F-031 Assertions and expectations.
6. F-032 Scenario runner.
7. F-013 Additional actors: HTTP Sender, Email Sender.
8. F-042 Additional sources: HTTP/AMQP/file import.
9. F-040 OpenTelemetry export.
10. F-041 Plugin/module contracts.

## MVP Cut Line

MVP should include:

- local MQTT connect/subscribe/publish
- payload inspector
- session recording and stored-session browsing
- replay and generated traffic source
- visual flow app editor with save/load/validate/run/stop
- explicit source, filter, router, dynamic mapper, actor, observer components
- Dynamic Expresso and Jsonata mapper engines
- JSON Schema validator
- MQTT Publisher, MQTT Recorder, File Writer
- basic metrics dashboard/projection
- clear error reporting from build/runtime/component failures

Post-MVP:

- assertions and scenario runner
- HTTP/email/AMQP actors and sources
- OpenTelemetry export
- plugin/module marketplace-style extension model
- richer reports for QA runs and session analysis
