namespace FluxMq.Scenarios;

public enum ScenarioStepFieldKind
{
    Text,
    MultilineText,
    Select,
    CheckBox,
    Connection
}

public sealed record ScenarioStepFieldOption(
    string Value,
    string Label);

public sealed record ScenarioStepFieldDescriptor(
    string Key,
    string Label,
    ScenarioStepFieldKind Kind,
    string DefaultValue,
    IReadOnlyList<ScenarioStepFieldOption>? Options = null,
    int Lines = 1)
{
    public IReadOnlyList<ScenarioStepFieldOption> Options { get; init; } = Options ?? [];
}
