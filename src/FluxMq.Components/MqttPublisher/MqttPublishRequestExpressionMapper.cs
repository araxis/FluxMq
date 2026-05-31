using FluxMq.Core.Models;
using FluxMq.Components.Mapping;
using FluxFlow.Engine.Mapping;

namespace FluxMq.Components.MqttPublisher;

public sealed class MqttPublishRequestExpressionMapper : IFlowMapper<MqttEnvelope, MqttPublishRequest>
{
    private readonly IFlowExpressionEngine _engine;
    private readonly MqttPublishRequestMapDefinition _definition;

    public MqttPublishRequestExpressionMapper(
        IFlowExpressionEngine engine,
        MqttPublishRequestMapDefinition definition)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        if (string.IsNullOrWhiteSpace(_definition.Expression))
        {
            throw new InvalidOperationException("MQTT publish request mapping requires an expression.");
        }
    }

    public MqttPublishRequest Map(MqttEnvelope input, FlowMapContext context)
        => EvaluateRequest(_definition.Expression, input, context);

    private MqttPublishRequest EvaluateRequest(string expression, MqttEnvelope input, FlowMapContext context)
    {
        var value = _engine.Evaluate(expression, context, typeof(object));
        return value switch
        {
            MqttPublishRequest request => request,
            null => throw new InvalidOperationException("Mapper expression returned null. Expected MqttPublishRequest object."),
            _ => CoerceRequest(value, input)
        };
    }

    private static MqttPublishRequest CoerceRequest(object value, MqttEnvelope input)
        => new()
        {
            Topic = ExpressionObjectReader.ReadRequiredString(value, "topic"),
            Payload = ExpressionObjectReader.ReadBytesOrDefault(value, "payload", input.Payload),
            QualityOfService =
                ExpressionObjectReader.TryRead(value, "qos", out _)
                    ? ExpressionObjectReader.ReadEnumOrDefault(value, "qos", input.QualityOfService)
                    : ExpressionObjectReader.ReadEnumOrDefault(value, "qualityOfService", input.QualityOfService),
            Retain = ExpressionObjectReader.ReadBoolOrDefault(value, "retain", input.Retain)
        };
}
