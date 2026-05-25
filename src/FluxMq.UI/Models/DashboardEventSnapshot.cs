using FluxMq.Pipeline.Components;

namespace FluxMq.UI.Models;

public sealed record DashboardEventSnapshot(
    int Count,
    FlowEvent? LatestEvent);
