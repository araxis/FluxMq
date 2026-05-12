using System.Text.Json;
using System.Text.Json.Serialization;

namespace FluxMq.Pipeline.Definitions;

public sealed record NodeDefinition
{
    public required NodeType Type { get; init; }
    public Dictionary<string, JsonElement> Configuration { get; init; } = [];
    public string? When { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> Ports { get; init; } = [];

    public IReadOnlyList<LinkDefinition> GetPortLinks(string portName, JsonSerializerOptions? options = null)
    {
        if (!Ports.TryGetValue(portName, out var value))
        {
            return [];
        }

        return LinkJson.ParseMany(value, When, options);
    }

    public IReadOnlyDictionary<string, IReadOnlyList<LinkDefinition>> GetAllPortLinks(JsonSerializerOptions? options = null)
        => Ports.ToDictionary(
            port => port.Key,
            port => (IReadOnlyList<LinkDefinition>)LinkJson.ParseMany(port.Value, When, options));
}
