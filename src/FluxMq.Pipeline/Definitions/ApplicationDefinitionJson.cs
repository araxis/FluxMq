using System.Text.Json;
using System.Text.Json.Serialization;

namespace FluxMq.Pipeline.Definitions;

public static class ApplicationDefinitionJson
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

    private sealed class FlowNodeTypeJsonConverter : JsonConverter<NodeType>
    {
        public override NodeType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => new(reader.GetString() ?? throw new JsonException("Flow node type must be a string."));

        public override void Write(Utf8JsonWriter writer, NodeType value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.Value);
    }

    private sealed class FlowPortNameJsonConverter : JsonConverter<PortName>
    {
        public override PortName Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => new(reader.GetString() ?? throw new JsonException("Flow port name must be a string."));

        public override void Write(Utf8JsonWriter writer, PortName value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.Value);
    }

    private sealed class FlowNodeNameJsonConverter : JsonConverter<NodeName>
    {
        public override NodeName Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => new(reader.GetString() ?? throw new JsonException("Flow node name must be a string."));

        public override void Write(Utf8JsonWriter writer, NodeName value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.Value);
    }

    private sealed class FlowPortReferenceJsonConverter : JsonConverter<PortReference>
    {
        public override PortReference Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => PortReference.Parse(reader.GetString() ?? throw new JsonException("Flow port reference must be a string."));

        public override void Write(Utf8JsonWriter writer, PortReference value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.ToString());
    }

    private sealed class FlowLinkDefinitionJsonConverter : JsonConverter<LinkDefinition>
    {
        public override LinkDefinition Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                return new LinkDefinition
                {
                    From = PortReference.Parse(reader.GetString()!)
                };
            }

            using var document = JsonDocument.ParseValue(ref reader);
            return LinkJson.ParseOne(document.RootElement, null, options);
        }

        public override void Write(Utf8JsonWriter writer, LinkDefinition value, JsonSerializerOptions options)
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
