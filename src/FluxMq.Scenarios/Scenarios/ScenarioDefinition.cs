using System.Text.Json;
using System.Text.Json.Serialization;

namespace FluxMq.Scenarios;

public sealed record ScenarioDefinition
{
    private Dictionary<string, ScenarioPhaseDefinition>? _phases = [];
    private Dictionary<string, ScenarioStepDefinition>? _steps = [];
    private ScenarioRunProfileDefinition? _runProfile = new();
    private List<string>? _runHistory = [];
    private List<ScenarioReportSnapshotDefinition>? _reportSnapshots = [];

    public int Version { get; init; } = 2;

    public Dictionary<string, ScenarioPhaseDefinition> Phases
    {
        get => _phases ??= [];
        init => _phases = value ?? [];
    }

    public Dictionary<string, ScenarioStepDefinition> Steps
    {
        get => _steps ??= [];
        init => _steps = value ?? [];
    }

    public ScenarioRunProfileDefinition RunProfile
    {
        get => _runProfile ??= new();
        init => _runProfile = value ?? new ScenarioRunProfileDefinition();
    }

    public List<string> RunHistory
    {
        get => _runHistory ??= [];
        init => _runHistory = value ?? [];
    }

    public List<ScenarioReportSnapshotDefinition> ReportSnapshots
    {
        get => _reportSnapshots ??= [];
        init => _reportSnapshots = value ?? [];
    }

    [JsonIgnore]
    public bool HasPhaseSteps => Phases.Any(static phase => phase.Value.Steps.Count > 0);

    public IEnumerable<KeyValuePair<string, ScenarioStepDefinition>> EnumerateSteps()
    {
        if (HasPhaseSteps)
        {
            foreach (var phase in Phases.Values.OrderBy(static phase => phase.Order))
            {
                foreach (var step in phase.Steps)
                {
                    yield return step;
                }
            }

            yield break;
        }

        foreach (var step in Steps)
        {
            yield return step;
        }
    }
}

public sealed record ScenarioPhaseDefinition
{
    private Dictionary<string, ScenarioStepDefinition>? _steps = [];

    public string Title { get; init; } = string.Empty;
    public string Kind { get; init; } = ScenarioPhaseKinds.Stimulus;
    public int Order { get; init; }

    public Dictionary<string, ScenarioStepDefinition> Steps
    {
        get => _steps ??= [];
        init => _steps = value ?? [];
    }
}

public sealed record ScenarioRunProfileDefinition
{
    public string Mode { get; init; } = "local";
    public int TimeoutMs { get; init; } = 30000;
    public bool StopOnFailure { get; init; } = true;
}

public sealed record ScenarioReportSnapshotDefinition
{
    public string RunId { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? Summary { get; init; }
}

public static class ScenarioPhaseKinds
{
    public const string Setup = "setup";
    public const string Stimulus = "stimulus";
    public const string Observe = "observe";
    public const string Assert = "assert";
    public const string Cleanup = "cleanup";
    public const string Imported = "imported";
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
