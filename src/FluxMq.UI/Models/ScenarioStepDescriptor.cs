using FluxMq.Scenarios;

namespace FluxMq.UI.Models;

public enum ScenarioStepEditorKind
{
    ExpectEvent,
    MqttPublish,
    MqttTrigger
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
