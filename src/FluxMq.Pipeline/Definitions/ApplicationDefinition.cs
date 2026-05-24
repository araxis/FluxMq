namespace FluxMq.Pipeline.Definitions;

public sealed record ApplicationDefinition
{
    public Dictionary<string, NodeDefinition> Resources { get; init; } = [];
    public Dictionary<string, WorkflowDefinition> Workflows { get; init; } = [];
    public Dictionary<string, DashboardDefinition> Dashboards { get; init; } = [];
    public Dictionary<string, ScenarioDefinition> Tests { get; init; } = [];
}
