namespace FluxMq.Pipeline.Definitions;

public sealed record WorkflowDefinition
{
    public Dictionary<string, NodeDefinition> Nodes { get; init; } = [];
}
