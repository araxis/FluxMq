using FluxMq.Core.Models;
using FluxMq.Components.Mapping;
using FluxMq.Pipeline.Mapping;

namespace FluxMq.Components.MessageFilter;

public sealed class MqttEnvelopeExpressionPredicate : IFlowPredicate<MqttEnvelope>
{
    private readonly IFlowExpressionEngine _engine;
    private readonly string _expression;

    public MqttEnvelopeExpressionPredicate(IFlowExpressionEngine engine, string expression)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        if (string.IsNullOrWhiteSpace(expression))
        {
            throw new ArgumentException("Expression must not be empty.", nameof(expression));
        }

        _expression = expression;
    }

    public bool IsMatch(MqttEnvelope input)
        => _engine.Evaluate<bool>(_expression, MqttEnvelopeExpressionContextFactory.Create(input));
}
