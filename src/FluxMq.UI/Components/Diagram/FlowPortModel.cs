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
    bool? representsInput = null,
    string? valueType = null)
    : PortModel(parent, alignment)
{
    public string PortName { get; } = portName;
    public bool SingleLink { get; } = singleLink;
    public bool RepresentsInput { get; } = representsInput ?? alignment == PortAlignment.Left;
    public string ValueType { get; } = string.IsNullOrWhiteSpace(valueType) ? "Any" : valueType.Trim();
    public bool IsPulsing { get; set; }

    public override DiagramShape GetShape() => new FlowPortShape(this);

    public override bool CanAttachTo(ILinkable linkable)
    {
        if (linkable is not FlowPortModel other)
        {
            return base.CanAttachTo(linkable);
        }

        if (ReferenceEquals(Parent, other.Parent))
        {
            return false;
        }

        if (RepresentsInput == other.RepresentsInput)
        {
            return false;
        }

        if (SingleLink && Links.Count > 0)
        {
            return false;
        }

        if (other.SingleLink && other.Links.Count > 0)
        {
            return false;
        }

        return CanCarryValueTo(other) && base.CanAttachTo(linkable);
    }

    public bool CanCarryValueTo(FlowPortModel other)
    {
        var source = RepresentsInput ? other : this;
        var target = RepresentsInput ? this : other;
        return ValueTypesCompatible(source.ValueType, target.ValueType);
    }

    private static bool ValueTypesCompatible(string sourceType, string targetType)
    {
        if (string.IsNullOrWhiteSpace(sourceType) || string.IsNullOrWhiteSpace(targetType))
        {
            return true;
        }

        if (IsWildcard(sourceType) || IsWildcard(targetType))
        {
            return true;
        }

        return string.Equals(sourceType.Trim(), targetType.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWildcard(string valueType)
        => string.Equals(valueType.Trim(), "Any", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(valueType.Trim(), "Configured output type", StringComparison.OrdinalIgnoreCase);

    private sealed class FlowPortShape(PortModel port) : DiagramShape
    {
        public IEnumerable<DiagramPoint> GetIntersectionsWithLine(DiagramLine line) => [port.MiddlePosition];

        public DiagramPoint? GetPointAtAngle(double angle) => port.MiddlePosition;
    }
}
