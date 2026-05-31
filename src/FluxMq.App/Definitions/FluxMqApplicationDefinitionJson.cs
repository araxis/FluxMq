using FluxMq.Pipeline.Definitions;
using System.Text.Json;
using System.Text.Json.Serialization;
using EngineApplicationDefinitionJson = FluxFlow.Engine.Definitions.ApplicationDefinitionJson;

namespace FluxMq.App.Definitions;

public static class FluxMqApplicationDefinitionJson
{
    public static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = EngineApplicationDefinitionJson.CreateSerializerOptions();
        options.Converters.Add(new DashboardGridTrackDefinitionJsonConverter());
        return options;
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
