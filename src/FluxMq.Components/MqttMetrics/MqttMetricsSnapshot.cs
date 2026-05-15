namespace FluxMq.Components.MqttMetrics;

public sealed record MqttMetricsSnapshot
{
    public long MessageCount { get; init; }
    public long TotalPayloadBytes { get; init; }
    public long MinPayloadBytes { get; init; }
    public long MaxPayloadBytes { get; init; }
    public long RetainedMessageCount { get; init; }
    public int UniqueTopicCount { get; init; }
    public string? LastTopic { get; init; }
    public DateTimeOffset? LastReceivedAt { get; init; }
    public double AveragePayloadBytes => MessageCount == 0 ? 0 : (double)TotalPayloadBytes / MessageCount;
}
