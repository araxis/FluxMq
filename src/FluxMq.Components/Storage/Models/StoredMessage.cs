using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using MQTTnet.Protocol;

namespace FluxMq.Components.Storage.Models;

public sealed class StoredMessage
{
    public MessageId Id { get; set; } = MessageId.New();
    public SessionId SessionId { get; set; }
    public string Topic { get; set; } = string.Empty;
    public byte[] Payload { get; set; } = [];
    public DateTimeOffset ReceivedAt { get; set; }
    public MqttQualityOfServiceLevel QualityOfService { get; set; }
    public bool Retain { get; set; }

    public static StoredMessage From(SessionId sessionId, MqttEnvelope envelope) => new()
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
