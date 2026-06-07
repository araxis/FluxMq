# Dashboard Feature — Architectural Plan

## Context

The user wants a Dashboard concept layered on top of the existing pipeline runtime. Pipelines can already record sessions (`MqttRecorderComponent`, `IMessageRepository`) and replay them through the package-backed session store adapter used by `session.source` and `replay.source`. The dashboard adds two things:

1. **Metric calculator nodes** — new `IFlowNode` pipeline nodes users add to workflows in the diagram designer to compute per-topic payload sizes, message rates, error counts, etc.
2. **Dashboard** — a UI that binds "dashboard blocks" (display widgets) to metric node outputs. Data can come from either a live running pipeline or a stored/replayed session — even with no broker connected.

The dashboard does not replace the pipeline. It is a view over runtime/projection outputs. Source mode is selected before the graph runs, so dashboard blocks should not have separate live and replay implementations.

## 2026-06-07 Current Dashboard Editor Direction

- Dashboard metric query, visualization, and cell style are separate concerns.
- Metric query defines the number: source, measure, window, filters, format.
- Metric visualization defines the inner presentation: label/title/subtitle, visibility, placement, alignment, inner background, inner border, colors, digits, glow, and fit behavior.
- Cell style defines only the outer dashboard cell/container: background, border, radius, padding, and grid layout behavior.
- KPI is the first widget using this ownership model. `metric.value` and `metric.digital` are separate visualization modules with their own defaults, property definitions, renderers, editor dialog, summary, and compatibility loading.
- The KPI inspector should stay compact but inline: one `Visualization` row, followed by the selected visualization module's own property rows in the same property grid. Do not use a separate visual-settings popup for KPI unless the user explicitly re-approves that direction.
- Existing dashboards remain loadable through compatibility fallbacks, but applying visualization settings writes visualization-owned keys such as `metric.value.*` and `metric.digital.*`, not old shared `kpi.*` display keys.
- Next step after visual QA: review the KPI visualization editor flow manually, then reuse the approved pattern one widget at a time.

---

## Phase 1: New Metric Pipeline Components

Location: `src/FluxMq.Components/`

All components follow the `MqttMetricsComponent` pattern: `ActionBlock<MqttEnvelope>` as `Input`, `BroadcastBlock<TSnapshot>` as output, `BroadcastBlock<FlowError>` as `Errors`, completion propagated via `ContinueWith`.

### A. `PayloadSizePerTopicComponent`
- Folder: `Components/PayloadSizePerTopic/`
- Output: `ISourceBlock<PayloadSizePerTopicSnapshot>` via `Snapshots` property
- Snapshot: `sealed record PayloadSizePerTopicSnapshot` with `IReadOnlyDictionary<string, long> BytesPerTopic`, `IReadOnlyDictionary<string, long> CountPerTopic`
- On each envelope: update the per-topic accumulator (lock-guarded `Dictionary<string, long>`), post a new snapshot via BroadcastBlock

### B. `MessageRateComponent`
- Folder: `Components/MessageRate/`
- Output: `ISourceBlock<MessageRateSnapshot>` via `Snapshots` property
- Snapshot: `sealed record MessageRateSnapshot` with `double MessagesPerSecond`, `long WindowMessageCount`, `TimeSpan WindowDuration`
- Uses `System.Threading.PeriodicTimer` to emit snapshots at a fixed interval (configurable window, e.g. 5s); timer started in `StartAsync`, stopped on completion
- On each envelope: record `ReceivedAt` in a sliding-window queue; on timer tick, prune stale entries and compute rate, then post snapshot

### C. `ErrorCountComponent` (deferred — lower priority)
- Input: `ITargetBlock<FlowError>` — connects to other nodes' `Errors` output ports
- Output: `ISourceBlock<int>` error count
- Can be addressed later; the existing `MqttMetricsComponent` covers most initial needs

### Registration
- Add descriptors to `FlowComponentCatalog` (`src/FluxMq.UI/Services/FlowComponentCatalog.cs`):
  - `"metrics.payloadPerTopic"` — category: "Metrics", ports: Input (MqttEnvelope), Snapshots (PayloadSizePerTopicSnapshot), Errors (FlowError)
  - `"metrics.messageRate"` — category: "Metrics", ports: Input (MqttEnvelope), Snapshots (MessageRateSnapshot), Errors (FlowError)
- Add widget stubs to `NodeWidgetRegistry` (`src/FluxMq.UI/Services/NodeWidgetRegistry.cs`)
- Wire up in `FlowApplicationHost` / the existing component factory so these node types are recognized and instantiated during `Build()`

