using System.Text.Json;
using System.Text.Json.Nodes;

namespace FluxMq.UI.Services;

public sealed partial class FlowDefinitionComposer
{
    /// <summary>
    /// Embeds node positions and collapsed state under <c>FluxMq.Designer.nodes</c> in the JSON.
    /// Only the canvas section is touched; the flow definition is unchanged.
    /// </summary>
    public string WriteNodePositions(string json, IReadOnlyDictionary<string, (double X, double Y, bool Collapsed)> positions)
    {
        var root = ParseOrCreate(json);
        var fluxMq = GetOrCreateObject(root, "FluxMq");
        var designer = GetOrCreateObject(fluxMq, "Designer");
        var nodes = new JsonObject();
        foreach (var (name, (x, y, collapsed)) in positions)
        {
            nodes[name] = new JsonObject
            {
                ["x"] = x,
                ["y"] = y,
                ["collapsed"] = collapsed
            };
        }

        designer["nodes"] = nodes;
        return root.ToJsonString(Options);
    }

    /// <summary>
    /// Reads node positions previously written by <see cref="WriteNodePositions"/>.
    /// Returns an empty dictionary when no designer section exists.
    /// </summary>
    public IReadOnlyDictionary<string, (double X, double Y, bool Collapsed)> ReadNodePositions(string json)
    {
        var result = new Dictionary<string, (double X, double Y, bool Collapsed)>(StringComparer.Ordinal);
        using var doc = ParseDefinitionJson(json, "Read node positions");
        var root = doc.RootElement;
        if (root.TryGetProperty("FluxMq", out var fluxMq) &&
            fluxMq.TryGetProperty("Designer", out var designer) &&
            designer.TryGetProperty("nodes", out var nodes) &&
            nodes.ValueKind == JsonValueKind.Object)
        {
            foreach (var node in nodes.EnumerateObject())
            {
                var x = node.Value.TryGetProperty("x", out var xp) ? xp.GetDouble() : 0;
                var y = node.Value.TryGetProperty("y", out var yp) ? yp.GetDouble() : 0;
                var collapsed = node.Value.TryGetProperty("collapsed", out var cp) && cp.GetBoolean();
                result[node.Name] = (x, y, collapsed);
            }
        }

        return result;
    }

    /// <summary>
    /// Returns the JSON with the <c>FluxMq.Designer</c> section removed so that
    /// the in-memory definition stays clean of UI-only data.
    /// </summary>
    public string StripDesignerSection(string json)
    {
        var root = ParseOrCreate(json);
        if (root["FluxMq"] is JsonObject fluxMq)
        {
            fluxMq.Remove("Designer");
        }

        return root.ToJsonString(Options);
    }
}
