using FluxMq.Pipeline.Definitions;

namespace FluxMq.Pipeline.Runtime;

public sealed record FlowRuntimeNodeFactoryContext(
    FlowNodeName Name,
    FlowNodeDefinition Definition,
    string? WorkflowName)
{
    public bool IsResource => WorkflowName is null;
}
