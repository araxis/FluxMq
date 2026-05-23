using FluxMq.Core.Models;
using FluxMq.Pipeline.Mapping;
using MQTTnet.Protocol;
using System.Text;

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
    }

    public MqttPublishRequest Map(MqttEnvelope input, FlowMapContext context)
    {
        return new MqttPublishRequest
        {
            Topic = EvaluateString(_definition.TopicExpression, context, input.Topic),
            Payload = EvaluatePayload(_definition.PayloadExpression, context, input.Payload),
            QualityOfService = EvaluateQualityOfService(_definition.QualityOfServiceExpression, context, input.QualityOfService),
            Retain = EvaluateBool(_definition.RetainExpression, context, input.Retain)
        };
    }

    private string EvaluateString(string? expression, FlowMapContext context, string fallback)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return fallback;
        }

        return _engine.Evaluate<string>(expression, context);
    }

    private byte[] EvaluatePayload(string? expression, FlowMapContext context, byte[] fallback)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return fallback;
        }

        var value = _engine.Evaluate(expression, context, typeof(object));
        return value switch
        {
            byte[] bytes => bytes,
            string text => Encoding.UTF8.GetBytes(text),
            null => [],
            _ => throw new InvalidOperationException(
                $"Payload expression returned unsupported value type '{value.GetType().FullName}'. Expected byte[] or string.")
        };
    }

    private MqttQualityOfServiceLevel EvaluateQualityOfService(
        string? expression,
        FlowMapContext context,
        MqttQualityOfServiceLevel fallback)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return fallback;
        }

        var value = _engine.Evaluate(expression, context, typeof(object));
        return value switch
        {
            MqttQualityOfServiceLevel qos => qos,
            int qos => ParseQualityOfService(qos),
            long qos => ParseQualityOfService(checked((int)qos)),
            short qos => ParseQualityOfService(qos),
            byte qos => ParseQualityOfService(qos),
            string qos => ParseQualityOfService(qos),
            _ => throw new InvalidOperationException(
                $"QoS expression returned unsupported value type '{value?.GetType().FullName ?? "null"}'. Expected QoS enum, number, or string.")
        };
    }

    private bool EvaluateBool(string? expression, FlowMapContext context, bool fallback)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return fallback;
        }

        return _engine.Evaluate<bool>(expression, context);
    }

    private static MqttQualityOfServiceLevel ParseQualityOfService(int value)
        => value switch
        {
            0 => MqttQualityOfServiceLevel.AtMostOnce,
            1 => MqttQualityOfServiceLevel.AtLeastOnce,
            2 => MqttQualityOfServiceLevel.ExactlyOnce,
            _ => throw new InvalidOperationException("QoS expression must return 0, 1, or 2.")
        };

    private static MqttQualityOfServiceLevel ParseQualityOfService(string value)
    {
        if (int.TryParse(value, out var number))
        {
            return ParseQualityOfService(number);
        }

        if (Enum.TryParse<MqttQualityOfServiceLevel>(value, ignoreCase: true, out var qos))
        {
            return qos;
        }

        throw new InvalidOperationException("QoS expression string must be 0, 1, 2, AtMostOnce, AtLeastOnce, or ExactlyOnce.");
    }
}
