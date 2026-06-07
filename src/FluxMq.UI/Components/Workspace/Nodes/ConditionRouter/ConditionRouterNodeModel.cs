using FluxMq.UI.Components.Diagram;
using FluxMq.UI.Models;
using System.Text.Json.Nodes;
using DiagramPoint = Blazor.Diagrams.Core.Geometry.Point;

namespace FluxMq.UI.Components.Workspace.Nodes.ConditionRouter;

public sealed class ConditionRouterNodeModel(string id, DiagramPoint position, string nodeName, FlowComponentDescriptor? descriptor, bool isResource)
    : FlowDiagramNodeModel(id, position, nodeName, "flow.when", descriptor, isResource)
{
    public static readonly IReadOnlyList<string> InputTypes =
    [
        "MqttEnvelope",
        "NumberMetricReading"
    ];

    public string InputType { get; set; } = "MqttEnvelope";

    public string Expression { get; set; } = string.Empty;

    protected override void OnConfigurationLoaded(JsonObject? config)
    {
        InputType = NormalizeInputType(config?["inputType"]?.GetValue<string?>());
        Expression = config?["expression"]?.GetValue<string?>() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(Expression))
        {
            Expression = DefaultExpression(InputType);
        }
    }

    public override JsonObject BuildConfiguration()
        => new()
        {
            ["inputType"] = NormalizeInputType(InputType),
            ["expression"] = string.IsNullOrWhiteSpace(Expression)
                ? DefaultExpression(InputType)
                : Expression.Trim(),
            ["boundedCapacity"] = 1000
        };

    public override string ResolvePortValueType(ComponentPortDescriptor descriptor)
    {
        if (string.Equals(descriptor.Name, "Input", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(descriptor.Name, "WhenTrue", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(descriptor.Name, "WhenFalse", StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeInputType(InputType);
        }

        return base.ResolvePortValueType(descriptor);
    }

    public static string NormalizeInputType(string? value)
    {
        var trimmed = value?.Trim();
        return InputTypes.Contains(trimmed, StringComparer.Ordinal)
            ? trimmed!
            : "MqttEnvelope";
    }

    public static string DefaultExpression(string? inputType)
        => NormalizeInputType(inputType) switch
        {
            "NumberMetricReading" => "value > 10",
            _ => "qos >= 1"
        };
}
