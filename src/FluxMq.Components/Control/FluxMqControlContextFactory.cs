using FluxFlow.Components.Control.Contracts;
using FluxFlow.Engine.Mapping;

namespace FluxMq.Components.Control;

public sealed class FluxMqControlContextFactory : IControlContextFactory
{
    public FlowMapContext Create(object? input, ControlNodeContext context)
        => input is null
            ? new FlowMapContext
            {
                Variables = new Dictionary<string, object?>
                {
                    ["input"] = null,
                    ["value"] = null,
                    ["inputType"] = context.InputType.Name
                }
            }
            : FluxMqControlExpressionContextFactory.Create(input);
}
