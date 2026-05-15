using FluxMq.UI.Components.Diagram;
using FluxMq.UI.Models;
using System.Text.Json.Nodes;
using DiagramPoint = Blazor.Diagrams.Core.Geometry.Point;

namespace FluxMq.UI.Components.Workspace.Nodes.MqttConnection;

public sealed class MqttConnectionNodeModel(DiagramPoint position, string nodeName, FlowComponentDescriptor? descriptor, bool isResource)
    : FlowDiagramNodeModel(position, nodeName, "mqtt.connection", descriptor, isResource)
{
    public string ProfileName { get; set; } = "broker";
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1883;
    public string ClientId { get; set; } = string.Empty;
    public bool UseTls { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public int KeepAliveSeconds { get; set; } = 60;
    public bool CleanStart { get; set; } = true;

    protected override void OnConfigurationLoaded(JsonObject? config)
    {
        if (config?["profile"] is not JsonObject profile) return;
        ProfileName = profile["name"]?.GetValue<string>() ?? "broker";
        Host = profile["host"]?.GetValue<string>() ?? "localhost";
        Port = ReadInt(profile, "port", 1883);
        ClientId = profile["clientId"]?.GetValue<string>() ?? string.Empty;
        UseTls = ReadBool(profile, "useTls", false);
        Username = profile["username"]?.GetValue<string?>();
        Password = profile["password"]?.GetValue<string?>();
        KeepAliveSeconds = ReadInt(profile, "keepAliveSeconds", 60);
        CleanStart = ReadBool(profile, "cleanStart", true);
    }

    public override JsonObject BuildConfiguration() => new()
    {
        ["profile"] = new JsonObject
        {
            ["name"] = ProfileName,
            ["host"] = string.IsNullOrWhiteSpace(Host) ? "localhost" : Host,
            ["port"] = Port,
            ["clientId"] = string.IsNullOrWhiteSpace(ClientId) ? Guid.NewGuid().ToString("N")[..8] : ClientId,
            ["useTls"] = UseTls,
            ["username"] = string.IsNullOrWhiteSpace(Username) ? null : Username,
            ["password"] = string.IsNullOrWhiteSpace(Password) ? null : Password,
            ["keepAliveSeconds"] = KeepAliveSeconds,
            ["cleanStart"] = CleanStart
        }
    };

    private static int ReadInt(JsonObject root, string key, int fallback)
    {
        if (root[key] is JsonValue v && v.TryGetValue<int>(out var r)) return r;
        return fallback;
    }

    private static bool ReadBool(JsonObject root, string key, bool fallback)
    {
        if (root[key] is JsonValue v && v.TryGetValue<bool>(out var r)) return r;
        return fallback;
    }
}
