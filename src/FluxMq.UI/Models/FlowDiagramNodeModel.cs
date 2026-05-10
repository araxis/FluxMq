using Blazor.Diagrams.Core.Models;
using System.Text.Json.Nodes;
using DiagramPoint = Blazor.Diagrams.Core.Geometry.Point;
using DiagramSize = Blazor.Diagrams.Core.Geometry.Size;

namespace FluxMq.UI.Models;

public sealed class FlowDiagramNodeModel : NodeModel
{
    private static readonly DiagramSize ExpandedSize = new(280, 240);
    private static readonly DiagramSize CollapsedSize = new(280, 82);

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
        // Nodes default to collapsed — the user can flip / expand on demand.
        IsCollapsed = true;
        Size = CollapsedSize;
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
