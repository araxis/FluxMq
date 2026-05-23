# Dashboard Feature — Architectural Plan

## Context

The user wants a Dashboard concept layered on top of the existing pipeline runtime. Pipelines can already record sessions (`MqttRecorderComponent`, `ISessionRepository`, `IMessageRepository`) and replay them (`ReplaySourceComponent`, `RecordedSessionReplayFactory`). The dashboard adds two things:

1. **Metric calculator nodes** — new `IFlowNode` pipeline nodes users add to workflows in the diagram designer to compute per-topic payload sizes, message rates, error counts, etc.
2. **Dashboard** — a UI that binds "dashboard blocks" (display widgets) to metric node outputs. Data can come from either a live running pipeline or a stored/replayed session — even with no broker connected.

The dashboard does not replace the pipeline. It is a view over runtime/projection outputs. Source mode is selected before the graph runs, so dashboard blocks should not have separate live and replay implementations.

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
| `src/FluxMq.Components/Replay/ReplaySourceComponent.cs` | Source implementation for timed stored-session replay |
| `src/FluxMq.Components/Replay/RecordedSessionReplayFactory.cs` | Creates replay source from SessionId |
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
