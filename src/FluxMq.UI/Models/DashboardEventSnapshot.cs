using FluxFlow.Engine.Components;

namespace FluxMq.UI.Models;

public sealed record DashboardEventSnapshot(
    int Count,
    FlowEvent? LatestEvent,
    int RecentCount,
    TimeSpan RateWindow,
    double EventsPerSecond,
    IReadOnlyList<int> BucketCounts,
    IReadOnlyList<DashboardTopicMetric> TopicCounts,
    long TotalPayloadBytes,
    int UniqueTopicCount,
    int RetainedCount)
{
    public double AveragePayloadBytes => Count == 0 ? 0 : TotalPayloadBytes / (double)Count;
}

public sealed record DashboardTopicMetric(string Topic, int Count);
