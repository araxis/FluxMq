using FluxMq.UI.Components.Diagram;
using FluxMq.UI.Models;
using System.Text.Json.Nodes;
using DiagramPoint = Blazor.Diagrams.Core.Geometry.Point;

namespace FluxMq.UI.Components.Workspace.Nodes.StateReducer;

public sealed class StateReducerNodeModel(
    string id,
    DiagramPoint position,
    string nodeName,
    FlowComponentDescriptor? descriptor,
    bool isResource)
    : FlowDiagramNodeModel(id, position, nodeName, "state.reducer", descriptor, isResource)
{
    public const string DefaultEngine = "jsonata";
    public const string DefaultReducer = "input";
    public const int DefaultBoundedCapacity = 128;
    public const int DefaultMaxKeys = 1024;

    public static readonly IReadOnlyList<string> Engines =
    [
        "jsonata",
        "dynamic-expresso"
    ];

    public string Engine { get; set; } = DefaultEngine;
    public string KeyExpression { get; set; } = string.Empty;
    public string Reducer { get; set; } = DefaultReducer;
    public string ExpressionName { get; set; } = string.Empty;
    public int BoundedCapacity { get; set; } = DefaultBoundedCapacity;
    public int MaxKeys { get; set; } = DefaultMaxKeys;

    protected override void OnConfigurationLoaded(JsonObject? config)
    {
        Engine = NormalizeEngine(ReadString(config, "engine", DefaultEngine));
        KeyExpression = ReadString(config, "keyExpression", string.Empty);
        Reducer = ReadString(config, "reducer", DefaultReducer);
        ExpressionName = ReadString(config, "expressionName", string.Empty);
        BoundedCapacity = NormalizeBoundedCapacity(ReadInt(config, "boundedCapacity", DefaultBoundedCapacity));
        MaxKeys = NormalizeMaxKeys(ReadInt(config, "maxKeys", DefaultMaxKeys));
    }

    public override JsonObject BuildConfiguration()
    {
        var config = new JsonObject
        {
            ["engine"] = NormalizeEngine(Engine),
            ["reducer"] = string.IsNullOrWhiteSpace(Reducer) ? DefaultReducer : Reducer.Trim(),
            ["boundedCapacity"] = NormalizeBoundedCapacity(BoundedCapacity),
            ["maxKeys"] = NormalizeMaxKeys(MaxKeys)
        };

        if (!string.IsNullOrWhiteSpace(KeyExpression))
        {
            config["keyExpression"] = KeyExpression.Trim();
        }

        if (!string.IsNullOrWhiteSpace(ExpressionName))
        {
            config["expressionName"] = ExpressionName.Trim();
        }

        return config;
    }

    public static string NormalizeEngine(string? value)
    {
        var trimmed = value?.Trim();
        return Engines.Contains(trimmed, StringComparer.Ordinal)
            ? trimmed!
            : DefaultEngine;
    }

    public static int NormalizeBoundedCapacity(int boundedCapacity)
        => boundedCapacity > 0 ? boundedCapacity : DefaultBoundedCapacity;

    public static int NormalizeMaxKeys(int maxKeys)
        => maxKeys >= 0 ? maxKeys : DefaultMaxKeys;

    private static string ReadString(JsonObject? config, string key, string fallback)
        => config?[key]?.GetValue<string?>() is { Length: > 0 } value ? value : fallback;

    private static int ReadInt(JsonObject? config, string key, int fallback)
    {
        if (config?[key] is JsonValue value && value.TryGetValue<int>(out var result))
        {
            return result;
        }

        return fallback;
    }
}
