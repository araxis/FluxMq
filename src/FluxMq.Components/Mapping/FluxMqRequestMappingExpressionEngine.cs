using FluxMq.Components.FileWriter;
using FluxMq.Components.MqttPublisher;
using FluxMq.Components.Replay;
using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxFlow.Engine.Mapping;

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
            return CoercePublishRequest(value, GetEnvelope(context));
        }

        if (resultType == typeof(MqttRecordingRequest))
        {
            var value = _inner.Evaluate(expression, context, typeof(object));
            return CoerceRecordingRequest(value, GetEnvelope(context));
        }

        if (resultType == typeof(FileWriteRequest))
        {
            var value = _inner.Evaluate(expression, context, typeof(object));
            return CoerceFileWriteRequest(value, GetEnvelope(context));
        }

        return _inner.Evaluate(expression, context, resultType);
    }

    private static MqttEnvelope GetEnvelope(FlowMapContext context)
    {
        if (context.Variables.TryGetValue("envelope", out var value) &&
            value is MqttEnvelope envelope)
        {
            return envelope;
        }

        throw new InvalidOperationException("FluxMQ request mapping requires an MqttEnvelope input context.");
    }

    private static MqttPublishRequest CoercePublishRequest(object? value, MqttEnvelope input)
        => value switch
        {
            MqttPublishRequest request => request,
            null => throw new InvalidOperationException(
                "Mapper expression returned null. Expected MqttPublishRequest object."),
            _ => new MqttPublishRequest
            {
                Topic = ExpressionObjectReader.ReadRequiredString(value, "topic"),
                Payload = ExpressionObjectReader.ReadBytesOrDefault(value, "payload", input.Payload),
                QualityOfService =
                    ExpressionObjectReader.TryRead(value, "qos", out _)
                        ? ExpressionObjectReader.ReadEnumOrDefault(value, "qos", input.QualityOfService)
                        : ExpressionObjectReader.ReadEnumOrDefault(value, "qualityOfService", input.QualityOfService),
                Retain = ExpressionObjectReader.ReadBoolOrDefault(value, "retain", input.Retain)
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

    private static FileWriteRequest CoerceFileWriteRequest(object? value, MqttEnvelope input)
        => value switch
        {
            FileWriteRequest request => request,
            null => throw new InvalidOperationException(
                "Mapper expression returned null. Expected FileWriteRequest object."),
            _ => new FileWriteRequest
            {
                Path = ExpressionObjectReader.ReadRequiredString(value, "path"),
                Content = ExpressionObjectReader.ReadBytesOrDefault(value, "content", input.Payload),
                Mode = ExpressionObjectReader.ReadEnumOrDefault(value, "mode", FileWriteMode.Overwrite),
                CreateDirectory = ExpressionObjectReader.ReadBoolOrDefault(value, "createDirectory", true)
            }
        };
}
