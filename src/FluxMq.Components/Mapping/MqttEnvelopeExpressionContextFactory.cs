using FluxMq.Core.Models;
using FluxMq.Core.Ids;
using FluxMq.Components.FileWriter;
using FluxMq.Components.MqttPublisher;
using FluxMq.Components.Replay;
using FluxFlow.Components.State.Contracts;
using FluxFlow.Engine.Mapping;
using MQTTnet.Protocol;
using System.Text;
using System.Text.Json;

namespace FluxMq.Components.Mapping;

public static class MqttEnvelopeExpressionContextFactory
{
    public static FlowMapContext Create(MqttEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var payloadText = Encoding.UTF8.GetString(envelope.Payload);

        return new FlowMapContext
        {
            Variables = new Dictionary<string, object?>
            {
                ["envelope"] = envelope,
                ["topic"] = envelope.Topic,
                ["payload"] = envelope.Payload,
                ["payloadText"] = payloadText,
                ["payloadJson"] = TryParseJson(payloadText),
                ["qos"] = (int)envelope.QualityOfService,
                ["qualityOfService"] = envelope.QualityOfService,
                ["retain"] = envelope.Retain,
                ["receivedAt"] = envelope.ReceivedAt,
                ["Encoding"] = typeof(Encoding),
                ["MqttQualityOfServiceLevel"] = typeof(MqttQualityOfServiceLevel),
                ["MqttPublishRequest"] = typeof(MqttPublishRequest),
                ["MqttRecordingRequest"] = typeof(MqttRecordingRequest),
                ["SessionId"] = typeof(SessionId),
                ["Guid"] = typeof(Guid),
                ["FileWriteRequest"] = typeof(FileWriteRequest),
                ["FileWriteMode"] = typeof(FileWriteMode),
                ["StateReducerInput"] = typeof(StateReducerInput),
                ["StateReducerOperation"] = typeof(StateReducerOperation)
            }
        };
    }

    private static JsonElement? TryParseJson(string payloadText)
    {
        if (string.IsNullOrWhiteSpace(payloadText))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(payloadText);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
