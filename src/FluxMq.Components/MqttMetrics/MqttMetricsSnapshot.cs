namespace FluxMq.Components.MqttMetrics;

public sealed record MqttTopicMetric(string Topic, long Count);
public sealed record MqttTopicRateMetric(string Topic, long Count, double MessagesPerSecond);

public sealed record MqttMetricsSnapshot
{
    public static readonly TimeSpan DefaultRateWindow = TimeSpan.FromSeconds(60);

    public long MessageCount { get; init; }
    public long RollingMessageCount { get; init; }
    public long TotalPayloadBytes { get; init; }
    public long MinPayloadBytes { get; init; }
    public long MaxPayloadBytes { get; init; }
    public long RetainedMessageCount { get; init; }
    public int UniqueTopicCount { get; init; }
    public string? LastTopic { get; init; }
    public DateTimeOffset? LastReceivedAt { get; init; }
    public TimeSpan RateWindow { get; init; } = DefaultRateWindow;
    public TimeSpan SinceStartDuration { get; init; } = TimeSpan.Zero;
    public IReadOnlyList<MqttTopicMetric> TopicCounts { get; init; } = [];
    public IReadOnlyList<MqttTopicRateMetric> TopicRates { get; init; } = [];
    public double AveragePayloadBytes => MessageCount == 0 ? 0 : (double)TotalPayloadBytes / MessageCount;
    public double MessagesPerSecond => RateWindow <= TimeSpan.Zero ? 0 : RollingMessageCount / RateWindow.TotalSeconds;
    public double AverageMessagesPerSecond => SinceStartDuration <= TimeSpan.Zero ? 0 : MessageCount / SinceStartDuration.TotalSeconds;
}
