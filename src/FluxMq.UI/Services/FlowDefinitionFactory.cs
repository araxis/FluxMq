using FluxMq.Core.Models;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FluxMq.UI.Services;

public sealed class FlowDefinitionFactory
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public string CreateInspectPayloadsDefinition(MqttConnectionProfile profile, string subscription)
    {
        var root = CreateRoot();
        var flowApplication = GetFlowApplication(root);
        flowApplication["resources"] = new JsonObject
        {
            ["source"] = CreateMessageSource(profile, subscription)
        };
        flowApplication["workflows"] = new JsonObject
        {
            ["inspectPayloads"] = new JsonObject
            {
                ["inspect"] = new JsonObject
                {
                    ["type"] = "mqtt.payload-inspector",
                    ["Input"] = "source.Output"
                },
                ["metrics"] = new JsonObject
                {
                    ["type"] = "mqtt.metrics-sink",
                    ["Input"] = "source.Output"
                }
            }
        };

        return root.ToJsonString(Options);
    }

    public string UpsertComponent(string json, string componentType, MqttConnectionProfile profile, string subscription)
    {
        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);

        if (componentType == "mqtt.message-source")
        {
            var resources = GetOrCreateObject(flowApplication, "resources");
            resources["source"] = CreateMessageSource(profile, subscription);
            return root.ToJsonString(Options);
        }

        var workflows = GetOrCreateObject(flowApplication, "workflows");
        var workflow = GetOrCreateObject(workflows, "inspectPayloads");
        var nodeName = componentType switch
        {
            "mqtt.payload-inspector" => "inspect",
            "mqtt.metrics-sink" => "metrics",
            _ => MakeNodeName(componentType)
        };

        workflow[nodeName] = new JsonObject
        {
            ["type"] = componentType,
            ["Input"] = "source.Output"
        };

        return root.ToJsonString(Options);
    }

    private static JsonObject CreateRoot()
        => new()
        {
            ["FluxMq"] = new JsonObject
            {
                ["FlowApplication"] = new JsonObject()
            }
        };

    private static JsonObject ParseOrCreate(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return CreateRoot();
        }

        var node = JsonNode.Parse(json);
        return node as JsonObject ?? CreateRoot();
    }

    private static JsonObject GetFlowApplication(JsonObject root)
    {
        var fluxMq = GetOrCreateObject(root, "FluxMq");
        return GetOrCreateObject(fluxMq, "FlowApplication");
    }

    private static JsonObject GetOrCreateObject(JsonObject parent, string propertyName)
    {
        if (parent[propertyName] is JsonObject existing)
        {
            return existing;
        }

        var created = new JsonObject();
        parent[propertyName] = created;
        return created;
    }

    private static JsonObject CreateMessageSource(MqttConnectionProfile profile, string subscription)
        => new()
        {
            ["type"] = "mqtt.message-source",
            ["configuration"] = new JsonObject
            {
                ["profile"] = new JsonObject
                {
                    ["name"] = string.IsNullOrWhiteSpace(profile.Name) ? "local-broker" : profile.Name,
                    ["host"] = profile.Host,
                    ["port"] = profile.Port,
                    ["clientId"] = profile.ClientId,
                    ["useTls"] = profile.UseTls,
                    ["keepAliveSeconds"] = (int)Math.Max(1, profile.KeepAlive.TotalSeconds),
                    ["cleanStart"] = profile.CleanStart
                },
                ["subscriptions"] = new JsonArray(string.IsNullOrWhiteSpace(subscription) ? "#" : subscription),
                ["boundedCapacity"] = 1000
            }
        };

    private static string MakeNodeName(string componentType)
    {
        var tail = componentType.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "node";
        var parts = tail.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return "node";
        }

        return parts[0] + string.Concat(parts.Skip(1).Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
    }
}
