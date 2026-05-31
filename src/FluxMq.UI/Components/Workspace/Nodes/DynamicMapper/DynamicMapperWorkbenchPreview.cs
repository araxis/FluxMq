using FluxMq.Components.FileWriter;
using FluxMq.Components.Mapping;
using FluxMq.Components.MqttPublisher;
using FluxMq.Components.Replay;
using FluxMq.Core.Models;
using FluxFlow.Engine.Mapping;
using MQTTnet.Protocol;
using System.Text;
using System.Text.Json;

namespace FluxMq.UI.Components.Workspace.Nodes.DynamicMapper;

public sealed record DynamicMapperVariable(
    string Label,
    string DynamicExpressoExpression,
    string JsonataExpression,
    string Value,
    int Depth = 0,
    bool JsonOnly = false);

public sealed record DynamicMapperOutputField(
    string Name,
    string Type,
    bool Required,
    string Example);

public sealed record DynamicMapperPreviewProperty(string Name, string Value);

public sealed record DynamicMapperPreviewResult(
    bool Success,
    string Title,
    IReadOnlyList<DynamicMapperPreviewProperty> Properties,
    string? Error = null,
    string Json = "");

public sealed record DynamicMapperInputSampleResult(
    bool Success,
    MqttEnvelope? Envelope,
    string? Error);

