using FluxMq.Components.FileWriter;
using FluxMq.Components.JsonSchema;
using FluxMq.Components.MqttPayloadInspector;
using FluxMq.Components.MqttPublisher;
using FluxMq.Components.Replay;
using FluxMq.Core.Models;
using FluxFlow.Components.Http.Contracts;
using FluxFlow.Components.Payloads.Contracts;
using System.Text;

namespace FluxMq.Components.Control;

internal static class FlowControlLogShape
{
    public static string Context<TInput>(TInput value, string prefix)
    {
        var parts = new List<string> { prefix, $"inputType={typeof(TInput).Name}" };
        if (value is MqttEnvelope envelope)
        {
            parts.Add($"qos={(int)envelope.QualityOfService}");
            parts.Add($"retain={envelope.Retain}");
        }

        return string.Join("; ", parts);
    }

    public static (string? Topic, int? PayloadBytes) Shape<TInput>(TInput value)
        => value switch
        {
            MqttEnvelope envelope => (envelope.Topic, envelope.Payload.Length),
            MqttPublishRequest request => (request.Topic, request.Payload.Length),
            MqttRecordingRequest request => (request.Envelope.Topic, request.Envelope.Payload.Length),
            InspectedMqttMessage inspected => (inspected.Envelope.Topic, inspected.Envelope.Payload.Length),
            JsonSchemaValidationResult validation => (validation.Envelope.Topic, validation.Envelope.Payload.Length),
            FileWriteRequest request => (null, request.Content.Length),
            PayloadInspectionRequest request => (null, request.Bytes?.Length ?? ByteCount(request.Text)),
            PayloadInspectionResult result => (null, result.ByteCount),
            HttpRequestInput request => (request.Url, request.Bytes?.Length ?? ByteCount(request.Body)),
            HttpResponseOutput response => (response.Url, response.BodyBytes.Length),
            HttpErrorOutput error => (error.Url, null),
            _ => (null, null)
        };

    private static int? ByteCount(string? value)
        => value is null ? null : Encoding.UTF8.GetByteCount(value);
}
