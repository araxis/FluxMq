using DynamicExpresso;

namespace FluxMq.Pipeline.Mapping;

public sealed class DynamicExpressoFlowExpressionEngine : IFlowExpressionEngine
{
    public string Name => "dynamic-expresso";

    public object? Evaluate(string expression, FlowMapContext context, Type resultType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(resultType);

        var interpreter = new Interpreter();
        foreach (var (name, value) in context.Variables)
        {
            interpreter.SetVariable(name, value, value?.GetType() ?? typeof(object));
        }

        return interpreter.Eval(expression, resultType);
    }
}
