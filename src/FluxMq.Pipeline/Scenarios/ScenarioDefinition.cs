using System.Text.Json;

namespace FluxMq.Pipeline.Scenarios;

public sealed record ScenarioDefinition
{
    private Dictionary<string, ScenarioStepDefinition>? _steps = [];

    public Dictionary<string, ScenarioStepDefinition> Steps
    {
        get => _steps ??= [];
        init => _steps = value ?? [];
    }
}

public sealed record ScenarioStepDefinition
{
    private Dictionary<string, JsonElement>? _configuration = [];

    public string Type { get; init; } = string.Empty;

    public Dictionary<string, JsonElement> Configuration
    {
        get => _configuration ??= [];
        init => _configuration = value ?? [];
    }
}
