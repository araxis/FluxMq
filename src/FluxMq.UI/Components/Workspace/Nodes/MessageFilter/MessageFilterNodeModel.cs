using FluxMq.UI.Components.Diagram;
using FluxMq.UI.Models;
using System.Text.Json.Nodes;
using DiagramPoint = Blazor.Diagrams.Core.Geometry.Point;

namespace FluxMq.UI.Components.Workspace.Nodes.MessageFilter;

public sealed class MessageFilterNodeModel(string id, DiagramPoint position, string nodeName, FlowComponentDescriptor? descriptor, bool isResource)
    : FlowDiagramNodeModel(id, position, nodeName, "flow.filter", descriptor, isResource)
{
    public string[] Patterns { get; set; } = ["#"];
    public string Expression { get; set; } = string.Empty;

    protected override void OnConfigurationLoaded(JsonObject? config)
    {
        Patterns = ReadPatterns(config);
        Expression = config?["expression"]?.GetValue<string?>() ?? string.Empty;
    }

    public override JsonObject BuildConfiguration()
    {
        var patterns = new JsonArray();
        foreach (var p in Patterns) patterns.Add(p);
        var obj = new JsonObject { ["patterns"] = patterns };
        if (!string.IsNullOrWhiteSpace(Expression))
            obj["expression"] = Expression;
        return obj;
    }

    private static string[] ReadPatterns(JsonObject? config)
    {
        if (config?["patterns"] is JsonArray array)
        {
            var result = array
                .Select(n => n?.GetValue<string?>() ?? string.Empty)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToArray();
            return result.Length > 0 ? result : ["#"];
        }
        return ["#"];
    }
}
