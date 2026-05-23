using FluxMq.Core.Models;
using FluxMq.Pipeline.Mapping;
using System.Text;

namespace FluxMq.Components.Mapping;

public static class MqttEnvelopeExpressionContextFactory
{
    public static FlowMapContext Create(MqttEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        return new FlowMapContext
        {
            Variables = new Dictionary<string, object?>
            {
                ["envelope"] = envelope,
                ["topic"] = envelope.Topic,
                ["payload"] = envelope.Payload,
                ["payloadText"] = Encoding.UTF8.GetString(envelope.Payload),
                ["qos"] = (int)envelope.QualityOfService,
                ["qualityOfService"] = envelope.QualityOfService,
                ["retain"] = envelope.Retain,
                ["receivedAt"] = envelope.ReceivedAt
            }
        };
    }
}
