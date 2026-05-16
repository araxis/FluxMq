using FluxMq.UI.Components.Diagram;
using FluxMq.UI.Components.Workspace.Nodes.MetricNode;
using FluxMq.UI.Components.Workspace.Nodes.MqttTrigger;
using FluxMq.UI.Components.Workspace.Nodes.PayloadInspector;
using FluxMq.UI.Components.Workspace.Nodes.SessionSource;
using FluxMq.UI.Models;
using DiagramPoint = Blazor.Diagrams.Core.Geometry.Point;

namespace FluxMq.UI.Services;

public static class FlowNodeModelFactory
{
    public static FlowDiagramNodeModel Create(
        string id,
        DiagramPoint position,
        string nodeName,
        string nodeType,
        FlowComponentDescriptor? descriptor,
        bool isResource) => nodeType switch
    {
        "mqtt.trigger" => new MqttTriggerNodeModel(id, position, nodeName, descriptor, isResource),
        "session.source" => new SessionSourceNodeModel(id, position, nodeName, descriptor, isResource),
        "mqtt.payload-inspector" => new PayloadInspectorNodeModel(id, position, nodeName, descriptor, isResource),
        "mqtt.metrics-sink" => new MetricsSinkNodeModel(id, position, nodeName, descriptor, isResource),
        _ => new FlowDiagramNodeModel(id, position, nodeName, nodeType, descriptor, isResource)
    };
}
