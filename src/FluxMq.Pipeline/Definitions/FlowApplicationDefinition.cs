namespace FluxMq.Pipeline.Definitions;

public sealed record FlowApplicationDefinition
{
    public Dictionary<string, FlowNodeDefinition> Resources { get; init; } = [];
    public Dictionary<string, Dictionary<string, FlowNodeDefinition>> Workflows { get; init; } = [];
}
