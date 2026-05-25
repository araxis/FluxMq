using FluxMq.Pipeline.Scenarios;
using FluxMq.UI.Models;
using MudBlazor;

namespace FluxMq.UI.Services;

public sealed class ScenarioStepCatalog
{
    public static ScenarioStepCatalog Shared { get; } = new();

    private readonly IReadOnlyList<ScenarioStepDescriptor> _steps =
    [
        new(
            ScenarioStepTypes.MqttPublish,
            "MQTT publish",
            "Action",
            "Publish a message through an app broker.",
            Icons.Material.Filled.Send,
            "publishMessage",
            ScenarioStepEditorKind.MqttPublish),
        new(
            ScenarioStepTypes.ExpectEvent,
            "Expect event",
            "Expectation",
            "Wait for a runtime event that matches configured filters.",
            Icons.Material.Filled.Rule,
            "expectEvent",
            ScenarioStepEditorKind.ExpectEvent)
    ];

    public IReadOnlyList<ScenarioStepDescriptor> Steps => _steps;

    public ScenarioStepDescriptor? Find(string? type)
        => _steps.FirstOrDefault(step => string.Equals(step.Type, type, StringComparison.Ordinal));

    public ScenarioStepDescriptor Describe(string? type)
        => Find(type) ?? new ScenarioStepDescriptor(
            type ?? string.Empty,
            string.IsNullOrWhiteSpace(type) ? "Untyped" : type,
            "Custom",
            "Custom test step.",
            Icons.Material.Filled.Extension,
            "step",
            ScenarioStepEditorKind.ExpectEvent);
}
