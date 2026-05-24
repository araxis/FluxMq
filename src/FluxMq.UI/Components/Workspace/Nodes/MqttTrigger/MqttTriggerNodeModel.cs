using FluxMq.UI.Components.Diagram;
using FluxMq.UI.Models;
using FluxMq.UI.Services;
using System.Text.Json.Nodes;
using DiagramPoint = Blazor.Diagrams.Core.Geometry.Point;

namespace FluxMq.UI.Components.Workspace.Nodes.MqttTrigger;

public sealed class MqttTriggerNodeModel(string id, DiagramPoint position, string nodeName, FlowComponentDescriptor? descriptor, bool isResource)
    : FlowDiagramNodeModel(id, position, nodeName, "mqtt.trigger", descriptor, isResource)
{
    public const int DefaultSubscriptionQos = 1;
    public string Connection { get; set; } = string.Empty;
    public List<MqttTriggerSubscriptionDraft> Subscriptions { get; set; } = [new("#", DefaultSubscriptionQos)];
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
        foreach (var subscription in Subscriptions)
        {
            subscriptions.Add(new JsonObject
            {
                ["topicFilter"] = string.IsNullOrWhiteSpace(subscription.TopicFilter) ? "#" : subscription.TopicFilter.Trim(),
                ["qos"] = NormalizeQualityOfService(subscription.QualityOfService),
                ["receiveRetained"] = subscription.ReceiveRetainedMessages,
                ["retainAsPublished"] = subscription.RetainAsPublished
            });
        }

        return new JsonObject
        {
            ["connection"] = string.IsNullOrWhiteSpace(Connection) ? FlowDefinitionComposer.BrokerResourceName : Connection,
            ["subscriptions"] = subscriptions,
            ["boundedCapacity"] = BoundedCapacity
        };
    }

    private static List<MqttTriggerSubscriptionDraft> ReadSubscriptions(JsonObject? config)
    {
        if (config?["subscriptions"] is not JsonArray array) return [new("#", DefaultSubscriptionQos)];

        var result = array
            .Select(n => n switch
            {
                JsonValue v when v.TryGetValue<string>(out var s) => new MqttTriggerSubscriptionDraft(s, DefaultSubscriptionQos),
                JsonObject o => new MqttTriggerSubscriptionDraft(
                    o["topicFilter"]?.GetValue<string?>() ?? string.Empty,
                    ReadQualityOfService(o["qos"]),
                    ReadBool(o["receiveRetained"] ?? o["retain"], defaultValue: true),
                    ReadBool(o["retainAsPublished"], defaultValue: true)),
                _ => new MqttTriggerSubscriptionDraft(string.Empty, DefaultSubscriptionQos)
            })
            .Where(s => !string.IsNullOrWhiteSpace(s.TopicFilter))
            .Select(s => s with
            {
                TopicFilter = s.TopicFilter.Trim(),
                QualityOfService = NormalizeQualityOfService(s.QualityOfService)
            })
            .ToList();
        return result.Count > 0 ? result : [new("#", DefaultSubscriptionQos)];
    }

    private static int ReadInt(JsonObject? root, string key, int fallback)
    {
        if (root?[key] is JsonValue v && v.TryGetValue<int>(out var r)) return r;
        return fallback;
    }

    private static int ReadQualityOfService(JsonNode? node)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<int>(out var number))
            {
                return NormalizeQualityOfService(number);
            }

            if (value.TryGetValue<string>(out var text))
            {
                return text.Trim().ToLowerInvariant() switch
                {
                    "0" or "atmostonce" => 0,
                    "1" or "atleastonce" => 1,
                    "2" or "exactlyonce" => 2,
                    _ => DefaultSubscriptionQos
                };
            }
        }

        return DefaultSubscriptionQos;
    }

    public static int NormalizeQualityOfService(int value)
        => value switch
        {
            <= 0 => 0,
            >= 2 => 2,
            _ => 1
        };

    private static bool ReadBool(JsonNode? node, bool defaultValue)
    {
        if (node is JsonValue value && value.TryGetValue<bool>(out var result))
        {
            return result;
        }

        return defaultValue;
    }
}

public sealed record MqttTriggerSubscriptionDraft(
    string TopicFilter,
    int QualityOfService,
    bool ReceiveRetainedMessages = true,
    bool RetainAsPublished = true);
