using Blazor.Diagrams.Core.Models;
using Blazor.Diagrams.Core.Models.Base;
using DiagramLine = Blazor.Diagrams.Core.Geometry.Line;
using DiagramPoint = Blazor.Diagrams.Core.Geometry.Point;
using DiagramShape = Blazor.Diagrams.Core.Geometry.IShape;

namespace FluxMq.UI.Components.Diagram;

public sealed class FlowPortModel(
    NodeModel parent,
    PortAlignment alignment,
    string portName,
    bool singleLink = false,
    bool? representsInput = null)
    : PortModel(parent, alignment)
{
    public string PortName { get; } = portName;
    public bool SingleLink { get; } = singleLink;
    public bool RepresentsInput { get; } = representsInput ?? alignment == PortAlignment.Left;
    public bool IsPulsing { get; set; }

    public override DiagramShape GetShape() => new FlowPortShape(this);

    public override bool CanAttachTo(ILinkable linkable)
    {
        if (SingleLink && Links.Count > 0) return false;
        return linkable is not FlowPortModel { SingleLink: true, Links.Count: > 0 } && base.CanAttachTo(linkable);
    }

    private sealed class FlowPortShape(PortModel port) : DiagramShape
    {
        public IEnumerable<DiagramPoint> GetIntersectionsWithLine(DiagramLine line) => [port.MiddlePosition];

        public DiagramPoint? GetPointAtAngle(double angle) => port.MiddlePosition;
    }
}
