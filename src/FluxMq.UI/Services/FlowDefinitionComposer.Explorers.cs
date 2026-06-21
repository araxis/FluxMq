using FluxMq.App.Definitions;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FluxMq.UI.Services;

public sealed partial class FlowDefinitionComposer
{
    public IReadOnlyList<string> GetExplorerNames(string json)
        => GetNamedObjectKeys(json, "explorers");

    public IReadOnlyDictionary<string, ExplorerDefinition> ReadExplorersFromDefinition(string json)
    {
        using var doc = ParseDefinitionJson(json, "Read explorers");
        var root = doc.RootElement;
        var flowApp = TryGetFlowApplication(root, out var app) ? app : root;

        if (!flowApp.TryGetProperty("explorers", out var explorersElement) ||
            explorersElement.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, ExplorerDefinition>(StringComparer.Ordinal);
        }

        var explorers = JsonSerializer.Deserialize<Dictionary<string, ExplorerDefinition>>(
            explorersElement.GetRawText(),
            FluxMqApplicationDefinitionJson.CreateSerializerOptions());

        return explorers is null
            ? new Dictionary<string, ExplorerDefinition>(StringComparer.Ordinal)
            : new Dictionary<string, ExplorerDefinition>(explorers, StringComparer.Ordinal);
    }

    public string UpsertExplorer(string json, string explorerName, ExplorerDefinition explorer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(explorerName);
        ArgumentNullException.ThrowIfNull(explorer);

        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        var explorers = GetOrCreateObject(flowApplication, "explorers");
        explorers[explorerName.Trim()] =
            JsonSerializer.SerializeToNode(explorer, FluxMqApplicationDefinitionJson.CreateSerializerOptions()) as JsonObject
            ?? new JsonObject();

        return root.ToJsonString(Options);
    }
}
