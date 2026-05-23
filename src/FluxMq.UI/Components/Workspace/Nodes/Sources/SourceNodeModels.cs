using FluxMq.UI.Components.Diagram;
using FluxMq.UI.Models;
using System.Text.Json.Nodes;
using DiagramPoint = Blazor.Diagrams.Core.Geometry.Point;

namespace FluxMq.UI.Components.Workspace.Nodes.Sources;

public sealed class GeneratedSourceNodeModel(
    string id,
    DiagramPoint position,
    string nodeName,
    FlowComponentDescriptor? descriptor,
    bool isResource)
    : FlowDiagramNodeModel(id, position, nodeName, "generated.source", descriptor, isResource)
{
    public List<GeneratedMessageDraft> Messages { get; set; } =
    [
        new()
        {
            Topic = "factory/sample",
            Payload = """{"value":21.7,"unit":"c","status":"ok"}""",
            QualityOfService = 0,
            Retain = false
        }
    ];

    public int BoundedCapacity { get; set; } = SourceNodeConfiguration.DefaultBoundedCapacity;

    protected override void OnConfigurationLoaded(JsonObject? config)
    {
        Messages = SourceNodeConfiguration.ReadGeneratedMessages(config?["messages"]);
        BoundedCapacity = SourceNodeConfiguration.ReadBoundedCapacity(config);
    }

    public override JsonObject BuildConfiguration() => new()
    {
        ["messages"] = SourceNodeConfiguration.BuildGeneratedMessages(Messages),
        ["boundedCapacity"] = SourceNodeConfiguration.NormalizeBoundedCapacity(BoundedCapacity)
    };
}

public sealed class ReplaySourceNodeModel(
    string id,
    DiagramPoint position,
    string nodeName,
    FlowComponentDescriptor? descriptor,
    bool isResource)
    : FlowDiagramNodeModel(id, position, nodeName, "replay.source", descriptor, isResource)
{
    public string SessionId { get; set; } = string.Empty;
    public double Speed { get; set; } = 1;
    public int BoundedCapacity { get; set; } = SourceNodeConfiguration.DefaultBoundedCapacity;

    protected override void OnConfigurationLoaded(JsonObject? config)
    {
        SessionId = SourceNodeConfiguration.ReadString(config, "sessionId", string.Empty);
        Speed = SourceNodeConfiguration.ReadPositiveDouble(config, "speed", 1);
        BoundedCapacity = SourceNodeConfiguration.ReadBoundedCapacity(config);
    }

    public override JsonObject BuildConfiguration() => new()
    {
        ["sessionId"] = SessionId.Trim(),
        ["speed"] = Speed > 0 && double.IsFinite(Speed) ? Speed : 1,
        ["boundedCapacity"] = SourceNodeConfiguration.NormalizeBoundedCapacity(BoundedCapacity)
    };
}

public sealed class GeneratedMessageDraft
{
    public string Topic { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public int QualityOfService { get; set; }
    public bool Retain { get; set; }
    public string ReceivedAt { get; set; } = string.Empty;
}

internal static class SourceNodeConfiguration
{
    public const int DefaultBoundedCapacity = 1000;

    public static JsonArray BuildGeneratedMessages(IEnumerable<GeneratedMessageDraft> messages)
    {
        var array = new JsonArray();
        var normalized = messages
            .Where(message => !string.IsNullOrWhiteSpace(message.Topic))
            .ToArray();

        if (normalized.Length == 0)
        {
            normalized =
            [
                new GeneratedMessageDraft
                {
                    Topic = "factory/sample",
                    Payload = """{"value":21.7,"unit":"c","status":"ok"}"""
                }
            ];
        }

        foreach (var message in normalized)
        {
            var item = new JsonObject
            {
                ["topic"] = message.Topic.Trim(),
                ["payload"] = message.Payload ?? string.Empty,
                ["payloadEncoding"] = "utf8",
                ["qos"] = NormalizeQualityOfService(message.QualityOfService),
                ["retain"] = message.Retain
            };

            if (!string.IsNullOrWhiteSpace(message.ReceivedAt))
            {
                item["receivedAt"] = message.ReceivedAt.Trim();
            }

            array.Add(item);
        }

        return array;
    }

    public static List<GeneratedMessageDraft> ReadGeneratedMessages(JsonNode? node)
    {
        var result = new List<GeneratedMessageDraft>();
        if (node is JsonArray array)
        {
            foreach (var item in array.OfType<JsonObject>())
            {
                var payload = item["payload"] switch
                {
                    JsonValue value when value.TryGetValue<string>(out var text) => text,
                    JsonArray bytes => bytes.ToJsonString(),
                    null => string.Empty,
                    var other => other.ToJsonString()
                };

                result.Add(new GeneratedMessageDraft
                {
                    Topic = ReadString(item, "topic", string.Empty),
                    Payload = payload,
                    QualityOfService = ReadQualityOfService(item["qos"]),
                    Retain = ReadBool(item, "retain", false),
                    ReceivedAt = ReadString(item, "receivedAt", string.Empty)
                });
            }
        }

        return result.Count > 0
            ? result
            :
            [
                new GeneratedMessageDraft
                {
                    Topic = "factory/sample",
                    Payload = """{"value":21.7,"unit":"c","status":"ok"}"""
                }
            ];
    }

    public static int ReadBoundedCapacity(JsonObject? config)
        => NormalizeBoundedCapacity(ReadInt(config, "boundedCapacity", DefaultBoundedCapacity));

    public static int NormalizeBoundedCapacity(int boundedCapacity)
        => boundedCapacity > 0 ? boundedCapacity : DefaultBoundedCapacity;

    public static string ReadString(JsonObject? config, string key, string fallback)
        => config?[key] is JsonValue value && value.TryGetValue<string?>(out var result) && !string.IsNullOrWhiteSpace(result)
            ? result
            : fallback;

    public static double ReadPositiveDouble(JsonObject? config, string key, double fallback)
    {
        if (config?[key] is JsonValue value &&
            value.TryGetValue<double>(out var result) &&
            result > 0 &&
            double.IsFinite(result))
        {
            return result;
        }

        return fallback;
    }

    private static int ReadInt(JsonObject? config, string key, int fallback)
    {
        if (config?[key] is JsonValue value && value.TryGetValue<int>(out var result) && result > 0)
        {
            return result;
        }

        return fallback;
    }

    private static bool ReadBool(JsonObject? config, string key, bool fallback)
    {
        if (config?[key] is JsonValue value && value.TryGetValue<bool>(out var result))
        {
            return result;
        }

        return fallback;
    }

    private static int ReadQualityOfService(JsonNode? node)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<int>(out var intValue))
            {
                return NormalizeQualityOfService(intValue);
            }

            if (value.TryGetValue<string?>(out var textValue))
            {
                return textValue?.Trim().ToLowerInvariant() switch
                {
                    "0" or "atmostonce" => 0,
                    "1" or "atleastonce" => 1,
                    "2" or "exactlyonce" => 2,
                    _ => 0
                };
            }
        }

        return 0;
    }

    public static int NormalizeQualityOfService(int value)
        => value is >= 0 and <= 2 ? value : 0;
}
