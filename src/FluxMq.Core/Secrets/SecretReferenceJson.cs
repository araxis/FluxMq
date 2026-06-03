using FluxFlow.Components.Secrets.Contracts;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FluxMq.Core.Secrets;

public static class SecretReferenceJson
{
    public static SecretReference? ReadOptional(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) ||
            value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            var name = value.GetString();
            return string.IsNullOrWhiteSpace(name)
                ? null
                : new SecretReference { Name = new SecretName(name) };
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"Configuration value '{propertyName}' must be a string or an object.");
        }

        var secretName = ReadRequiredString(value, "name", $"{propertyName}.name");
        var attributes = ReadAttributes(value, propertyName);
        return new SecretReference
        {
            Name = new SecretName(secretName),
            Version = ReadNullableString(value, "version", $"{propertyName}.version"),
            Kind = ReadNullableString(value, "kind", $"{propertyName}.kind"),
            Attributes = attributes
        };
    }

    public static JsonObject Write(SecretReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        var json = new JsonObject
        {
            ["name"] = reference.Name.Value
        };

        if (!string.IsNullOrWhiteSpace(reference.Version))
        {
            json["version"] = reference.Version;
        }

        if (!string.IsNullOrWhiteSpace(reference.Kind))
        {
            json["kind"] = reference.Kind;
        }

        if (reference.Attributes.Count > 0)
        {
            var attributes = new JsonObject();
            foreach (var (key, value) in reference.Attributes.OrderBy(attribute => attribute.Key, StringComparer.Ordinal))
            {
                attributes[key] = value;
            }

            json["attributes"] = attributes;
        }

        return json;
    }

    private static IReadOnlyDictionary<string, string> ReadAttributes(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty("attributes", out var attributesElement) ||
            attributesElement.ValueKind == JsonValueKind.Null)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        if (attributesElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"Configuration value '{propertyName}.attributes' must be an object.");
        }

        var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var attribute in attributesElement.EnumerateObject())
        {
            if (attribute.Value.ValueKind != JsonValueKind.String)
            {
                throw new InvalidOperationException($"Configuration value '{propertyName}.attributes.{attribute.Name}' must be a string.");
            }

            attributes[attribute.Name] = attribute.Value.GetString() ?? string.Empty;
        }

        return attributes;
    }

    private static string ReadRequiredString(JsonElement element, string propertyName, string displayName)
    {
        var value = ReadNullableString(element, propertyName, displayName);
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Configuration value '{displayName}' is required.")
            : value;
    }

    private static string? ReadNullableString(JsonElement element, string propertyName, string displayName)
    {
        if (!element.TryGetProperty(propertyName, out var value) ||
            value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"Configuration value '{displayName}' must be a string.");
        }

        return value.GetString();
    }
}
