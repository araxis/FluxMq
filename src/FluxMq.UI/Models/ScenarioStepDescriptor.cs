using FluxMq.Scenarios;

namespace FluxMq.UI.Models;

public enum ScenarioStepEditorKind
{
    ExpectEvent,
    MqttPublish,
    MqttTrigger,
    Delay,
    MetricThreshold,
    Cleanup
}

public sealed record ScenarioStepDescriptor(
    string Type,
    string DisplayName,
    string Category,
    string Description,
    string Icon,
    string NamePrefix,
    ScenarioStepEditorKind EditorKind,
    IReadOnlyList<ScenarioStepFieldDescriptor> Fields,
    string DefaultPhase);
