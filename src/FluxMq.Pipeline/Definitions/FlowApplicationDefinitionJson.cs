using System.Text.Json;
using System.Text.Json.Serialization;

namespace FluxMq.Pipeline.Definitions;

public static class FlowApplicationDefinitionJson
{
    public static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

        options.Converters.Add(new FlowNodeTypeJsonConverter());
        options.Converters.Add(new FlowPortNameJsonConverter());
        options.Converters.Add(new FlowNodeNameJsonConverter());
        options.Converters.Add(new FlowPortReferenceJsonConverter());
        options.Converters.Add(new FlowLinkDefinitionJsonConverter());

        return options;
    }

    private sealed class FlowNodeTypeJsonConverter : JsonConverter<FlowNodeType>
    {
        public override FlowNodeType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => new(reader.GetString() ?? throw new JsonException("Flow node type must be a string."));

        public override void Write(Utf8JsonWriter writer, FlowNodeType value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.Value);
    }

    private sealed class FlowPortNameJsonConverter : JsonConverter<FlowPortName>
    {
        public override FlowPortName Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => new(reader.GetString() ?? throw new JsonException("Flow port name must be a string."));

        public override void Write(Utf8JsonWriter writer, FlowPortName value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.Value);
    }

    private sealed class FlowNodeNameJsonConverter : JsonConverter<FlowNodeName>
    {
        public override FlowNodeName Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => new(reader.GetString() ?? throw new JsonException("Flow node name must be a string."));

        public override void Write(Utf8JsonWriter writer, FlowNodeName value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.Value);
    }

    private sealed class FlowPortReferenceJsonConverter : JsonConverter<FlowPortReference>
    {
        public override FlowPortReference Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => FlowPortReference.Parse(reader.GetString() ?? throw new JsonException("Flow port reference must be a string."));

        public override void Write(Utf8JsonWriter writer, FlowPortReference value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.ToString());
    }

    private sealed class FlowLinkDefinitionJsonConverter : JsonConverter<FlowLinkDefinition>
    {
        public override FlowLinkDefinition Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                return new FlowLinkDefinition
                {
                    From = FlowPortReference.Parse(reader.GetString()!)
                };
            }

            using var document = JsonDocument.ParseValue(ref reader);
            return FlowLinkJson.ParseOne(document.RootElement, null, options);
        }

        public override void Write(Utf8JsonWriter writer, FlowLinkDefinition value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString("from", value.From.ToString());
            if (!string.IsNullOrWhiteSpace(value.When))
            {
                writer.WriteString("when", value.When);
            }

            writer.WriteEndObject();
        }
    }
}
