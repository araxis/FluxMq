using System.Text.Json;

namespace FluxMq.Pipeline.Definitions;

public sealed record ScenarioDefinition
{
    public Dictionary<string, ScenarioStepDefinition> Steps { get; init; } = [];
}

public sealed record ScenarioStepDefinition
{
    public string Type { get; init; } = string.Empty;
    public Dictionary<string, JsonElement> Configuration { get; init; } = [];
}
