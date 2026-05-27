namespace FluxMq.UI.Models;

public enum ScenarioStepEditorKind
{
    ExpectEvent,
    MqttPublish
}

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

public sealed record ScenarioStepDescriptor(
    string Type,
    string DisplayName,
    string Category,
    string Description,
    string Icon,
    string NamePrefix,
    ScenarioStepEditorKind EditorKind,
    IReadOnlyList<ScenarioStepFieldDescriptor> Fields);
