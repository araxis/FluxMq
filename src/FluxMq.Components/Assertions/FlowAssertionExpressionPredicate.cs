using FluxMq.Pipeline.Mapping;

namespace FluxMq.Components.Assertions;

public sealed class FlowAssertionExpressionPredicate<TInput> : IFlowPredicate<TInput>
{
    private readonly IFlowExpressionEngine _engine;
    private readonly string _expression;

    public FlowAssertionExpressionPredicate(IFlowExpressionEngine engine, string expression)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        _expression = expression;
    }

    public bool IsMatch(TInput input)
        => _engine.Evaluate<bool>(_expression, FlowAssertionContextFactory.Create(input));
}
