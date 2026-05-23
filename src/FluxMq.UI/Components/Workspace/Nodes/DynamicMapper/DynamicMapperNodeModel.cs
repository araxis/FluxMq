using FluxMq.UI.Components.Diagram;
using FluxMq.UI.Models;
using System.Text.Json.Nodes;
using DiagramPoint = Blazor.Diagrams.Core.Geometry.Point;

namespace FluxMq.UI.Components.Workspace.Nodes.DynamicMapper;

public sealed class DynamicMapperNodeModel(string id, DiagramPoint position, string nodeName, FlowComponentDescriptor? descriptor, bool isResource)
    : FlowDiagramNodeModel(id, position, nodeName, "flow.mapper", descriptor, isResource)
{
    public string Engine { get; set; } = "dynamic-expresso";
    public string InputType { get; set; } = "MqttEnvelope";
    public string OutputType { get; set; } = "MqttPublishRequest";
    public Dictionary<string, string> Map { get; set; } = DefaultMap("MqttPublishRequest");

    protected override void OnConfigurationLoaded(JsonObject? config)
    {
        Engine = ReadString(config, "engine", "dynamic-expresso");
        InputType = ReadString(config, "inputType", "MqttEnvelope");
        OutputType = ReadString(config, "outputType", "MqttPublishRequest");
        Map = ReadMap(config) ?? DefaultMap(OutputType);
    }

    public override JsonObject BuildConfiguration()
    {
        var map = new JsonObject();
        foreach (var (key, value) in Map)
        {
            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
            {
                map[key] = value;
            }
        }

        var configuration = new JsonObject
        {
            ["engine"] = Engine,
            ["inputType"] = InputType,
            ["outputType"] = OutputType,
            ["map"] = map
        };

        if (OutputType == "MqttRecordingRequest" &&
            Map.TryGetValue("sessionId", out var sessionId) &&
            !string.IsNullOrWhiteSpace(sessionId))
        {
            configuration["sessionId"] = sessionId.Trim();
        }

        return configuration;
    }

    public static Dictionary<string, string> DefaultMap(string outputType)
        => outputType switch
        {
            "FileWriteRequest" => new(StringComparer.Ordinal)
            {
                ["path"] = "\"messages/\" + topic + \".txt\"",
                ["content"] = "payloadText",
                ["mode"] = "\"Append\"",
                ["createDirectory"] = "true"
            },
            "MqttRecordingRequest" => new(StringComparer.Ordinal)
            {
                ["sessionId"] = string.Empty
            },
            _ => new(StringComparer.Ordinal)
            {
                ["topic"] = "topic",
                ["payload"] = "payloadText",
                ["qos"] = "qos",
                ["retain"] = "retain"
            }
        };

    private static string ReadString(JsonObject? config, string key, string fallback)
        => config?[key]?.GetValue<string?>() is { Length: > 0 } value ? value : fallback;

    private static Dictionary<string, string>? ReadMap(JsonObject? config)
    {
        if (config?["map"] is not JsonObject map)
        {
            return null;
        }

        var result = map
            .Where(pair => pair.Value is JsonValue)
            .Select(pair => (pair.Key, Value: pair.Value!.GetValue<string?>() ?? string.Empty))
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

        return result.Count == 0 ? null : result;
    }
}
