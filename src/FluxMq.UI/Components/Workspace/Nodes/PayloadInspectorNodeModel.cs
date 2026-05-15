using FluxMq.UI.Components.Diagram;
using FluxMq.UI.Models;
using DiagramPoint = Blazor.Diagrams.Core.Geometry.Point;

namespace FluxMq.UI.Components.Workspace.Nodes;

public sealed class PayloadInspectorNodeModel(DiagramPoint position, string nodeName, FlowComponentDescriptor? descriptor, bool isResource)
    : FlowDiagramNodeModel(position, nodeName, "mqtt.payload-inspector", descriptor, isResource);
