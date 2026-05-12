using FluxMq.Pipeline.Definitions;

namespace FluxMq.Pipeline.Runtime;

public class Workflow
{
    public required WorkflowName Name { get; init; }
    public IEnumerable<RuntimeNode> Nodes { get; init; } = [];
}