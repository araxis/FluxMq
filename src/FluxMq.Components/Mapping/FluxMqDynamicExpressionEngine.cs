using DynamicExpresso;
using FluxFlow.Engine.Mapping;

namespace FluxMq.Components.Mapping;

public sealed class FluxMqDynamicExpressionEngine : IFlowExpressionEngine
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
            if (value is Type type)
            {
                interpreter.Reference(type);
                continue;
            }

            interpreter.SetVariable(name, value, value?.GetType() ?? typeof(object));
        }

        return interpreter.Eval(expression, resultType);
    }
}