---

## Phase 2: Dashboard Definition Model

Location: `src/FluxMq.Pipeline/Definitions/`

```csharp
// DashboardName.cs — follows WorkflowName / NodeName pattern
public readonly record struct DashboardName(string Value);

// DashboardBlockDefinition.cs
public sealed record DashboardBlockDefinition
{
    public required string BlockType { get; init; }      // "metrics.summary" | "metrics.payloadPerTopic" | "metrics.rate"
    public required string Title { get; init; }
    public required string SourceAddress { get; init; }  // NodeAddress string — which metric node to subscribe to
    public Dictionary<string, JsonElement> Configuration { get; init; } = [];
}

// DashboardDefinition.cs
public sealed record DashboardDefinition
{
    public required DashboardName Name { get; init; }
    public List<DashboardBlockDefinition> Blocks { get; init; } = [];
}
```

Extend `ApplicationDefinition.cs`:
```csharp
public Dictionary<string, DashboardDefinition> Dashboards { get; init; } = [];
```

This keeps everything in one application JSON file, consistent with the existing Resources + Workflows structure.

---

## Metric Builder Direction

The visual metric builder is the standard-mode facade, not the final metric capability boundary. The dashboard metric model should be treated as a context-aware metric function contract: given the active app/runtime context, event projections, topic state, payload summaries, and saved resources, it produces a typed metric value or series for widgets.

Design implications:

- Keep the current sentence-style visual builder simple, readable, and safe for common users.
- Keep persisted metric definitions neutral and structured so they can later be produced by either the visual builder or an advanced expression-backed editor.
- Do not bake KPI-specific UI assumptions into the metric engine; KPI is only the first consumer.
- Preserve an upgrade path for an advanced mode where experienced users can define metric logic with code-like expressions over an explicit app context.
- Validate advanced metrics before execution, show preview output against sample/live context, and keep failures isolated to the metric result instead of breaking the dashboard.
- Make the visual builder a generator/editor for structured metric definitions, not a separate metric system.

This matters now because the query-builder UI should expose intent as `Measure + Source + Window + Match + Format`, while the underlying model stays capable of representing richer metric functions later.

### 2026-06-07 - Event Counter Metric Builder Slice

- Reused the approved metric query builder for `event.counter` as the second focused dashboard consumer after KPI.
- Kept `event.counter` count-only by passing an allowed-measure list into the builder and normalizing old/non-count drafts back to `count`.
- Moved event counter event/status/topic matching into the reusable metric definition path; new saves keep the widget config focused on display/style plus metric binding.
- Updated dashboard inspector wording so KPI and event counter open widget-specific query dialogs while sharing the same metric framework.
- Added focused tests for event counter default config, composer-created metric bindings, property-grid builder dispatch, and legacy widget-filter cleanup.
- Follow-up: aligned the event counter editor-cell view with the popup preview by rendering from the metric value path, removed the old sparkline/counter delegate from the module, and compacted the metric-query inspector row into a single-line value/summary/action layout.
- NuGet/source-reference follow-up: source-reference test mode now swaps FluxMQ Core, Scenarios, Components, App, and UI FluxFlow dependencies to project references consistently, preventing mixed package/source assembly conflicts after FluxFlow package upgrades while keeping package-only fallback intact.

### 2026-06-07 - Metric Visualization Foundation Slice

- Added a UI-only metric visualization foundation so dashboard metric definitions, metric visualizations, and cell styles remain separate concerns.
- Introduced `metric.value` as the first visualization module; KPI, event counter, event rate, and rate tile use the shared value view instead of duplicating number rendering.
- Converted `event.rate` into a rate-only metric-builder consumer; the widget config stays focused on display/style plus metric binding, and the metric query owns source/window/match/format.
- Removed the old event-rate progress-track dependency from `event.rate` and `rate.tile`; linear meters remain a future focused visualization rather than hidden behavior inside rate widgets.
- Updated KPI as the reference consumer for the visualization layer: KPI display settings now include a catalog-backed `Visualization` row and persist `visualization = metric.value` through defaults, reset, save, and reopen.
- Next step after UI review: verify the KPI `Visualization` row feels right in the property grid, then reuse the same pattern for the next focused metric widget before adding `metric.digital` or `metric.arcMeter`.
- Visualization-specific property rows must be driven by the selected `DashboardMetricVisualizationModule`, not hard-coded in the widget inspector. `metric.value` currently owns the value visual settings for title/subtitle/value colors and title/value alignment/placement.
- Next approved implementation step after KPI visual QA: add one second visualization module, likely `metric.digital` or `metric.arcMeter`, and prove the property grid swaps to that module's property set without changing KPI metric-query or cell-style behavior.

