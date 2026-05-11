using Blazor.Diagrams.Core.Models;
using System.Text.Json.Nodes;
using DiagramPoint = Blazor.Diagrams.Core.Geometry.Point;
using DiagramSize = Blazor.Diagrams.Core.Geometry.Size;

namespace FluxMq.UI.Models;

public sealed class FlowDiagramNodeModel : NodeModel
{
    private readonly DiagramSize _collapsedSize;
    private readonly DiagramSize _expandedSize;

    // port section height: border(1) + padding(10) + margin-top(8) - negative-margin-bottom(10) = 9px overhead
    private static int PortSectionHeight(int portCount)
        => portCount > 0 ? 9 + portCount * 26 : 0;

    private static DiagramSize ComputeCollapsedSize(int portCount)
        => new(280, 82 + PortSectionHeight(portCount));

    private static DiagramSize ComputeExpandedSize(int portCount)
        => new(280, 160 + PortSectionHeight(portCount));

    public FlowDiagramNodeModel(
        DiagramPoint position,
        string nodeName,
        string nodeType,
        FlowComponentDescriptor? descriptor,
        bool isResource)
        : base(position)
    {
        NodeName = nodeName;
        NodeType = nodeType;
        DisplayName = descriptor?.DisplayName ?? nodeType;
        Category = descriptor?.Category ?? (isResource ? "Resource" : "Node");
        Summary = descriptor?.Summary ?? "Configuration-defined flow node.";
        IsResource = isResource;
        PortDescriptors = descriptor?.Ports ?? [];
        _collapsedSize = ComputeCollapsedSize(PortDescriptors.Count);
        _expandedSize = ComputeExpandedSize(PortDescriptors.Count);
        ControlledSize = true;
        IsCollapsed = true;
        Size = _collapsedSize;
    }

    public string NodeName { get; }
    public string NodeType { get; }
    public string DisplayName { get; }
    public string Category { get; }
    public string Summary { get; }
    public bool IsResource { get; }
    public IReadOnlyList<ComponentPortDescriptor> PortDescriptors { get; }
    public bool IsCollapsed { get; private set; }
    public string? ActivityText { get; private set; }
    /// <summary>
    /// The node's current configuration JSON object, kept in sync by the
    /// designer when the definition is rebuilt. Read by per-node editors.
    /// </summary>
    public JsonObject? Configuration { get; set; }

    public void Toggle()
    {
        SetCollapsed(!IsCollapsed);
    }

    public void SetCollapsed(bool isCollapsed)
    {
        if (IsCollapsed == isCollapsed)
        {
            return;
        }

        IsCollapsed = isCollapsed;
        Size = IsCollapsed ? _collapsedSize : _expandedSize;
        RefreshAll();
    }

    public void SetActivity(string? activityText)
    {
        if (ActivityText == activityText)
        {
            return;
        }

        ActivityText = activityText;
        Refresh();
    }
}
