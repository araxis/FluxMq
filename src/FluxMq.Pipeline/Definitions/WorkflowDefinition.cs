namespace FluxMq.Pipeline.Definitions;

public class WorkflowDefinition
{
    public required WorkflowName Name { get; init; }
    public IEnumerable<NodeDefinition> Nodes { get; init; } = [];
}