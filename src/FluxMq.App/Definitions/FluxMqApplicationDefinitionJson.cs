using System.Text.Json;
using System.Text.Json.Serialization;
using FluxMq.App.Metrics;
using FluxMq.Core.Secrets;
using FluxFlow.Components.Secrets.Contracts;
using EngineApplicationDefinitionJson = FluxFlow.Engine.Definitions.ApplicationDefinitionJson;

namespace FluxMq.App.Definitions;

public static class FluxMqApplicationDefinitionJson
{
    public static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = EngineApplicationDefinitionJson.CreateSerializerOptions();
        options.Converters.Add(new DashboardGridTrackDefinitionJsonConverter());
        options.Converters.Add(new SecretReferenceJsonConverter());
        options.Converters.Add(new JsonStringEnumConverter<FluxMetricInstrumentKind>(JsonNamingPolicy.CamelCase));
        return options;
    }

    private sealed class SecretReferenceJsonConverter : JsonConverter<SecretReference>
    {
        public override SecretReference? Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                var name = reader.GetString();
                return string.IsNullOrWhiteSpace(name)
                    ? null
                    : new SecretReference { Name = new SecretName(name) };
            }

            using var document = JsonDocument.ParseValue(ref reader);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("Secret reference must be a string or an object.");
            }

            return new SecretReference
            {
                Name = new SecretName(ReadRequiredString(root, "name")),
                Version = ReadNullableString(root, "version"),
                Kind = ReadNullableString(root, "kind"),
                Attributes = ReadAttributes(root)
            };
        }

        public override void Write(
            Utf8JsonWriter writer,
            SecretReference value,
            JsonSerializerOptions options)
            => SecretReferenceJson.Write(value).WriteTo(writer, options);

        private static string ReadRequiredString(JsonElement element, string propertyName)
        {
            var value = ReadNullableString(element, propertyName);
            return string.IsNullOrWhiteSpace(value)
                ? throw new JsonException($"Secret reference property '{propertyName}' is required.")
                : value;
        }

        private static string? ReadNullableString(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var value) ||
                value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            return value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : throw new JsonException($"Secret reference property '{propertyName}' must be a string.");
        }

        private static IReadOnlyDictionary<string, string> ReadAttributes(JsonElement element)
        {
            if (!element.TryGetProperty("attributes", out var attributesElement) ||
                attributesElement.ValueKind == JsonValueKind.Null)
            {
                return new Dictionary<string, string>(StringComparer.Ordinal);
            }

            if (attributesElement.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("Secret reference property 'attributes' must be an object.");
            }

            var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var attribute in attributesElement.EnumerateObject())
            {
                if (attribute.Value.ValueKind != JsonValueKind.String)
                {
                    throw new JsonException($"Secret reference attribute '{attribute.Name}' must be a string.");
                }

                attributes[attribute.Name] = attribute.Value.GetString() ?? string.Empty;
            }

            return attributes;
        }
    }

    private sealed class DashboardGridTrackDefinitionJsonConverter : JsonConverter<DashboardGridTrackDefinition>
    {
        public override DashboardGridTrackDefinition Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                try
                {
                    return DashboardGridTrackDefinition.Parse(reader.GetString()!);
                }
                catch (FormatException exception)
                {
                    throw new JsonException(exception.Message, exception);
                }
            }

            if (reader.TokenType == JsonTokenType.Number && reader.TryGetDouble(out var fixedSize))
            {
                return DashboardGridTrackDefinition.Fixed(fixedSize);
            }

            using var document = JsonDocument.ParseValue(ref reader);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("Dashboard grid track must be a string, number, or object.");
            }

            if (root.TryGetProperty("size", out var sizeElement) && sizeElement.ValueKind == JsonValueKind.String)
            {
                try
                {
                    return DashboardGridTrackDefinition.Parse(sizeElement.GetString()!);
                }
                catch (FormatException exception)
                {
                    throw new JsonException(exception.Message, exception);
                }
            }

            var unit = root.TryGetProperty("unit", out var unitElement) && unitElement.ValueKind == JsonValueKind.String
                ? unitElement.GetString()
                : throw new JsonException("Dashboard grid track object must contain a string 'unit' property.");

            if (!root.TryGetProperty("value", out var valueElement) ||
                valueElement.ValueKind != JsonValueKind.Number ||
                !valueElement.TryGetDouble(out var value))
            {
                throw new JsonException("Dashboard grid track object must contain a numeric 'value' property.");
            }

            return unit?.Trim().ToLowerInvariant() switch
            {
                "fixed" => DashboardGridTrackDefinition.Fixed(value),
                "percent" => DashboardGridTrackDefinition.Percent(value),
                "star" => DashboardGridTrackDefinition.Star(value),
                _ => throw new JsonException("Dashboard grid track unit must be fixed, percent, or star.")
            };
        }

        public override void Write(
            Utf8JsonWriter writer,
            DashboardGridTrackDefinition value,
            JsonSerializerOptions options)
            => writer.WriteStringValue(value.ToSizeString());
    }
}
