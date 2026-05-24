namespace FluxMq.UI.Models;

public sealed record MqttTriggerActivitySnapshot
{
    public long MessageCount { get; init; }
    public string? LastTopic { get; init; }
    public long LastPayloadBytes { get; init; }
    public DateTimeOffset? LastReceivedAt { get; init; }
}
