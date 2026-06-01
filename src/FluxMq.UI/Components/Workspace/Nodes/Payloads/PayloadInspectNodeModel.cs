using FluxMq.UI.Components.Diagram;
using FluxMq.UI.Models;
using System.Text.Json.Nodes;
using DiagramPoint = Blazor.Diagrams.Core.Geometry.Point;

namespace FluxMq.UI.Components.Workspace.Nodes.Payloads;

public sealed class PayloadInspectNodeModel(
    string id,
    DiagramPoint position,
    string nodeName,
    FlowComponentDescriptor? descriptor,
    bool isResource)
    : FlowDiagramNodeModel(id, position, nodeName, "payload.inspect", descriptor, isResource)
{
    public const int DefaultMaxPreviewBytes = 1024;
    public const int DefaultMaxFormattedChars = 4096;
    public const int DefaultBoundedCapacity = 128;

    public int MaxPreviewBytes { get; set; } = DefaultMaxPreviewBytes;
    public int MaxFormattedChars { get; set; } = DefaultMaxFormattedChars;
    public bool DetectBase64 { get; set; } = true;
    public bool FormatJson { get; set; } = true;
    public bool FormatXml { get; set; } = true;
    public int BoundedCapacity { get; set; } = DefaultBoundedCapacity;

    protected override void OnConfigurationLoaded(JsonObject? config)
    {
        MaxPreviewBytes = ReadPositiveInt(config, "maxPreviewBytes", DefaultMaxPreviewBytes);
        MaxFormattedChars = ReadPositiveInt(config, "maxFormattedChars", DefaultMaxFormattedChars);
        DetectBase64 = ReadBool(config, "detectBase64", true);
        FormatJson = ReadBool(config, "formatJson", true);
        FormatXml = ReadBool(config, "formatXml", true);
        BoundedCapacity = ReadPositiveInt(config, "boundedCapacity", DefaultBoundedCapacity);
    }

    public override JsonObject BuildConfiguration() => new()
    {
        ["maxPreviewBytes"] = NormalizePositiveInt(MaxPreviewBytes, DefaultMaxPreviewBytes),
        ["maxFormattedChars"] = NormalizePositiveInt(MaxFormattedChars, DefaultMaxFormattedChars),
        ["detectBase64"] = DetectBase64,
        ["formatJson"] = FormatJson,
        ["formatXml"] = FormatXml,
        ["boundedCapacity"] = NormalizePositiveInt(BoundedCapacity, DefaultBoundedCapacity)
    };

    public static int NormalizePositiveInt(int value, int fallback)
        => value > 0 ? value : fallback;

    private static bool ReadBool(JsonObject? config, string key, bool fallback)
        => config?[key] is JsonValue value && value.TryGetValue<bool>(out var result)
            ? result
            : fallback;

    private static int ReadPositiveInt(JsonObject? config, string key, int fallback)
    {
        if (config?[key] is JsonValue value &&
            value.TryGetValue<int>(out var result) &&
            result > 0)
        {
            return result;
        }

        return fallback;
    }
}
