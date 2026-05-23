using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Components.Mapping;
using FluxMq.Pipeline.Mapping;

namespace FluxMq.Components.Replay;

public sealed class MqttRecordingRequestExpressionMapper : IFlowMapper<MqttEnvelope, MqttRecordingRequest>
{
    private readonly IFlowExpressionEngine _engine;
    private readonly string _expression;

    public MqttRecordingRequestExpressionMapper(IFlowExpressionEngine engine, string expression)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _expression = string.IsNullOrWhiteSpace(expression)
            ? throw new ArgumentException("Recording request mapper expression must not be empty.", nameof(expression))
            : expression;
    }

    public MqttRecordingRequest Map(MqttEnvelope input, FlowMapContext context)
    {
        var value = _engine.Evaluate(_expression, context, typeof(object));
        return value switch
        {
            MqttRecordingRequest request => request,
            null => throw new InvalidOperationException("Mapper expression returned null. Expected MqttRecordingRequest object."),
            _ => CoerceRequest(value, input)
        };
    }

    private static MqttRecordingRequest CoerceRequest(object value, MqttEnvelope input)
        => new()
        {
            SessionId = new SessionId(ExpressionObjectReader.ReadRequiredGuid(value, "sessionId")),
            Envelope = input
        };
}
