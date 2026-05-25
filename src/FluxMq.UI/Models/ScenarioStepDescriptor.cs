namespace FluxMq.UI.Models;

public enum ScenarioStepEditorKind
{
    ExpectEvent,
    MqttPublish
}

public sealed record ScenarioStepDescriptor(
    string Type,
    string DisplayName,
    string Category,
    string Description,
    string Icon,
    string NamePrefix,
    ScenarioStepEditorKind EditorKind);
