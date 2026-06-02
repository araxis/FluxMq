using FluxFlow.Components.Assertions.Contracts;
using FluxFlow.Components.Control.Contracts;
using FluxFlow.Engine.Mapping;
using AssertionNodeContext = FluxFlow.Components.Assertions.Contracts.AssertionNodeContext;

namespace FluxMq.Components.Control;

public sealed class FluxMqControlContextFactory : IControlContextFactory, IAssertionContextFactory
{
    public FlowMapContext Create(object? input, ControlNodeContext context)
        => CreateContext(input, context.InputType.Name);

    public FlowMapContext Create(object? input, AssertionNodeContext context)
        => CreateContext(input, context.InputType.Name);

    private static FlowMapContext CreateContext(object? input, string inputTypeName)
        => input is null
            ? new FlowMapContext
            {
                Variables = new Dictionary<string, object?>
                {
                    ["input"] = null,
                    ["value"] = null,
                    ["inputType"] = inputTypeName
                }
            }
            : FluxMqControlExpressionContextFactory.Create(input);
}
