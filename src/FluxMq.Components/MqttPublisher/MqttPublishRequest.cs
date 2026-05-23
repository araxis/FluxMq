using MQTTnet.Protocol;

namespace FluxMq.Components.MqttPublisher;

public sealed record MqttPublishRequest
{
    public required string Topic { get; init; }
    public byte[] Payload { get; init; } = [];
    public MqttQualityOfServiceLevel QualityOfService { get; init; } = MqttQualityOfServiceLevel.AtMostOnce;
    public bool Retain { get; init; }
}
