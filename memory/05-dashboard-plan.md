# Dashboard Feature — Architectural Plan

## Context

The user wants a Dashboard concept layered on top of the existing pipeline runtime. Pipelines can already record sessions (`MqttRecordingSinkComponent`, `ISessionRepository`, `IMessageRepository`) and replay them (`ReplaySourceComponent`, `RecordedSessionReplayFactory`). The dashboard adds two things:

1. **Metric calculator nodes** — new `IFlowNode` pipeline nodes users add to workflows in the diagram designer to compute per-topic payload sizes, message rates, error counts, etc.
2. **Dashboard** — a UI that binds "dashboard blocks" (display widgets) to metric node outputs. Data can come from either a live running pipeline or a stored/replayed session — even with no broker connected.

The dashboard does not replace the pipeline. It is a view over metric ISourceBlock<T> outputs, whether those are live or produced by an internal replay pipeline.

---

## Phase 1: New Metric Pipeline Components

Location: `src/FluxMq.Components/`

All components follow the `MqttMetricsSinkComponent` pattern: `ActionBlock<MqttEnvelope>` as `Input`, `BroadcastBlock<TSnapshot>` as output, `BroadcastBlock<FlowError>` as `Errors`, completion propagated via `ContinueWith`.

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
- Can be addressed later; the existing `MqttMetricsSinkComponent` covers most initial needs

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

Location: `src/FluxMq.Pipeline/Runtime/DashboardRuntime.cs` (new file)

### Data Source Abstraction

```csharp
public abstract class DashboardDataSource { }

public sealed class LiveDashboardSource(ApplicationRuntime runtime) : DashboardDataSource
{
    public ApplicationRuntime Runtime { get; } = runtime;
}

public sealed class ReplayDashboardSource(SessionId sessionId) : DashboardDataSource
{
    public SessionId SessionId { get; } = sessionId;
}
```

### `DashboardRuntime`

- `StartAsync(DashboardDefinition, DashboardDataSource, ct)`:
  - **Live**: walk definition.Blocks, resolve NodeAddress from the running ApplicationRuntime, cast the RuntimeNode's component to the expected metric component, return its `Snapshots` ISourceBlock
  - **Replay**: use `RecordedSessionReplayFactory` to create `ReplaySourceComponent` from `SessionId`, instantiate the metric components declared in definition.Blocks, link `replay.Output → metric.Input`, start the mini-pipeline, return each metric component's `Snapshots` ISourceBlock
- Exposes per-block data sources; disposes the replay pipeline on `DisposeAsync`

Replay mode works with no broker — `ReplaySourceComponent` drains all stored messages through the metric calculators entirely from the LiteDB store.

---

## Phase 4: UI

### `DashboardService` (`src/FluxMq.UI/Services/DashboardService.cs`)
- Wraps `DashboardRuntime`
- Exposes `ActiveDashboard`, `State`, `Changed` event (following `FlowWorkspaceService` pattern)
- Methods: `RunLiveAsync(definition, runtime)`, `RunReplayAsync(definition, sessionId)`, `StopAsync()`

### Dashboard Page (`src/FluxMq.UI/Pages/DashboardPage.razor`)
- New route: `/dashboard`
- Displays a grid of dashboard blocks
- Each block is a Blazor component that subscribes to its `ISourceBlock<T>` via a linked `ActionBlock<T>`, calling `InvokeAsync(StateHasChanged)` on each update; unlinks on dispose

### Dashboard Block Components (`src/FluxMq.UI/Components/Dashboard/`)
- `MetricsSummaryBlock.razor` — binds to `ISourceBlock<MqttMetricsSnapshot>`
- `PayloadPerTopicBlock.razor` — binds to `ISourceBlock<PayloadSizePerTopicSnapshot>`, table of topic → bytes
- `MessageRateBlock.razor` — binds to `ISourceBlock<MessageRateSnapshot>`, msgs/sec display

### `DashboardPanel.razor` (sidebar, `src/FluxMq.UI/Components/Workspace/`)
- Lets user pick: live (current runtime) or a stored session from `ISessionRepository`
- "Launch Dashboard" button → navigates to `/dashboard`

---

## Critical Files

| File | Action |
|------|--------|
| `src/FluxMq.Components/MqttMetrics/MqttMetricsSinkComponent.cs` | Reference pattern for new components |
| `src/FluxMq.Components/ReplaySourceComponent.cs` | Used in replay mode |
| `src/FluxMq.Components/RecordedSessionReplayFactory.cs` | Creates replay source from SessionId |
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
3. Dashboard definition model (`DashboardDefinition`, `DashboardBlockDefinition`, `DashboardName`) + extend `ApplicationDefinition`
4. `DashboardRuntime` (replay mode first — easier to test; live mode second) + tests
5. `DashboardService` (UI service wrapper)
6. `DashboardPage.razor` + block components
7. `DashboardPanel.razor` (session selector + launch)
8. Register new metric components in `FlowComponentCatalog` + `NodeWidgetRegistry`

---

## Verification

- **Unit tests** for `PayloadSizePerTopicComponent` and `MessageRateComponent`: xUnit + Shouldly + hand-rolled fakes, `BufferBlock<T>` to assert emitted snapshots
- **Unit tests** for `DashboardRuntime` replay mode: create fake stored messages → run → assert final snapshot matches expected aggregation
- **UI**: start pipeline with metric nodes, launch dashboard in live mode → blocks update in real-time; stop pipeline, select stored session, launch in replay mode → blocks populate from session with no broker
