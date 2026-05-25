namespace FluxMq.UI.Models;

public sealed record TestScenarioSnapshot(
    string Name,
    IReadOnlyList<ScenarioStepSnapshot> Steps)
{
    public int StepCount => Steps.Count;
}

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
