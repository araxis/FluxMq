using FluxMq.UI.Components.Diagram;
using FluxMq.UI.Models;
using System.Text.Json.Nodes;
using DiagramPoint = Blazor.Diagrams.Core.Geometry.Point;

namespace FluxMq.UI.Components.Workspace.Nodes.SessionSource;

public sealed class SessionSourceNodeModel(string id, DiagramPoint position, string nodeName, FlowComponentDescriptor? descriptor, bool isResource)
    : FlowDiagramNodeModel(id, position, nodeName, "session.source", descriptor, isResource)
{
    public const double DefaultSpeed = 1;
    public const int DefaultBoundedCapacity = 1000;

    public string SessionId { get; set; } = string.Empty;
    public bool PreserveTiming { get; set; }
    public double Speed { get; set; } = DefaultSpeed;
    public int BoundedCapacity { get; set; } = DefaultBoundedCapacity;

    protected override void OnConfigurationLoaded(JsonObject? config)
    {
        SessionId = ReadString(config, "sessionId", string.Empty);
        PreserveTiming = ReadBool(config, "preserveTiming", false);
        Speed = NormalizeSpeed(ReadDouble(config, "speed", DefaultSpeed));
        BoundedCapacity = NormalizeBoundedCapacity(ReadInt(config, "boundedCapacity", DefaultBoundedCapacity));
    }

    public override JsonObject BuildConfiguration() => new()
    {
        ["sessionId"] = SessionId.Trim(),
        ["preserveTiming"] = PreserveTiming,
        ["speed"] = NormalizeSpeed(Speed),
        ["boundedCapacity"] = NormalizeBoundedCapacity(BoundedCapacity)
    };

    public static double NormalizeSpeed(double value)
        => value > 0 && double.IsFinite(value) ? value : DefaultSpeed;

    public static int NormalizeBoundedCapacity(int value)
        => value > 0 ? value : DefaultBoundedCapacity;

    private static string ReadString(JsonObject? root, string key, string fallback)
    {
        if (root?[key] is JsonValue v && v.TryGetValue<string?>(out var r) && !string.IsNullOrWhiteSpace(r)) return r;
        return fallback;
    }

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
