using Blazor.Diagrams.Core.Models;
using DiagramPoint = Blazor.Diagrams.Core.Geometry.Point;
using DiagramSize = Blazor.Diagrams.Core.Geometry.Size;

namespace FluxMq.UI.Models;

public sealed class FlowDiagramNodeModel : NodeModel
{
    private static readonly DiagramSize ExpandedSize = new(260, 178);
    private static readonly DiagramSize CollapsedSize = new(260, 82);

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
        ControlledSize = true;
        Size = ExpandedSize;
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

    public void Toggle()
    {
        IsCollapsed = !IsCollapsed;
        Size = IsCollapsed ? CollapsedSize : ExpandedSize;
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
