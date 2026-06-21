using MQTTnet.Protocol;

namespace FluxMq.Core.Models;

public sealed record MqttEnvelope
{
    public required string Topic { get; init; }
    public required byte[] Payload { get; init; }
    public DateTimeOffset ReceivedAt { get; init; } = DateTimeOffset.UtcNow;
    public MqttQualityOfServiceLevel QualityOfService { get; init; }
    public bool Retain { get; init; }
    public string? BrokerName { get; init; }
}
