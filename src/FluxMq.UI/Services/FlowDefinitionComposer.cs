using FluxMq.Core.Models;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FluxMq.UI.Services;

/// <summary>
/// Builds JSON flow definitions for the desktop workspace. The default shape is:
///   workflows.inspectPayloads.traffic  (traffic.source)
///   workflows.inspectPayloads.inspect  (mqtt.payload-inspector &lt;- traffic.Output)
///   workflows.inspectPayloads.metrics  (mqtt.metrics-sink &lt;- traffic.Output)
/// </summary>
public sealed class FlowDefinitionComposer
{
    public const string BrokerResourceName = "broker";
    public const string TriggerNodeName = "trigger";
    public const string TrafficSourceNodeName = "traffic";
    public const string InspectorNodeName = "inspect";
    public const string MetricsNodeName = "metrics";
    public const string DefaultWorkflowName = "inspectPayloads";

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
            [BrokerResourceName] = CreateConnection(profile)
        };
        flowApplication["workflows"] = new JsonObject
        {
            [DefaultWorkflowName] = new JsonObject
            {
                [TriggerNodeName] = CreateTrigger(BrokerResourceName, subscription),
                [InspectorNodeName] = new JsonObject
                {
                    ["type"] = "mqtt.payload-inspector",
                    ["Input"] = $"{TriggerNodeName}.Output"
                },
                [MetricsNodeName] = new JsonObject
                {
                    ["type"] = "mqtt.metrics-sink",
                    ["Input"] = $"{TriggerNodeName}.Output"
                }
            }
        };

        return root.ToJsonString(Options);
    }

    /// <summary>
    /// Updates the broker resource and trigger node in an existing definition.
    /// Creates the resource and trigger if absent.
    /// </summary>
    public string UpsertBroker(string json, MqttConnectionProfile profile, string subscription)
    {
        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);

        var resources = GetOrCreateObject(flowApplication, "resources");
        resources[BrokerResourceName] = CreateConnection(profile);

        var workflows = GetOrCreateObject(flowApplication, "workflows");
        var workflow = GetOrCreateObject(workflows, DefaultWorkflowName);
        workflow.Remove(TrafficSourceNodeName);
        workflow[TriggerNodeName] = CreateTrigger(BrokerResourceName, subscription);
        RewriteInputLink(workflow, InspectorNodeName, $"{TriggerNodeName}.Output");
        RewriteInputLink(workflow, MetricsNodeName, $"{TriggerNodeName}.Output");

        return root.ToJsonString(Options);
    }

    /// <summary>
    /// Replaces the configuration object of a single node identified by its
    /// resource or workflow-node name. Returns the original JSON unchanged
    /// when the node can't be located. Used by per-node editor widgets.
    /// </summary>
    public string UpdateNodeConfiguration(string json, string nodeName, JsonObject configuration)
    {
        if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(nodeName))
        {
            return json;
        }

        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);

        // Resources first.
        if (flowApplication["resources"] is JsonObject resources && resources[nodeName] is JsonObject resourceNode)
        {
            resourceNode["configuration"] = configuration;
            return root.ToJsonString(Options);
        }

        // Then workflows.
        if (flowApplication["workflows"] is JsonObject workflows)
        {
            foreach (var workflow in workflows)
            {
                if (workflow.Value is JsonObject workflowObject && workflowObject[nodeName] is JsonObject workflowNode)
                {
                    workflowNode["configuration"] = configuration;
                    return root.ToJsonString(Options);
                }
            }
        }

        return json;
    }

    /// <summary>Adds a downstream component (inspector, metrics-sink, ...) wired to the trigger output.</summary>
    public string AddComponent(string json, string componentType)
    {
        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        var workflows = GetOrCreateObject(flowApplication, "workflows");
        var workflow = GetOrCreateObject(workflows, DefaultWorkflowName);

        var nodeName = componentType switch
        {
            "mqtt.payload-inspector" => InspectorNodeName,
            "mqtt.metrics-sink" => MetricsNodeName,
            _ => MakeNodeName(componentType)
        };

        var sourceNodeName = workflow.ContainsKey(TriggerNodeName)
            ? TriggerNodeName
            : TrafficSourceNodeName;

        workflow[nodeName] = new JsonObject
        {
            ["type"] = componentType,
            ["Input"] = $"{sourceNodeName}.Output"
        };

        return root.ToJsonString(Options);
    }

    private static JsonObject CreateConnection(MqttConnectionProfile profile)
        => new()
        {
            ["type"] = "mqtt.connection",
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
                }
            }
        };

    private static JsonObject CreateTrigger(string connectionRef, string subscription)
        => new()
        {
            ["type"] = "mqtt.trigger",
            ["configuration"] = new JsonObject
            {
                ["connection"] = connectionRef,
                ["subscriptions"] = new JsonArray(string.IsNullOrWhiteSpace(subscription) ? "#" : subscription),
                ["boundedCapacity"] = 1000
            }
        };

    private static JsonObject CreateTrafficSource(MqttConnectionProfile profile, string subscription)
        => new()
        {
            ["type"] = "traffic.source",
            ["configuration"] = new JsonObject
            {
                ["kind"] = "live",
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

    private static void RewriteInputLink(JsonObject workflow, string nodeName, string link)
    {
        if (workflow[nodeName] is JsonObject node)
        {
            node["Input"] = link;
        }
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
