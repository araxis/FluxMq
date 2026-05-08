using FluxMq.Core.Models;
using MQTTnet.Protocol;

namespace FluxMq.Storage.Models;

public sealed class StoredMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public string Topic { get; set; } = string.Empty;
    public byte[] Payload { get; set; } = [];
    public DateTimeOffset ReceivedAt { get; set; }
    public MqttQualityOfServiceLevel QualityOfService { get; set; }
    public bool Retain { get; set; }

    public static StoredMessage From(Guid sessionId, MqttEnvelope envelope) => new()
    {
        SessionId = sessionId,
        Topic = envelope.Topic,
        Payload = envelope.Payload,
        ReceivedAt = envelope.ReceivedAt,
        QualityOfService = envelope.QualityOfService,
        Retain = envelope.Retain
    };

    public MqttEnvelope ToEnvelope() => new()
    {
        Topic = Topic,
        Payload = Payload,
        ReceivedAt = ReceivedAt,
        QualityOfService = QualityOfService,
        Retain = Retain
    };
}
