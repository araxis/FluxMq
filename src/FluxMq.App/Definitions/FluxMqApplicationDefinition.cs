using FluxMq.Pipeline.Definitions;
using EngineApplicationDefinition = FluxFlow.Engine.Definitions.ApplicationDefinition;
using EngineNodeDefinition = FluxFlow.Engine.Definitions.NodeDefinition;
using EngineWorkflowDefinition = FluxFlow.Engine.Definitions.WorkflowDefinition;

namespace FluxMq.App.Definitions;

public sealed record FluxMqApplicationDefinition
{
    private Dictionary<string, EngineNodeDefinition>? _resources = [];
    private Dictionary<string, EngineWorkflowDefinition>? _workflows = [];
    private Dictionary<string, DashboardDefinition>? _dashboards = [];
    private Dictionary<string, ScenarioDefinition>? _tests = [];

    public Dictionary<string, EngineNodeDefinition> Resources
    {
        get => _resources ??= [];
        init => _resources = value ?? [];
    }

    public Dictionary<string, EngineWorkflowDefinition> Workflows
    {
        get => _workflows ??= [];
        init => _workflows = value ?? [];
    }

    public Dictionary<string, DashboardDefinition> Dashboards
    {
        get => _dashboards ??= [];
        init => _dashboards = value ?? [];
    }

    public Dictionary<string, ScenarioDefinition> Tests
    {
        get => _tests ??= [];
        init => _tests = value ?? [];
    }

    public EngineApplicationDefinition ToEngineDefinition()
        => new()
        {
            Resources = new Dictionary<string, EngineNodeDefinition>(Resources, StringComparer.Ordinal),
            Workflows = new Dictionary<string, EngineWorkflowDefinition>(Workflows, StringComparer.Ordinal)
        };

    public static FluxMqApplicationDefinition FromEngineDefinition(EngineApplicationDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return new FluxMqApplicationDefinition
        {
            Resources = new Dictionary<string, EngineNodeDefinition>(definition.Resources, StringComparer.Ordinal),
            Workflows = new Dictionary<string, EngineWorkflowDefinition>(definition.Workflows, StringComparer.Ordinal)
        };
    }
}
