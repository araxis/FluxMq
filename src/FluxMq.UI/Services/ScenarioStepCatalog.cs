using FluxMq.Pipeline.Scenarios;
using FluxMq.UI.Models;
using MudBlazor;

namespace FluxMq.UI.Services;

public sealed class ScenarioStepCatalog
{
    public const string ConnectionKey = "connection";
    public const string TopicKey = "topic";
    public const string PayloadKey = "payload";
    public const string PayloadEncodingKey = "payloadEncoding";
    public const string QosKey = "qos";
    public const string RetainKey = "retain";

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
            ScenarioStepTypes.ExpectEvent,
            "Expect event",
            "Expectation",
            "Wait for a runtime event that matches configured filters.",
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
