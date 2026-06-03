using FluxMq.Core.Models;
using FluxMq.UI.Components.Workspace.Nodes.MqttTrigger;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FluxMq.UI.Services;

public sealed partial class FlowDefinitionComposer
{
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
                    ["type"] = "mqtt.metrics",
                    ["Input"] = $"{TriggerNodeName}.Output",
                    ["configuration"] = CreateDefaultComponentConfiguration("mqtt.metrics")
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
        workflow[TriggerNodeName] = CreateTrigger(BrokerResourceName, subscription);
        RewriteInputLink(workflow, InspectorNodeName, $"{TriggerNodeName}.Output");
        RewriteInputLink(workflow, MetricsNodeName, $"{TriggerNodeName}.Output");

        return root.ToJsonString(Options);
    }

    /// <summary>
    /// Reads all connection profiles from a definition.
    /// Covers two storage shapes:
    ///   1. <c>resources[name].type == "mqtt.connection"</c> with the workspace monitor subscription.
    ///   2. Future workflow nodes that embed broker profile objects directly.
    /// </summary>
    public IReadOnlyList<(MqttConnectionProfile Profile, string Subscription)> ReadConnectionsFromDefinition(string json)
        => ReadConnectionResourcesFromDefinition(json)
            .Select(connection => (connection.Profile, connection.Subscription))
            .ToArray();

    public IReadOnlyList<(string Name, MqttConnectionProfile Profile, string Subscription)> ReadConnectionResourcesFromDefinition(string json)
    {
        var result = new List<(string, MqttConnectionProfile, string)>();
        using var doc = ParseDefinitionJson(json, "Read connection resources");
        var root = doc.RootElement;

        JsonElement flowApp;
        if (root.TryGetProperty("FluxMq", out var fluxMq) &&
            fluxMq.TryGetProperty("FlowApplication", out flowApp) &&
            flowApp.ValueKind == JsonValueKind.Object)
        {
        }
        else
        {
            flowApp = root;
        }

        var resourceProfiles = new Dictionary<string, MqttConnectionProfile>(StringComparer.Ordinal);
        if (flowApp.TryGetProperty("resources", out var resources) &&
            resources.ValueKind == JsonValueKind.Object)
        {
            foreach (var resource in resources.EnumerateObject())
            {
                if (!resource.Value.TryGetProperty("type", out var type) ||
                    type.GetString() != "mqtt.connection") continue;
                if (!resource.Value.TryGetProperty("configuration", out var config) ||
                    !config.TryGetProperty("profile", out var profileEl)) continue;

                resourceProfiles[resource.Name] = ReadProfile(profileEl);
            }
        }

        foreach (var (name, profile) in resourceProfiles)
            result.Add((name, profile, LiveMqttWorkspaceService.DefaultBrokerMonitorSubscription));

        return result;
    }

    /// <summary>
    /// Adds or replaces a single mqtt.connection resource by name, leaving all other
    /// nodes and resources untouched. Used when the trigger widget saves its broker selection.
    /// </summary>
    public string UpsertConnectionResource(string json, string resourceName, MqttConnectionProfile profile)
    {
        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        var resources = GetOrCreateObject(flowApplication, "resources");
        resources[resourceName] = CreateConnection(profile);
        return root.ToJsonString(Options);
    }

    /// <summary>
    /// Atomically syncs a connection resource and updates a node's configuration in one pass,
    /// emitting a single JSON string so callers only need one <see cref="FlowWorkspaceService"/> call.
    /// </summary>
    public string SyncConnectionAndSaveNode(
        string json,
        string resourceName,
        MqttConnectionProfile profile,
        string nodeName,
        JsonObject nodeConfiguration,
        string? workflowName = null)
    {
        var withResource = UpsertConnectionResource(json, resourceName, profile);
        return UpdateNodeConfiguration(withResource, nodeName, nodeConfiguration, workflowName);
    }

    private static MqttConnectionProfile ReadProfile(JsonElement profileEl) => new()
    {
        Name = profileEl.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
        Host = profileEl.TryGetProperty("host", out var h) ? h.GetString() ?? "localhost" : "localhost",
        Port = profileEl.TryGetProperty("port", out var p) ? p.GetInt32() : 1883,
        ClientId = profileEl.TryGetProperty("clientId", out var c) ? c.GetString() ?? "" : "",
        UseTls = profileEl.TryGetProperty("useTls", out var tls) && tls.GetBoolean(),
        KeepAlive = TimeSpan.FromSeconds(profileEl.TryGetProperty("keepAliveSeconds", out var ka) ? ka.GetInt32() : 60),
        CleanStart = !profileEl.TryGetProperty("cleanStart", out var cs) || cs.GetBoolean()
    };

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
                ["subscriptions"] = new JsonArray(new JsonObject
                {
                    ["topicFilter"] = string.IsNullOrWhiteSpace(subscription) ? "#" : subscription,
                    ["qos"] = MqttTriggerNodeModel.DefaultSubscriptionQos,
                    ["receiveRetained"] = true,
                    ["retainAsPublished"] = true
                }),
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

    private static string? FindFirstConnectionResourceName(JsonObject flowApplication)
    {
        if (flowApplication["resources"] is not JsonObject resources)
        {
            return null;
        }

        foreach (var resource in resources)
        {
            if (resource.Value is JsonObject resourceNode &&
                resourceNode["type"]?.GetValue<string?>() == "mqtt.connection")
            {
                return resource.Key;
            }
        }

        return null;
    }
}
