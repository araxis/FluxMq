namespace FluxMq.Core.Metrics;

public sealed record FluxMetricReading<TValue>
{
    public required string MetricId { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    public required TValue Value { get; init; }
}
