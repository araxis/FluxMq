using FluxMq.UI.Components.Diagram;
using FluxMq.UI.Models;
using DiagramPoint = Blazor.Diagrams.Core.Geometry.Point;

namespace FluxMq.UI.Components.Workspace.Nodes.MetricNode;

public sealed class MetricsSinkNodeModel(DiagramPoint position, string nodeName, FlowComponentDescriptor? descriptor, bool isResource)
    : FlowDiagramNodeModel(position, nodeName, "mqtt.metrics-sink", descriptor, isResource);
