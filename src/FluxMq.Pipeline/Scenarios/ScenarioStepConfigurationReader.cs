using System.Text.Json;

namespace FluxMq.Pipeline.Scenarios;

internal static class ScenarioStepConfigurationReader
{
    public static string? ReadString(
        IReadOnlyDictionary<string, JsonElement> configuration,
        string key)
    {
        if (!configuration.TryGetValue(key, out var value) ||
            value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"Scenario configuration value '{key}' must be a string.");
        }

        var text = value.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    public static int ReadIntOrDefault(
        IReadOnlyDictionary<string, JsonElement> configuration,
        string key,
        int defaultValue,
        int minValue)
    {
        if (!configuration.TryGetValue(key, out var value))
        {
            return defaultValue;
        }

        if (value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt32(out var result) ||
            result < minValue)
        {
            throw new InvalidOperationException(
                $"Scenario configuration value '{key}' must be an integer greater than or equal to {minValue}.");
        }

        return result;
    }

    public static IReadOnlyDictionary<string, string> ReadStringMap(
        IReadOnlyDictionary<string, JsonElement> configuration,
        string key)
    {
        if (!configuration.TryGetValue(key, out var value) ||
            value.ValueKind == JsonValueKind.Null)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"Scenario configuration value '{key}' must be an object.");
        }

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.String)
            {
                throw new InvalidOperationException(
                    $"Scenario configuration value '{key}.{property.Name}' must be a string.");
            }

            result[property.Name] = property.Value.GetString() ?? string.Empty;
        }

        return result;
    }
}
