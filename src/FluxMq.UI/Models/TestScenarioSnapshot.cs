namespace FluxMq.UI.Models;

public sealed record TestScenarioSnapshot(
    string Name,
    IReadOnlyList<ScenarioStepSnapshot> Steps)
{
    public TestScenarioSnapshot(
        string name,
        IReadOnlyList<ScenarioPhaseSnapshot> phases)
        : this(name, phases.SelectMany(static phase => phase.Steps).ToArray())
    {
        Phases = phases;
    }

    public IReadOnlyList<ScenarioPhaseSnapshot> Phases { get; init; } =
    [
        new("main", "Scenario", "main", Steps)
    ];

    public int StepCount => Steps.Count;
}

public sealed record ScenarioPhaseSnapshot(
    string Name,
    string Title,
    string Kind,
    IReadOnlyList<ScenarioStepSnapshot> Steps);

public sealed record ScenarioStepSnapshot(
    string Name,
    string Type,
    IReadOnlyDictionary<string, string> Configuration)
{
    public string? ReadString(string key)
        => Configuration.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
}
