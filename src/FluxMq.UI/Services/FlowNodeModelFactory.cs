using FluxMq.UI.Components.Diagram;
using FluxMq.UI.Components.Workspace.Nodes.MetricNode;
using FluxMq.UI.Components.Workspace.Nodes.MqttConnection;
using FluxMq.UI.Components.Workspace.Nodes.MqttTrigger;
using FluxMq.UI.Components.Workspace.Nodes.PayloadInspector;
using FluxMq.UI.Components.Workspace.Nodes.SessionSource;
using FluxMq.UI.Models;
using DiagramPoint = Blazor.Diagrams.Core.Geometry.Point;

namespace FluxMq.UI.Services;

public static class FlowNodeModelFactory
{
    public static FlowDiagramNodeModel Create(
        DiagramPoint position,
        string nodeName,
        string nodeType,
        FlowComponentDescriptor? descriptor,
        bool isResource) => nodeType switch
    {
        "mqtt.trigger" => new MqttTriggerNodeModel(position, nodeName, descriptor, isResource),
        "mqtt.connection" => new MqttConnectionNodeModel(position, nodeName, descriptor, isResource),
        "session.source" => new SessionSourceNodeModel(position, nodeName, descriptor, isResource),
        "mqtt.payload-inspector" => new PayloadInspectorNodeModel(position, nodeName, descriptor, isResource),
        "mqtt.metrics-sink" => new MetricsSinkNodeModel(position, nodeName, descriptor, isResource),
        _ => new FlowDiagramNodeModel(position, nodeName, nodeType, descriptor, isResource)
    };
}
