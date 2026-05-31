using FluxMq.Pipeline.Scenarios;
using FluxMq.UI.Models;
using MudBlazor;

namespace FluxMq.UI.Services;

public sealed class ScenarioStepCatalog
{
    public const string ConnectionKey = ScenarioStepConfigurationKeys.Connection;
    public const string TopicKey = ScenarioStepConfigurationKeys.Topic;
    public const string PayloadKey = ScenarioStepConfigurationKeys.Payload;
    public const string PayloadEncodingKey = ScenarioStepConfigurationKeys.PayloadEncoding;
    public const string QosKey = ScenarioStepConfigurationKeys.Qos;
    public const string RetainKey = ScenarioStepConfigurationKeys.Retain;
    public const string SubscriptionsKey = ScenarioStepConfigurationKeys.Subscriptions;
    public const string ReceiveRetainedKey = ScenarioStepConfigurationKeys.ReceiveRetained;
    public const string RetainAsPublishedKey = ScenarioStepConfigurationKeys.RetainAsPublished;

    public static ScenarioStepCatalog Shared { get; } = new();

    private static readonly IReadOnlyList<ScenarioStepFieldOption> PayloadEncodingOptions =
    [
        new("json", "JSON"),
        new("text", "Text"),
        new("base64", "Base64"),
        new("bytes", "Bytes")
    ];

    private static readonly IReadOnlyList<ScenarioStepFieldOption> QosOptions =
    [
        new("0", "0"),
        new("1", "1"),
        new("2", "2")
    ];

    private readonly IReadOnlyList<ScenarioStepDescriptor> _steps =
    [
        new(
            ScenarioStepTypes.MqttPublisher,
            "MQTT publisher",
            "Action",
            "Publish a message through an app broker.",
            Icons.Material.Filled.Send,
            "publishMessage",
            ScenarioStepEditorKind.MqttPublish,
            [
                new(ConnectionKey, "Broker", ScenarioStepFieldKind.Connection, string.Empty, []),
                new(TopicKey, "Topic", ScenarioStepFieldKind.Text, "fluxmq/test", []),
                new(PayloadKey, "Payload", ScenarioStepFieldKind.MultilineText, """{"hello":"fluxmq"}""", [], 6),
                new(PayloadEncodingKey, "Payload encoding", ScenarioStepFieldKind.Select, "json", PayloadEncodingOptions),
                new(QosKey, "QoS", ScenarioStepFieldKind.Select, "0", QosOptions),
                new(RetainKey, "Retain", ScenarioStepFieldKind.CheckBox, "false", [])
            ]),
        new(
            ScenarioStepTypes.MqttTrigger,
            "MQTT trigger",
            "Action",
            "Listen for MQTT messages through an app broker.",
            Icons.Material.Filled.Sensors,
            "triggerMqtt",
            ScenarioStepEditorKind.MqttTrigger,
            [
                new(ConnectionKey, "Broker", ScenarioStepFieldKind.Connection, string.Empty, []),
                new(SubscriptionsKey, "Topic filter", ScenarioStepFieldKind.Text, "fluxmq/test/#", []),
                new(QosKey, "QoS", ScenarioStepFieldKind.Select, "1", QosOptions),
                new(ReceiveRetainedKey, "Receive retained", ScenarioStepFieldKind.CheckBox, "false", []),
                new(RetainAsPublishedKey, "Retain as published", ScenarioStepFieldKind.CheckBox, "true", [])
            ]),
        new(
            ScenarioStepTypes.WhenEvent,
            "When event",
            "Condition",
            "Continue only when a scenario or app event matches configured filters.",
            Icons.Material.Filled.AltRoute,
            "whenEvent",
            ScenarioStepEditorKind.ExpectEvent,
            []),
        new(
            ScenarioStepTypes.ExpectEvent,
            "Expect event",
            "Expectation",
            "Wait for a scenario or app event that matches configured filters.",
            Icons.Material.Filled.Rule,
            "expectEvent",
            ScenarioStepEditorKind.ExpectEvent,
            [])
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
            ScenarioStepEditorKind.ExpectEvent,
            []);

    public IReadOnlyDictionary<string, string> CreateDefaultConfiguration(
        string? type,
        string? defaultConnection = null)
    {
        var descriptor = Find(type);
        if (descriptor is null)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var configuration = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in descriptor.Fields)
        {
            configuration[field.Key] = field.Kind == ScenarioStepFieldKind.Connection &&
                                       !string.IsNullOrWhiteSpace(defaultConnection)
                ? defaultConnection
                : field.DefaultValue;
        }

        return configuration;
    }
}
