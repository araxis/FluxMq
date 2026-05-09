using System.Text.Json;
using System.Text.Json.Serialization;

namespace FluxMq.Pipeline.Definitions;

public sealed record FlowNodeDefinition
{
    public required FlowNodeType Type { get; init; }
    public Dictionary<string, JsonElement> Configuration { get; init; } = [];
    public string? When { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> Ports { get; init; } = [];

    public IReadOnlyList<FlowLinkDefinition> GetPortLinks(string portName, JsonSerializerOptions? options = null)
    {
        if (!Ports.TryGetValue(portName, out var value))
        {
            return [];
        }

        return FlowLinkJson.ParseMany(value, When, options);
    }

    public IReadOnlyDictionary<string, IReadOnlyList<FlowLinkDefinition>> GetAllPortLinks(JsonSerializerOptions? options = null)
        => Ports.ToDictionary(
            port => port.Key,
            port => (IReadOnlyList<FlowLinkDefinition>)FlowLinkJson.ParseMany(port.Value, When, options));
}
