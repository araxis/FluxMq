using FluxMq.Core.Models;
using FluxMq.Pipeline.Definitions;
using FluxMq.Pipeline.Runtime;
using System.Text.Json;

namespace FluxMq.App.Scenarios;

internal static class ApplicationDefinitionMqttConnectionProfileResolver
{
    public static MqttConnectionProfile Resolve(ApplicationDefinition definition, string connectionName)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);

        if (!definition.Resources.TryGetValue(connectionName, out var resource))
        {
            throw new InvalidOperationException($"MQTT connection resource '{connectionName}' does not exist.");
        }

        if (!string.Equals(resource.Type.Value, PipelineFlowNodeTypes.Connection.Value, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Resource '{connectionName}' is not an MQTT connection.");
        }

        return ReadConnectionProfile(resource);
    }

    private static MqttConnectionProfile ReadConnectionProfile(NodeDefinition definition)
    {
        if (!definition.Configuration.TryGetValue("profile", out var profileElement))
        {
            throw new InvalidOperationException($"Configuration value 'profile' is required for {PipelineFlowNodeTypes.Connection.Value}.");
        }

        if (profileElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Configuration value 'profile' must be an object.");
        }

        var defaults = new MqttConnectionProfile();
        return new MqttConnectionProfile
        {
            Name = ReadRequiredString(profileElement, "name"),
            Host = ReadStringOrDefault(profileElement, "host", defaults.Host),
            Port = ReadIntOrDefault(profileElement, "port", defaults.Port, minValue: 1),
            ClientId = ReadStringOrDefault(profileElement, "clientId", defaults.ClientId),
            UseTls = ReadBoolOrDefault(profileElement, "useTls", defaults.UseTls),
            Username = ReadNullableString(profileElement, "username"),
            Password = ReadNullableString(profileElement, "password"),
            KeepAlive = TimeSpan.FromSeconds(ReadIntOrDefault(profileElement, "keepAliveSeconds", (int)defaults.KeepAlive.TotalSeconds, minValue: 1)),
            CleanStart = ReadBoolOrDefault(profileElement, "cleanStart", defaults.CleanStart)
        };
    }

    private static string ReadRequiredString(JsonElement element, string propertyName)
    {
        var value = ReadNullableString(element, propertyName);
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Configuration value '{propertyName}' is required.")
            : value;
    }

    private static string ReadStringOrDefault(JsonElement element, string propertyName, string defaultValue)
        => ReadNullableString(element, propertyName) ?? defaultValue;

    private static string? ReadNullableString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) ||
            value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"Configuration value '{propertyName}' must be a string.");
        }

        return value.GetString();
    }

    private static int ReadIntOrDefault(JsonElement element, string propertyName, int defaultValue, int? minValue = null)
    {
        if (!element.TryGetProperty(propertyName, out var value) ||
            value.ValueKind == JsonValueKind.Null)
        {
            return defaultValue;
        }

        if (value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt32(out var result) ||
            (minValue is { } min && result < min))
        {
            throw new InvalidOperationException($"Configuration value '{propertyName}' must be a valid integer.");
        }

        return result;
    }

    private static bool ReadBoolOrDefault(JsonElement element, string propertyName, bool defaultValue)
    {
        if (!element.TryGetProperty(propertyName, out var value) ||
            value.ValueKind == JsonValueKind.Null)
        {
            return defaultValue;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new InvalidOperationException($"Configuration value '{propertyName}' must be a boolean.")
        };
    }
}
