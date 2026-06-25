using FluxMq.UI.Components.Diagram;
using FluxMq.UI.Models;
using FluxMq.UI.Services;
using System.Text.Json.Nodes;
using DiagramPoint = Blazor.Diagrams.Core.Geometry.Point;

namespace FluxMq.UI.Components.Workspace.Nodes.ConnectionStateTrigger;

public sealed class ConnectionStateTriggerNodeModel(string id, DiagramPoint position, string nodeName, FlowComponentDescriptor? descriptor, bool isResource)
    : FlowDiagramNodeModel(id, position, nodeName, "mqtt.connection-state-trigger", descriptor, isResource)
{
    public string Connection { get; set; } = string.Empty;

    protected override void OnConfigurationLoaded(JsonObject? config)
    {
        Connection = config?["connection"]?.GetValue<string?>() ?? string.Empty;
    }

    public override JsonObject BuildConfiguration()
        => new()
        {
            ["connection"] = string.IsNullOrWhiteSpace(Connection) ? FlowDefinitionComposer.BrokerResourceName : Connection.Trim()
        };
}
