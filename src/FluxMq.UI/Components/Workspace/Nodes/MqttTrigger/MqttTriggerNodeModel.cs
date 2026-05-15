using FluxMq.UI.Components.Diagram;
using FluxMq.UI.Models;
using FluxMq.UI.Services;
using System.Text.Json.Nodes;
using DiagramPoint = Blazor.Diagrams.Core.Geometry.Point;

namespace FluxMq.UI.Components.Workspace.Nodes.MqttTrigger;

public sealed class MqttTriggerNodeModel(DiagramPoint position, string nodeName, FlowComponentDescriptor? descriptor, bool isResource)
    : FlowDiagramNodeModel(position, nodeName, "mqtt.trigger", descriptor, isResource)
{
    public string Connection { get; set; } = string.Empty;
    public string[] Subscriptions { get; set; } = ["#"];
    public int BoundedCapacity { get; set; } = 1000;

    protected override void OnConfigurationLoaded(JsonObject? config)
    {
        Connection = config?["connection"]?.GetValue<string?>() ?? string.Empty;
        Subscriptions = ReadSubscriptions(config);
        BoundedCapacity = ReadInt(config, "boundedCapacity", 1000);
    }

    public override JsonObject BuildConfiguration()
    {
        var subscriptions = new JsonArray();
        foreach (var s in Subscriptions) subscriptions.Add(s);
        return new JsonObject
        {
            ["connection"] = string.IsNullOrWhiteSpace(Connection) ? FlowDefinitionComposer.BrokerResourceName : Connection,
            ["subscriptions"] = subscriptions,
            ["boundedCapacity"] = BoundedCapacity
        };
    }

    private static string[] ReadSubscriptions(JsonObject? config)
    {
        if (config?["subscriptions"] is not JsonArray array) return ["#"];
        var result = array
            .Select(n => n switch
            {
                JsonValue v when v.TryGetValue<string>(out var s) => s,
                JsonObject o => o["topicFilter"]?.GetValue<string?>() ?? string.Empty,
                _ => string.Empty
            })
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToArray();
        return result.Length > 0 ? result : ["#"];
    }

    private static int ReadInt(JsonObject? root, string key, int fallback)
    {
        if (root?[key] is JsonValue v && v.TryGetValue<int>(out var r)) return r;
        return fallback;
    }
}
