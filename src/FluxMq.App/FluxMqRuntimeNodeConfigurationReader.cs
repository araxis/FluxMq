using System.Text;
using System.Text.Json;
using FluxFlow.Engine.Definitions;
using FluxMq.Core.Ids;

namespace FluxMq.App;

internal static class FluxMqRuntimeNodeConfigurationReader
{
    public static string? GetNullableString(NodeDefinition definition, string key)
    {
        if (!definition.Configuration.TryGetValue(key, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var s = value.GetString();
        return string.IsNullOrWhiteSpace(s) ? null : s;
    }

    public static IReadOnlyDictionary<string, string> GetStringDictionary(NodeDefinition definition, string key)
    {
        if (!definition.Configuration.TryGetValue(key, out var element) ||
            element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"Configuration value '{key}' must be an object.");
        }

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (string.IsNullOrWhiteSpace(property.Name))
            {
                continue;
            }

            var value = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString(),
                JsonValueKind.Number => property.Value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null
            };

            if (!string.IsNullOrWhiteSpace(value))
            {
                result[property.Name.Trim()] = value.Trim();
            }
        }

        return result;
    }

    public static string GetRequiredString(NodeDefinition definition, string key)
    {
        if (!definition.Configuration.TryGetValue(key, out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"Configuration value '{key}' is required and must be a string.");
        }

        var s = value.GetString();
        if (string.IsNullOrWhiteSpace(s))
        {
            throw new InvalidOperationException($"Configuration value '{key}' must not be empty.");
        }

        return s;
    }

    public static string GetStringOrDefault(NodeDefinition definition, string key, string defaultValue)
    {
        if (!definition.Configuration.TryGetValue(key, out var value))
        {
            return defaultValue;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"Configuration value '{key}' must be a string.");
        }

        var s = value.GetString();
        if (string.IsNullOrWhiteSpace(s))
        {
            throw new InvalidOperationException($"Configuration value '{key}' must not be empty.");
        }

        return s;
    }

    public static int GetBoundedCapacity(NodeDefinition definition)
    {
        const int defaultBoundedCapacity = 1000;

        if (!definition.Configuration.TryGetValue("boundedCapacity", out var value))
        {
            return defaultBoundedCapacity;
        }

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var boundedCapacity) || boundedCapacity <= 0)
        {
            throw new InvalidOperationException("Configuration value 'boundedCapacity' must be a positive integer.");
        }

        return boundedCapacity;
    }

    public static SessionId? GetOptionalSessionId(NodeDefinition definition, string key)
    {
        var value = GetNullableString(definition, key);
        if (value is null)
        {
            return null;
        }

        if (!Guid.TryParse(value, out var guid) || guid == Guid.Empty)
        {
            throw new InvalidOperationException($"Configuration value '{key}' must be a non-empty GUID.");
        }

        return new SessionId(guid);
    }

    public static bool GetBoolOrDefault(NodeDefinition definition, string key, bool defaultValue)
    {
        if (!definition.Configuration.TryGetValue(key, out var value))
        {
            return defaultValue;
        }

        if (value.ValueKind != JsonValueKind.True && value.ValueKind != JsonValueKind.False)
        {
            throw new InvalidOperationException($"Configuration value '{key}' must be a boolean.");
        }

        return value.GetBoolean();
    }

    public static int GetIntOrDefault(NodeDefinition definition, string key, int defaultValue, int minValue)
    {
        if (!definition.Configuration.TryGetValue(key, out var value))
        {
            return defaultValue;
        }

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var result) || result < minValue)
        {
            throw new InvalidOperationException($"Configuration value '{key}' must be an integer greater than or equal to {minValue}.");
        }

        return result;
    }

    public static int? GetOptionalInt(NodeDefinition definition, string key, int minValue)
    {
        if (!definition.Configuration.TryGetValue(key, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var result) || result < minValue)
        {
            throw new InvalidOperationException($"Configuration value '{key}' must be an integer greater than or equal to {minValue}.");
        }

        return result;
    }

    public static double GetDoubleOrDefault(NodeDefinition definition, string key, double defaultValue)
    {
        if (!definition.Configuration.TryGetValue(key, out var value))
        {
            return defaultValue;
        }

        if (value.ValueKind != JsonValueKind.Number ||
            !value.TryGetDouble(out var result) ||
            result <= 0 ||
            double.IsNaN(result) ||
            double.IsInfinity(result))
        {
            throw new InvalidOperationException($"Configuration value '{key}' must be a positive finite number.");
        }

        return result;
    }

    public static byte[] DecodePayload(string value, string encoding)
        => encoding.Trim().ToLowerInvariant() switch
        {
            "utf8" or "text" => Encoding.UTF8.GetBytes(value),
            "base64" => Convert.FromBase64String(value),
            _ => throw new InvalidOperationException("Generated message payloadEncoding must be utf8 or base64.")
        };

    public static byte ReadByte(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetByte(out var result))
        {
            throw new InvalidOperationException("Generated message byte payload values must be between 0 and 255.");
        }

        return result;
    }

    public static DateTimeOffset ReadDateTimeOffsetOrDefault(JsonElement element, string propertyName, DateTimeOffset defaultValue)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return defaultValue;
        }

        if (property.ValueKind != JsonValueKind.String || !DateTimeOffset.TryParse(property.GetString(), out var value))
        {
            throw new InvalidOperationException($"Configuration value '{propertyName}' must be a valid date/time string.");
        }

        return value;
    }

    public static string ReadRequiredString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"Configuration value '{propertyName}' is required and must be a string.");
        }

        var value = property.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Configuration value '{propertyName}' must not be empty.");
        }

        return value;
    }

    public static string ReadStringOrDefault(JsonElement element, string propertyName, string defaultValue)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return defaultValue;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"Configuration value '{propertyName}' must be a string.");
        }

        var value = property.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Configuration value '{propertyName}' must not be empty.");
        }

        return value;
    }

    public static string? ReadNullableString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"Configuration value '{propertyName}' must be a string or null.");
        }

        var value = property.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public static int ReadIntOrDefault(JsonElement element, string propertyName, int defaultValue, int minValue)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return defaultValue;
        }

        if (property.ValueKind != JsonValueKind.Number || !property.TryGetInt32(out var value) || value < minValue)
        {
            throw new InvalidOperationException($"Configuration value '{propertyName}' must be an integer greater than or equal to {minValue}.");
        }

        return value;
    }

    public static bool ReadBoolOrDefault(JsonElement element, string propertyName, bool defaultValue)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return defaultValue;
        }

        if (property.ValueKind != JsonValueKind.True && property.ValueKind != JsonValueKind.False)
        {
            throw new InvalidOperationException($"Configuration value '{propertyName}' must be a boolean.");
        }

        return property.GetBoolean();
    }
}