---

## Phase 3: Dashboard Runtime

Location: `src/FluxMq.Pipeline/Runtime/DashboardRuntime.cs` or a projection-oriented runtime package after the source-agnostic refactor.

Dashboard runtime should bind blocks to typed runtime ports or projection outputs from the active application runtime.

It should not choose between live and replay execution. The active runtime already has a source binding:

- live broker source
- stored session source
- timed replay source
- as-fast-as-possible offline source
- imported/generated source later

Replay and offline mode work with no broker because the workflow source is bound to stored data before runtime start. Metric nodes and dashboard blocks receive the same `MqttEnvelope` stream shape as live mode.

---

## Phase 4: UI

### `DashboardService` (`src/FluxMq.UI/Services/DashboardService.cs`)
- Wraps dashboard/projection runtime
- Exposes `ActiveDashboard`, `State`, `Changed` event (following `FlowWorkspaceService` pattern)
- Methods should start from an active runtime/projection set. Live vs offline is selected by source binding outside the dashboard service.

### Dashboard Page (`src/FluxMq.UI/Pages/DashboardPage.razor`)
- New route: `/dashboard`
- Displays a grid of dashboard blocks
- Each block is a Blazor component that subscribes to its `ISourceBlock<T>` via a linked `ActionBlock<T>`, calling `InvokeAsync(StateHasChanged)` on each update; unlinks on dispose

### Dashboard Block Components (`src/FluxMq.UI/Components/Dashboard/`)
- `MetricsSummaryBlock.razor` — binds to `ISourceBlock<MqttMetricsSnapshot>`
- `PayloadPerTopicBlock.razor` — binds to `ISourceBlock<PayloadSizePerTopicSnapshot>`, table of topic → bytes
- `MessageRateBlock.razor` — binds to `ISourceBlock<MessageRateSnapshot>`, msgs/sec display

### `DashboardPanel.razor` (sidebar, `src/FluxMq.UI/Components/Workspace/`)
- Lets user select the active source binding: live broker, stored session, replay speed, or offline-as-fast-as-possible.
- "Launch Dashboard" button → navigates to `/dashboard`

---

## Critical Files

| File | Action |
|------|--------|
| `src/FluxMq.Components/MqttMetrics/MqttMetricsComponent.cs` | Reference pattern for new components |
| `src/FluxMq.Components/MessageSource/StoredSessionSourceComponent.cs` | Source implementation for stored-session and timed replay streams |
| `src/FluxMq.Components/Storage/FluxMqSessionStore.cs` | Adapts local stored MQTT messages to shared session contracts |
| `src/FluxMq.Pipeline/Definitions/ApplicationDefinition.cs` | Extend with Dashboards dict |
| `src/FluxMq.Pipeline/Runtime/` | Add DashboardRuntime.cs |
| `src/FluxMq.UI/Services/FlowComponentCatalog.cs` | Register new metric node types |
| `src/FluxMq.UI/Services/NodeWidgetRegistry.cs` | Register diagram widgets |
| `src/FluxMq.UI/Services/FlowWorkspaceService.cs` | Reference pattern for DashboardService |
| `src/FluxMq.UI/Services/LiveMqttWorkspaceService.cs` | Session/recording management reference |

---

## Implementation Order

1. `PayloadSizePerTopicComponent` + tests
2. `MessageRateComponent` + tests
3. Source-agnostic runtime binding refactor
4. Dashboard definition model (`DashboardDefinition`, `DashboardBlockDefinition`, `DashboardName`) + extend `ApplicationDefinition`
5. Dashboard runtime/projection binding + tests
6. `DashboardService` (UI service wrapper)
7. `DashboardPage.razor` + block components
8. `DashboardPanel.razor` (source binding selector + launch)
9. Register new metric components in `FlowComponentCatalog` + `NodeWidgetRegistry`

---

## Verification

- **Unit tests** for `PayloadSizePerTopicComponent` and `MessageRateComponent`: xUnit + Shouldly + hand-rolled fakes, `BufferBlock<T>` to assert emitted snapshots
- **Unit tests** for source-agnostic dashboard behavior: bind the same workflow/dashboard to a live-style in-memory source and a stored-session source, assert identical final snapshots
- **UI**: run pipeline with metric nodes from live broker → blocks update in real time; switch source binding to stored session → blocks populate from stored data with no broker and without changing downstream workflow/dashboard definitions
