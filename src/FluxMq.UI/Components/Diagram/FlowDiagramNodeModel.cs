using Blazor.Diagrams.Core.Models;
using FluxMq.UI.Models;
using System.Text.Json.Nodes;
using DiagramPoint = Blazor.Diagrams.Core.Geometry.Point;
using DiagramSize = Blazor.Diagrams.Core.Geometry.Size;

namespace FluxMq.UI.Components.Diagram;

public class FlowDiagramNodeModel : NodeModel
{
    // port section height: border(1) + padding(10) + margin-top(8) - negative-margin-bottom(10) = 9px overhead
    private static int PortSectionHeight(int portCount)
        => portCount > 0 ? 9 + portCount * 26 : 0;

    private static DiagramSize ComputeCollapsedSize(int portCount)
        => new(280, 82 + PortSectionHeight(portCount));

    private static DiagramSize ComputeExpandedSize(int portCount)
        => new(280, 160 + PortSectionHeight(portCount));

    public FlowDiagramNodeModel(
        string id,
        DiagramPoint position,
        string nodeName,
        string nodeType,
        FlowComponentDescriptor? descriptor,
        bool isResource)
        : base(id, position)
    {
        NodeName = nodeName;
        NodeType = nodeType;
        DisplayName = descriptor?.DisplayName ?? nodeType;
        Category = descriptor?.Category ?? (isResource ? "Resource" : "Node");
        Summary = descriptor?.Summary ?? "Configuration-defined flow node.";
        IsResource = isResource;
        PortDescriptors = descriptor?.Ports ?? [];
        ControlledSize = true;
        IsCollapsed = true;
        RefreshControlledSize();
    }

    public string NodeName { get; }
    public string NodeType { get; }
    public string DisplayName { get; }
    public string Category { get; }
    public string Summary { get; }
    public bool IsResource { get; }
    public IReadOnlyList<ComponentPortDescriptor> PortDescriptors { get; private set; }
    public bool IsCollapsed { get; private set; }
    public string? ActivityText { get; private set; }
    public IReadOnlyList<WorkspaceDiagnostic> Diagnostics { get; private set; } = [];
    public WorkspaceDiagnostic? PrimaryDiagnostic => Diagnostics
        .OrderByDescending(DiagnosticSeverityRank)
        .ThenBy(static diagnostic => diagnostic.Source, StringComparer.Ordinal)
        .ThenBy(static diagnostic => diagnostic.Code, StringComparer.Ordinal)
        .FirstOrDefault();

    /// <summary>Called after the node is created to parse the JSON configuration.</summary>
    public void LoadConfiguration(JsonObject? config) => OnConfigurationLoaded(config);

    /// <summary>Override to read typed properties from the JSON configuration object.</summary>
    protected virtual void OnConfigurationLoaded(JsonObject? config) { }

    /// <summary>Override to serialise typed properties back to a JSON configuration object on save.</summary>
    public virtual JsonObject BuildConfiguration() => [];

    public virtual string ResolvePortValueType(ComponentPortDescriptor descriptor)
        => descriptor.ValueType;

    protected void SetPortDescriptors(IReadOnlyList<ComponentPortDescriptor> descriptors)
    {
        PortDescriptors = descriptors;
        RefreshControlledSize();
    }

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
        RefreshControlledSize();
        RefreshAll();
    }

    private void RefreshControlledSize()
        => Size = IsCollapsed
            ? ComputeCollapsedSize(PortDescriptors.Count)
            : ComputeExpandedSize(PortDescriptors.Count);

    public void SetActivity(string? activityText)
    {
        if (ActivityText == activityText)
        {
            return;
        }

        ActivityText = activityText;
        Refresh();
    }

    public void SetDiagnostics(IEnumerable<WorkspaceDiagnostic> diagnostics)
    {
        var next = diagnostics.ToArray();
        if (Diagnostics.SequenceEqual(next))
        {
            return;
        }

        Diagnostics = next;
        Refresh();
    }

    private static int DiagnosticSeverityRank(WorkspaceDiagnostic diagnostic)
        => diagnostic.Severity switch
        {
            "Error" => 3,
            "Warning" => 2,
            "Info" => 1,
            _ => 0
        };
}
