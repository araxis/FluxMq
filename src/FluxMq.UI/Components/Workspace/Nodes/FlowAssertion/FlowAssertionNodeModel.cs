using FluxMq.UI.Components.Diagram;
using FluxMq.UI.Models;
using System.Text.Json.Nodes;
using DiagramPoint = Blazor.Diagrams.Core.Geometry.Point;

namespace FluxMq.UI.Components.Workspace.Nodes.FlowAssertion;

public sealed class FlowAssertionNodeModel(string id, DiagramPoint position, string nodeName, FlowComponentDescriptor? descriptor, bool isResource)
    : FlowDiagramNodeModel(id, position, nodeName, "flow.assert", descriptor, isResource)
{
    public const string DefaultAssertionName = "QoS at least once";
    public const string DefaultInputType = "MqttEnvelope";
    public const string DefaultExpression = "qos >= 1";
    public const string DefaultFailureMessage = "Expected QoS to be at least 1.";
    public const int DefaultBoundedCapacity = 1000;

    public static readonly IReadOnlyList<string> InputTypes =
    [
        "MqttEnvelope",
        "MqttPublishRequest",
        "MqttRecordingRequest",
        "FileWriteRequest",
        "JsonSchemaValidationResult",
        "InspectedMqttMessage",
        "MqttMetricsSnapshot",
        "FlowLogEntry",
        "FlowError"
    ];

    public string AssertionName { get; set; } = DefaultAssertionName;
    public string InputType { get; set; } = DefaultInputType;
    public string Expression { get; set; } = DefaultExpression;
    public string FailureMessage { get; set; } = DefaultFailureMessage;
    public int BoundedCapacity { get; set; } = DefaultBoundedCapacity;

    protected override void OnConfigurationLoaded(JsonObject? config)
    {
        AssertionName = ReadString(config, "assertionName", DefaultAssertionName);
        InputType = NormalizeInputType(ReadString(config, "inputType", DefaultInputType));
        Expression = ReadString(config, "expression", DefaultExpression);
        FailureMessage = ReadString(config, "failureMessage", DefaultFailureMessage);
        BoundedCapacity = NormalizeBoundedCapacity(ReadInt(config, "boundedCapacity", DefaultBoundedCapacity));
    }

    public override JsonObject BuildConfiguration()
        => new()
        {
            ["assertionName"] = string.IsNullOrWhiteSpace(AssertionName) ? DefaultAssertionName : AssertionName.Trim(),
            ["inputType"] = NormalizeInputType(InputType),
            ["expression"] = string.IsNullOrWhiteSpace(Expression) ? DefaultExpression : Expression.Trim(),
            ["failureMessage"] = string.IsNullOrWhiteSpace(FailureMessage) ? DefaultFailureMessage : FailureMessage.Trim(),
            ["boundedCapacity"] = NormalizeBoundedCapacity(BoundedCapacity)
        };

    public override string ResolvePortValueType(ComponentPortDescriptor descriptor)
    {
        if (descriptor.Name is "Input" or "Passed" or "Failed")
        {
            return NormalizeInputType(InputType);
        }

        return base.ResolvePortValueType(descriptor);
    }

    public static int NormalizeBoundedCapacity(int boundedCapacity)
        => boundedCapacity > 0 ? boundedCapacity : DefaultBoundedCapacity;

    public static string NormalizeInputType(string? value)
    {
        var trimmed = value?.Trim();
        return InputTypes.Contains(trimmed, StringComparer.Ordinal)
            ? trimmed!
            : DefaultInputType;
    }

    private static string ReadString(JsonObject? config, string key, string fallback)
        => config?[key]?.GetValue<string?>() is { Length: > 0 } value ? value : fallback;

    private static int ReadInt(JsonObject? config, string key, int fallback)
    {
        if (config?[key] is JsonValue value && value.TryGetValue<int>(out var result))
        {
            return result;
        }

        return fallback;
    }
}
