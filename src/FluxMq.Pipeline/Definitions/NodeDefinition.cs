using System.Text.Json;
using System.Text.Json.Serialization;

namespace FluxMq.Pipeline.Definitions;

public sealed record NodeDefinition
{
    public required NodeType Type { get; init; }
    public Dictionary<string, JsonElement> Configuration { get; init; } = [];
    public string? When { get; init; }
    public int Phase { get; init; } = 0;

    [JsonExtensionData]
    public Dictionary<string, JsonElement> Ports { get; init; } = [];

    public IReadOnlyList<LinkDefinition> GetPortLinks(string portName, string workflowName)
    {
        if (!Ports.TryGetValue(portName, out var value))
            return [];

        return LinkJson.ParseMany(value, workflowName, When);
    }

    public IReadOnlyDictionary<string, IReadOnlyList<LinkDefinition>> GetAllPortLinks(string workflowName)
        => Ports.ToDictionary(
            port => port.Key,
            port => (IReadOnlyList<LinkDefinition>)LinkJson.ParseMany(port.Value, workflowName, When));
}
