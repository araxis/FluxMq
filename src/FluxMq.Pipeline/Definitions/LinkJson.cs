using System.Text.Json;

namespace FluxMq.Pipeline.Definitions;

public static class LinkJson
{
    public static IReadOnlyList<LinkDefinition> ParseMany(
        JsonElement value,
        string? defaultWhen = null,
        JsonSerializerOptions? options = null)
    {
        options ??= ApplicationDefinitionJson.CreateSerializerOptions();

        if (value.ValueKind == JsonValueKind.Array)
        {
            return value.EnumerateArray()
                .Select(item => ParseOne(item, defaultWhen, options))
                .ToArray();
        }

        return [ParseOne(value, defaultWhen, options)];
    }

    public static LinkDefinition ParseOne(
        JsonElement value,
        string? defaultWhen = null,
        JsonSerializerOptions? options = null)
    {
        options ??= ApplicationDefinitionJson.CreateSerializerOptions();

        return value.ValueKind switch
        {
            JsonValueKind.String => new LinkDefinition
            {
                From = PortReference.Parse(value.GetString()!),
                When = defaultWhen
            },
            JsonValueKind.Object => ParseObject(value, defaultWhen, options),
            _ => throw new JsonException("Flow link must be a string or object.")
        };
    }

    private static LinkDefinition ParseObject(
        JsonElement value,
        string? defaultWhen,
        JsonSerializerOptions options)
    {
        var from = ReadProperty(value, "from") ?? ReadProperty(value, "From");
        if (from is null)
        {
            throw new JsonException("Flow link object must contain a From property.");
        }

        var when = ReadProperty(value, "when") ?? ReadProperty(value, "When");

        return new LinkDefinition
        {
            From = from.Value.Deserialize<PortReference>(options)!,
            When = when?.GetString() ?? defaultWhen
        };
    }

    private static JsonElement? ReadProperty(JsonElement value, string propertyName)
        => value.TryGetProperty(propertyName, out var property)
            ? property
            : null;
}
