using FluxMq.UI.Components.Diagram;
using FluxMq.UI.Models;
using System.Text.Json.Nodes;
using DiagramPoint = Blazor.Diagrams.Core.Geometry.Point;

namespace FluxMq.UI.Components.Workspace.Nodes.ConditionRouter;

public sealed class ConditionRouterNodeModel(string id, DiagramPoint position, string nodeName, FlowComponentDescriptor? descriptor, bool isResource)
    : FlowDiagramNodeModel(id, position, nodeName, "mqtt.condition-router", descriptor, isResource)
{
    public string Expression { get; set; } = string.Empty;

    protected override void OnConfigurationLoaded(JsonObject? config)
    {
        Expression = config?["expression"]?.GetValue<string?>() ?? string.Empty;
    }

    public override JsonObject BuildConfiguration()
        => new() { ["expression"] = Expression };
}
