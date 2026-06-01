using FluxMq.Components.FileWriter;
using FluxMq.Components.MqttPublisher;
using FluxMq.Components.Replay;
using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxFlow.Components.Http.Contracts;
using FluxFlow.Components.Payloads.Contracts;
using FluxFlow.Engine.Mapping;
using MQTTnet.Protocol;

namespace FluxMq.Components.Mapping;

public sealed class FluxMqRequestMappingExpressionEngine : IFlowExpressionEngine
{
    private readonly IFlowExpressionEngine _inner;

    public FluxMqRequestMappingExpressionEngine(IFlowExpressionEngine inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public string Name => _inner.Name;

    public object? Evaluate(string expression, FlowMapContext context, Type resultType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(resultType);

        if (resultType == typeof(MqttPublishRequest))
        {
            var value = _inner.Evaluate(expression, context, typeof(object));
            return CoercePublishRequest(value, TryGetEnvelope(context));
        }

        if (resultType == typeof(MqttRecordingRequest))
        {
            var value = _inner.Evaluate(expression, context, typeof(object));
            return CoerceRecordingRequest(value, GetRequiredEnvelope(context));
        }

        if (resultType == typeof(FileWriteRequest))
        {
            var value = _inner.Evaluate(expression, context, typeof(object));
            return CoerceFileWriteRequest(value, TryGetEnvelope(context));
        }

        if (resultType == typeof(PayloadInspectionRequest))
        {
            var value = _inner.Evaluate(expression, context, typeof(object));
            return CoercePayloadInspectionRequest(value, TryGetEnvelope(context));
        }

        if (resultType == typeof(HttpRequestInput))
        {
            var value = _inner.Evaluate(expression, context, typeof(object));
            return CoerceHttpRequestInput(value, TryGetEnvelope(context));
        }

        return _inner.Evaluate(expression, context, resultType);
    }

    private static MqttEnvelope GetRequiredEnvelope(FlowMapContext context)
        => TryGetEnvelope(context) ?? throw new InvalidOperationException("FluxMQ request mapping requires an MqttEnvelope input context.");

    private static MqttEnvelope? TryGetEnvelope(FlowMapContext context)
        => context.Variables.TryGetValue("envelope", out var value) && value is MqttEnvelope envelope
            ? envelope
            : null;

    private static MqttPublishRequest CoercePublishRequest(object? value, MqttEnvelope? input)
        => value switch
        {
            MqttPublishRequest request => request,
            null => throw new InvalidOperationException(
                "Mapper expression returned null. Expected MqttPublishRequest object."),
            _ => new MqttPublishRequest
            {
                Topic = ExpressionObjectReader.ReadRequiredString(value, "topic"),
                Payload = ExpressionObjectReader.ReadBytesOrDefault(value, "payload", input?.Payload ?? []),
                QualityOfService =
                    ExpressionObjectReader.TryRead(value, "qos", out _)
                        ? ExpressionObjectReader.ReadEnumOrDefault(value, "qos", input?.QualityOfService ?? MqttQualityOfServiceLevel.AtMostOnce)
                        : ExpressionObjectReader.ReadEnumOrDefault(value, "qualityOfService", input?.QualityOfService ?? MqttQualityOfServiceLevel.AtMostOnce),
                Retain = ExpressionObjectReader.ReadBoolOrDefault(value, "retain", input?.Retain ?? false)
            }
        };

    private static MqttRecordingRequest CoerceRecordingRequest(object? value, MqttEnvelope input)
        => value switch
        {
            MqttRecordingRequest request => request,
            null => throw new InvalidOperationException(
                "Mapper expression returned null. Expected MqttRecordingRequest object."),
            _ => new MqttRecordingRequest
            {
                SessionId = new SessionId(ExpressionObjectReader.ReadRequiredGuid(value, "sessionId")),
                Envelope = input
            }
        };

    private static FileWriteRequest CoerceFileWriteRequest(object? value, MqttEnvelope? input)
        => value switch
        {
            FileWriteRequest request => request,
            null => throw new InvalidOperationException(
                "Mapper expression returned null. Expected FileWriteRequest object."),
            _ => new FileWriteRequest
            {
                Path = ExpressionObjectReader.ReadRequiredString(value, "path"),
                Content = ExpressionObjectReader.ReadBytesOrDefault(value, "content", input?.Payload ?? []),
                Mode = ExpressionObjectReader.ReadEnumOrDefault(value, "mode", FileWriteMode.Overwrite),
                CreateDirectory = ExpressionObjectReader.ReadBoolOrDefault(value, "createDirectory", true)
            }
        };

    private static PayloadInspectionRequest CoercePayloadInspectionRequest(object? value, MqttEnvelope? input)
        => value switch
        {
            PayloadInspectionRequest request => request,
            null => throw new InvalidOperationException(
                "Mapper expression returned null. Expected PayloadInspectionRequest object."),
            _ => new PayloadInspectionRequest
            {
                Bytes = ExpressionObjectReader.ReadOptionalBytes(value, "bytes") ?? input?.Payload,
                Text = ExpressionObjectReader.ReadOptionalString(value, "text"),
                ContentType = ExpressionObjectReader.ReadOptionalString(value, "contentType"),
                EncodingHint =
                    ExpressionObjectReader.ReadOptionalString(value, "encodingHint") ??
                    ExpressionObjectReader.ReadOptionalString(value, "encoding")
            }
        };

    private static HttpRequestInput CoerceHttpRequestInput(object? value, MqttEnvelope? input)
        => value switch
        {
            HttpRequestInput request => request,
            null => throw new InvalidOperationException(
                "Mapper expression returned null. Expected HttpRequestInput object."),
            _ => new HttpRequestInput
            {
                Method = ExpressionObjectReader.ReadOptionalString(value, "method") ?? "GET",
                Url = ExpressionObjectReader.ReadRequiredString(value, "url"),
                Headers = ExpressionObjectReader.ReadStringDictionaryOrEmpty(value, "headers"),
                Body = ExpressionObjectReader.ReadOptionalString(value, "body"),
                Bytes = ExpressionObjectReader.ReadOptionalBytes(value, "bytes") ?? input?.Payload,
                ContentType = ExpressionObjectReader.ReadOptionalString(value, "contentType"),
                TimeoutMilliseconds = ExpressionObjectReader.ReadOptionalInt(value, "timeoutMilliseconds")
            }
        };
}
