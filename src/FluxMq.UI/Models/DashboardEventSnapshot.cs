using FluxFlow.Engine.Components;

namespace FluxMq.UI.Models;

public sealed record DashboardEventSnapshot(
    int Count,
    FlowEvent? LatestEvent,
    int RecentCount,
    TimeSpan RateWindow,
    double EventsPerSecond);