public static class DynamicMapperWorkbenchPreview
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static MqttEnvelope FallbackEnvelope() => new()
    {
        Topic = "factory/line-a/temperature",
        Payload = Encoding.UTF8.GetBytes("""{"value":21.7,"unit":"c","status":"ok"}"""),
        QualityOfService = MqttQualityOfServiceLevel.AtLeastOnce,
        Retain = false,
        ReceivedAt = DateTimeOffset.Parse("2026-05-23T10:00:00Z")
    };

    public static string InputJson(MqttEnvelope envelope)
    {
        var payloadText = Encoding.UTF8.GetString(envelope.Payload);
        object? payload = TryParseJson(payloadText) is { } payloadJson
            ? payloadJson
            : payloadText;

        return JsonSerializer.Serialize(
            new Dictionary<string, object?>
            {
                ["topic"] = envelope.Topic,
                ["qos"] = (int)envelope.QualityOfService,
                ["retain"] = envelope.Retain,
                ["receivedAt"] = envelope.ReceivedAt,
                ["payload"] = payload
            },
            JsonOptions);
    }

    public static DynamicMapperInputSampleResult ParseInputJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new(false, null, "Input JSON is empty.");
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement.Clone();
            var hasEnvelopeShape = HasEnvelopeShape(root);
            var topic = hasEnvelopeShape
                ? ReadString(root, "topic", "sample/topic")
                : "sample/topic";
            var qos = hasEnvelopeShape
                ? Math.Clamp(ReadInt(root, "qos", 1), 0, 2)
                : 1;
            var retain = hasEnvelopeShape && ReadBool(root, "retain", false);
            var receivedAt = hasEnvelopeShape
                ? ReadDateTimeOffset(root, "receivedAt", DateTimeOffset.Now)
                : DateTimeOffset.Now;
            var payloadText = ReadPayloadText(root, hasEnvelopeShape);

            return new(
                true,
                new MqttEnvelope
                {
                    Topic = string.IsNullOrWhiteSpace(topic) ? "sample/topic" : topic.Trim(),
                    Payload = Encoding.UTF8.GetBytes(payloadText),
                    QualityOfService = qos switch
                    {
                        <= 0 => MqttQualityOfServiceLevel.AtMostOnce,
                        >= 2 => MqttQualityOfServiceLevel.ExactlyOnce,
                        _ => MqttQualityOfServiceLevel.AtLeastOnce
                    },
                    Retain = retain,
                    ReceivedAt = receivedAt
                },
                null);
        }
        catch (JsonException exception)
        {
            return new(false, null, exception.Message);
        }
    }

    public static IReadOnlyList<DynamicMapperOutputField> OutputFields(string outputType, string engine = "dynamic-expresso")
    {
        var isJsonata = string.Equals(engine, "jsonata", StringComparison.OrdinalIgnoreCase);
        var topicPrefixExample = isJsonata ? "\"mirror/\" & topic" : "\"mirror/\" + topic";
        var pathExample = isJsonata ? "\"messages/\" & topic & \".txt\"" : "\"messages/\" + topic + \".txt\"";

        return outputType switch
        {
            "Any" =>
            [
                new("result", "any JSON", false, "expression")
            ],
            "JsonSchema" =>
            [
                new("schema", "JSON Schema file", true, "schemas/output.schema.json")
            ],
            "FileWriteRequest" =>
            [
                new("path", "string", true, pathExample),
                new("content", "string | byte[]", false, "payloadText"),
                new("mode", "Overwrite | Append | CreateNew", false, "\"Append\""),
                new("createDirectory", "bool", false, "true")
            ],
            "MqttRecordingRequest" =>
            [
                new("sessionId", "Guid", true, "\"00000000-0000-0000-0000-000000000001\"")
            ],
            _ =>
            [
                new("topic", "string", true, topicPrefixExample),
                new("payload", "string | byte[]", false, "payloadText"),
                new("qos", "0 | 1 | 2", false, "qos"),
                new("retain", "bool", false, "retain")
            ]
        };
    }

    public static IReadOnlyList<DynamicMapperVariable> Variables(MqttEnvelope envelope)
    {
        var payloadText = Encoding.UTF8.GetString(envelope.Payload);
        var variables = new List<DynamicMapperVariable>
        {
            new("topic", "topic", "topic", envelope.Topic),
            new("payloadText", "payloadText", "payloadText", Shorten(payloadText)),
            new("qos", "qos", "qos", ((int)envelope.QualityOfService).ToString()),
            new("retain", "retain", "retain", envelope.Retain.ToString()),
            new("receivedAt", "receivedAt", "receivedAt", envelope.ReceivedAt.ToString("O"))
        };

        if (TryParseJson(payloadText) is { } payloadJson)
        {
            variables.Add(new("payloadJson", string.Empty, "payloadJson", FormatJsonValue(payloadJson), JsonOnly: true));
            AddJsonChildren(variables, payloadJson, "payloadJson", depth: 1, maxVariables: 32);
        }

        return variables;
    }

    public static DynamicMapperPreviewResult PreviewAny(
        string engine,
        string expression,
        MqttEnvelope envelope,
        string title = "Any JSON")
    {
        try
        {
            var expressionEngine = CreateEngine(engine);
            var context = MqttEnvelopeExpressionContextFactory.Create(envelope);
            var value = expressionEngine.Evaluate(expression, context, typeof(object));
            var json = SerializeResult(value);

            return new DynamicMapperPreviewResult(
                true,
                title,
                [
                    new("Type", value?.GetType().Name ?? "null"),
                    new("JSON bytes", Encoding.UTF8.GetByteCount(json).ToString())
                ],
                Json: json);
        }
        catch (Exception exception)
        {
            return new DynamicMapperPreviewResult(false, title, [], exception.Message, ErrorJson(exception.Message));
        }
    }

    public static DynamicMapperPreviewResult Preview(
        string engine,
        string outputType,
        string expression,
        MqttEnvelope envelope)
    {
        try
        {
            var expressionEngine = CreateEngine(engine);
            var context = MqttEnvelopeExpressionContextFactory.Create(envelope);

            return outputType switch
            {
                "FileWriteRequest" => PreviewFileWriteRequest(expressionEngine, expression, context),
                "MqttRecordingRequest" => PreviewRecordingRequest(expressionEngine, expression, context),
                _ => PreviewPublishRequest(expressionEngine, expression, context)
            };
        }
        catch (Exception exception)
        {
            return new DynamicMapperPreviewResult(false, outputType, [], exception.Message, ErrorJson(exception.Message));
        }
    }

    public static string ExpressionFor(DynamicMapperVariable variable, string engine)
        => string.Equals(engine, "jsonata", StringComparison.OrdinalIgnoreCase)
            ? variable.JsonataExpression
            : variable.DynamicExpressoExpression;

    private static DynamicMapperPreviewResult PreviewPublishRequest(
        IFlowExpressionEngine engine,
        string expression,
        FlowMapContext context)
    {
        var mapper = new FluxMqRequestMappingExpressionEngine(engine);
        var request = (MqttPublishRequest)mapper.Evaluate(expression, context, typeof(MqttPublishRequest))!;
        var payloadText = Encoding.UTF8.GetString(request.Payload);
        return new DynamicMapperPreviewResult(
            true,
            "MqttPublishRequest",
            [
                new("Topic", request.Topic),
                new("Payload", Shorten(payloadText)),
                new("QoS", ((int)request.QualityOfService).ToString()),
                new("Retain", request.Retain.ToString())
            ],
            Json: SerializeResult(new Dictionary<string, object?>
            {
                ["topic"] = request.Topic,
                ["payload"] = TryParseJson(payloadText) is { } payloadJson ? payloadJson : payloadText,
                ["qos"] = (int)request.QualityOfService,
                ["retain"] = request.Retain
            }));
    }

    private static DynamicMapperPreviewResult PreviewFileWriteRequest(
        IFlowExpressionEngine engine,
        string expression,
        FlowMapContext context)
    {
        var mapper = new FluxMqRequestMappingExpressionEngine(engine);
        var request = (FileWriteRequest)mapper.Evaluate(expression, context, typeof(FileWriteRequest))!;
        var contentText = Encoding.UTF8.GetString(request.Content);
        return new DynamicMapperPreviewResult(
            true,
            "FileWriteRequest",
            [
                new("Path", request.Path),
                new("Content", Shorten(contentText)),
                new("Mode", request.Mode.ToString()),
                new("Create directory", request.CreateDirectory.ToString())
            ],
            Json: SerializeResult(new Dictionary<string, object?>
            {
                ["path"] = request.Path,
                ["content"] = TryParseJson(contentText) is { } contentJson ? contentJson : contentText,
                ["mode"] = request.Mode.ToString(),
                ["createDirectory"] = request.CreateDirectory
            }));
    }

    private static DynamicMapperPreviewResult PreviewRecordingRequest(
        IFlowExpressionEngine engine,
        string expression,
        FlowMapContext context)
    {
        var mapper = new FluxMqRequestMappingExpressionEngine(engine);
        var request = (MqttRecordingRequest)mapper.Evaluate(expression, context, typeof(MqttRecordingRequest))!;

        return new DynamicMapperPreviewResult(
            true,
            "MqttRecordingRequest",
            [
                new("Session", request.SessionId.ToString()),
                new("Envelope topic", request.Envelope.Topic),
                new("Payload bytes", request.Envelope.Payload.Length.ToString())
            ],
            Json: SerializeResult(new Dictionary<string, object?>
            {
                ["sessionId"] = request.SessionId.ToString(),
                ["envelope"] = new Dictionary<string, object?>
                {
                    ["topic"] = request.Envelope.Topic,
                    ["qos"] = (int)request.Envelope.QualityOfService,
                    ["retain"] = request.Envelope.Retain,
                    ["receivedAt"] = request.Envelope.ReceivedAt,
                    ["payload"] = TryParseJson(Encoding.UTF8.GetString(request.Envelope.Payload)) is { } payloadJson
                        ? payloadJson
                        : Encoding.UTF8.GetString(request.Envelope.Payload)
                }
            }));
    }

    private static IFlowExpressionEngine CreateEngine(string engine)
        => string.Equals(engine, "jsonata", StringComparison.OrdinalIgnoreCase)
            ? new JsonataFlowExpressionEngine()
            : new DynamicExpressoFlowExpressionEngine();

    private static JsonElement? TryParseJson(string payloadText)
    {
        if (string.IsNullOrWhiteSpace(payloadText))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(payloadText);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool HasEnvelopeShape(JsonElement root)
        => root.ValueKind == JsonValueKind.Object &&
           (root.TryGetProperty("payload", out _) ||
            root.TryGetProperty("payloadText", out _) ||
            root.TryGetProperty("topic", out _) ||
            root.TryGetProperty("qos", out _) ||
            root.TryGetProperty("retain", out _));

    private static string ReadPayloadText(JsonElement root, bool hasEnvelopeShape)
    {
        if (hasEnvelopeShape && root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("payloadText", out var payloadText) &&
                payloadText.ValueKind == JsonValueKind.String)
            {
                return payloadText.GetString() ?? string.Empty;
            }

            if (root.TryGetProperty("payload", out var payload))
            {
                return JsonElementToText(payload);
            }
        }

        return JsonElementToText(root);
    }

    private static string JsonElementToText(JsonElement element)
        => element.ValueKind == JsonValueKind.String
            ? element.GetString() ?? string.Empty
            : JsonSerializer.Serialize(element, JsonOptions);

    private static string ReadString(JsonElement root, string propertyName, string fallback)
        => root.ValueKind == JsonValueKind.Object &&
           root.TryGetProperty(propertyName, out var property) &&
           property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? fallback
            : fallback;

    private static int ReadInt(JsonElement root, string propertyName, int fallback)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(propertyName, out var property))
        {
            return fallback;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var number))
        {
            return number;
        }

        return property.ValueKind == JsonValueKind.String && int.TryParse(property.GetString(), out number)
            ? number
            : fallback;
    }

    private static bool ReadBool(JsonElement root, string propertyName, bool fallback)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(propertyName, out var property))
        {
            return fallback;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(property.GetString(), out var value) => value,
            _ => fallback
        };
    }

    private static DateTimeOffset ReadDateTimeOffset(JsonElement root, string propertyName, DateTimeOffset fallback)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return fallback;
        }

        return DateTimeOffset.TryParse(property.GetString(), out var value) ? value : fallback;
    }

    private static void AddJsonChildren(
        List<DynamicMapperVariable> variables,
        JsonElement element,
        string path,
        int depth,
        int maxVariables)
    {
        if (variables.Count >= maxVariables || depth > 3)
        {
            return;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (variables.Count >= maxVariables)
                    {
                        return;
                    }

                    var childPath = $"{path}.{property.Name}";
                    variables.Add(new(property.Name, string.Empty, childPath, FormatJsonValue(property.Value), depth, JsonOnly: true));
                    AddJsonChildren(variables, property.Value, childPath, depth + 1, maxVariables);
                }
                break;

            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray().Take(8))
                {
                    if (variables.Count >= maxVariables)
                    {
                        return;
                    }

                    var childPath = $"{path}[{index}]";
                    variables.Add(new($"[{index}]", string.Empty, childPath, FormatJsonValue(item), depth, JsonOnly: true));
                    AddJsonChildren(variables, item, childPath, depth + 1, maxVariables);
                    index++;
                }
                break;
        }
    }

    private static string FormatJsonValue(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.Object => "{...}",
            JsonValueKind.Array => "[...]",
            JsonValueKind.String => element.GetString() ?? "",
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "null",
            _ => JsonSerializer.Serialize(element, JsonOptions)
        };

    private static string Shorten(string value)
        => value.Length <= 96 ? value : value[..93] + "...";

    private static string SerializeResult(object? value)
        => JsonSerializer.Serialize(value, JsonOptions);

    private static string ErrorJson(string message)
        => SerializeResult(new Dictionary<string, object?>
        {
            ["error"] = message
        });
}
