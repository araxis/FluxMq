using FluxMq.UI.Components.Diagram;
using FluxMq.UI.Models;
using System.Text.Json.Nodes;
using DiagramPoint = Blazor.Diagrams.Core.Geometry.Point;

namespace FluxMq.UI.Components.Workspace.Nodes;

public sealed class SessionSourceNodeModel(DiagramPoint position, string nodeName, FlowComponentDescriptor? descriptor, bool isResource)
    : FlowDiagramNodeModel(position, nodeName, "session.source", descriptor, isResource)
{
    public string SessionId { get; set; } = string.Empty;
    public bool PreserveTiming { get; set; }
    public double Speed { get; set; } = 1;
    public int BoundedCapacity { get; set; } = 1000;

    protected override void OnConfigurationLoaded(JsonObject? config)
    {
        SessionId = config?["sessionId"]?.GetValue<string?>() ?? string.Empty;
        PreserveTiming = ReadBool(config, "preserveTiming", false);
        Speed = ReadDouble(config, "speed", 1);
        BoundedCapacity = ReadInt(config, "boundedCapacity", 1000);
    }

    public override JsonObject BuildConfiguration() => new()
    {
        ["sessionId"] = SessionId,
        ["preserveTiming"] = PreserveTiming,
        ["speed"] = Speed <= 0 ? 1 : Speed,
        ["boundedCapacity"] = BoundedCapacity
    };

    private static bool ReadBool(JsonObject? root, string key, bool fallback)
    {
        if (root?[key] is JsonValue v && v.TryGetValue<bool>(out var r)) return r;
        return fallback;
    }

    private static double ReadDouble(JsonObject? root, string key, double fallback)
    {
        if (root?[key] is JsonValue v && v.TryGetValue<double>(out var r)) return r;
        return fallback;
    }

    private static int ReadInt(JsonObject? root, string key, int fallback)
    {
        if (root?[key] is JsonValue v && v.TryGetValue<int>(out var r)) return r;
        return fallback;
    }
}
