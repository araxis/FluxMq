using FluxMq.Core.Models;
using FluxMq.UI.Components.Workspace.Nodes.DynamicMapper;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FluxMq.UI.Services;

/// <summary>
/// Builds JSON flow definitions for the desktop workspace. The default shape uses
/// explicit live, stored-session, replay, and generated source nodes.
/// </summary>
public sealed class FlowDefinitionComposer
{
    public const string BrokerResourceName = "broker";
    public const string TriggerNodeName = "trigger";
    public const string LiveSourceNodeName = "live";
    public const string StoredSourceNodeName = "stored";
    public const string InspectorNodeName = "inspect";
    public const string MetricsNodeName = "metrics";
    public const string FilterNodeName = "filter";
    public const string RouterNodeName = "router";
    public const string MapperNodeName = "mapper";
    public const string RecorderNodeName = "recorder";
    public const string PublisherNodeName = "publisher";
    public const string StateSourceNodeName = "state";
    public const string ReplayNodeName = "replay";
    public const string GeneratedNodeName = "generated";
    public const string DefaultWorkflowName = "inspectPayloads";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public string CreateEmptyDefinition() => CreateRoot().ToJsonString(Options);

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

    /// <summary>
    /// Renames a workflow node and rewrites all intra-workflow references that point to it.
    /// No-ops when old and new names are equal or the node cannot be found.
    /// </summary>
    public string RenameWorkflowNode(string json, string? workflowName, string oldName, string newName)
    {
        if (string.IsNullOrWhiteSpace(workflowName) ||
            string.IsNullOrWhiteSpace(oldName) ||
            string.IsNullOrWhiteSpace(newName) ||
            string.Equals(oldName, newName, StringComparison.Ordinal))
            return json;

        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);

        if (flowApplication["workflows"] is not JsonObject workflows ||
            workflows[workflowName] is not JsonObject workflow)
            return json;

        var nodeDef = workflow[oldName];
        if (nodeDef is null) return json;

        workflow.Remove(oldName);
        workflow[newName] = nodeDef;

        var prefix = oldName + ".";
        foreach (var (_, node) in workflow.AsEnumerable().ToList())
        {
            if (node is not JsonObject nodeObj) continue;
            var updates = nodeObj
                .Where(p => p.Value is JsonValue jv &&
                             jv.TryGetValue<string>(out var s) &&
                             s.StartsWith(prefix, StringComparison.Ordinal))
                .Select(p => (p.Key, newName + p.Value!.GetValue<string>()[oldName.Length..]))
                .ToList();
            foreach (var (key, val) in updates)
                nodeObj[key] = val;
        }

        return root.ToJsonString(Options);
    }

    /// <summary>Returns (name, type) pairs for all nodes in a specific workflow.</summary>
    public IReadOnlyList<(string Name, string Type)> GetWorkflowNodes(string json, string workflowName)
    {
        var result = new List<(string, string)>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            JsonElement flowApp;
            if (root.TryGetProperty("FluxMq", out var fluxMq) &&
                fluxMq.TryGetProperty("FlowApplication", out flowApp) &&
                flowApp.ValueKind == JsonValueKind.Object)
            { }
            else
            {
                flowApp = root;
            }

            if (flowApp.TryGetProperty("workflows", out var workflows) &&
                workflows.ValueKind == JsonValueKind.Object &&
                workflows.TryGetProperty(workflowName, out var workflow) &&
                workflow.ValueKind == JsonValueKind.Object)
            {
                foreach (var node in workflow.EnumerateObject())
                {
                    var type = node.Value.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";
                    result.Add((node.Name, type));
                }
            }
        }
        catch { }
        return result;
    }

    /// <summary>Returns the ordered list of workflow names in the definition.</summary>
    public IReadOnlyList<string> GetWorkflowNames(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            JsonElement flowApp;
            if (root.TryGetProperty("FluxMq", out var fluxMq) &&
                fluxMq.TryGetProperty("FlowApplication", out flowApp) &&
                flowApp.ValueKind == JsonValueKind.Object)
            { }
            else
            {
                flowApp = root;
            }

            if (flowApp.TryGetProperty("workflows", out var workflows) &&
                workflows.ValueKind == JsonValueKind.Object)
            {
                return workflows.EnumerateObject().Select(w => w.Name).ToArray();
            }
        }
        catch { }
        return [];
    }

    /// <summary>Adds an empty workflow with the given name if it does not already exist.</summary>
    public string AddWorkflow(string json, string name)
    {
        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        var workflows = GetOrCreateObject(flowApplication, "workflows");
        if (!workflows.ContainsKey(name))
            workflows[name] = new JsonObject();
        return root.ToJsonString(Options);
    }

    /// <summary>Removes a workflow by name, leaving the definition unchanged if it doesn't exist.</summary>
    public string RemoveWorkflow(string json, string name)
    {
        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        if (flowApplication["workflows"] is JsonObject workflows)
            workflows.Remove(name);
        return root.ToJsonString(Options);
    }

    /// <summary>Adds a component and, when appropriate, wires it to the current explicit source node.</summary>
    public string AddComponent(string json, string componentType, string? targetWorkflowName = null)
    {
        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        var workflows = GetOrCreateObject(flowApplication, "workflows");
        var workflow = GetOrCreateObject(workflows, targetWorkflowName ?? DefaultWorkflowName);

        var nodeName = componentType switch
        {
            "mqtt.payload-inspector" => InspectorNodeName,
            "mqtt.metrics" => MetricsNodeName,
            "mqtt.message-filter" => FilterNodeName,
            "mqtt.condition-router" => RouterNodeName,
            "json.schema-validator" => "jsonSchemaValidator",
            "flow.mapper" => MakeUniqueNodeName(workflow, MapperNodeName),
            "mqtt.recording-request" => RecorderNodeName,
            "mqtt.recorder" => RecorderNodeName,
            "mqtt.publish-request" => PublisherNodeName,
            "mqtt.publisher" => PublisherNodeName,
            "file.write-request" => "fileWriteRequest",
            "file.writer" => "fileWriter",
            "mqtt.connection-state-trigger" => StateSourceNodeName,
            "mqtt.live-source" => LiveSourceNodeName,
            "replay.source" => ReplayNodeName,
            "generated.source" => GeneratedNodeName,
            "session.source" => StoredSourceNodeName,
            _ => MakeNodeName(componentType)
        };

        var node = new JsonObject
        {
            ["type"] = componentType
        };

        if (componentType == "flow.mapper")
        {
            node["configuration"] = CreateDynamicMapperConfiguration("MqttPublishRequest");
        }
        else if (componentType == "json.schema-validator")
        {
            node["configuration"] = CreateJsonSchemaValidatorConfiguration();
        }

        if (FindDefaultInputLink(componentType, workflow) is { Length: > 0 } inputLink)
        {
            node["Input"] = inputLink;
        }

        workflow[nodeName] = node;

        return root.ToJsonString(Options);
    }

    /// <summary>
    /// Embeds node positions and collapsed state under <c>FluxMq.Designer.nodes</c> in the JSON.
    /// Only the canvas section is touched; the flow definition is unchanged.
    /// </summary>
    public string WriteNodePositions(string json, IReadOnlyDictionary<string, (double X, double Y, bool Collapsed)> positions)
    {
        var root = ParseOrCreate(json);
        var fluxMq = GetOrCreateObject(root, "FluxMq");
        var designer = GetOrCreateObject(fluxMq, "Designer");
        var nodes = new JsonObject();
        foreach (var (name, (x, y, collapsed)) in positions)
        {
            nodes[name] = new JsonObject
            {
                ["x"] = x,
                ["y"] = y,
                ["collapsed"] = collapsed
            };
        }
        designer["nodes"] = nodes;
        return root.ToJsonString(Options);
    }

    /// <summary>
    /// Reads node positions previously written by <see cref="WriteNodePositions"/>.
    /// Returns an empty dictionary when no designer section exists.
    /// </summary>
    public IReadOnlyDictionary<string, (double X, double Y, bool Collapsed)> ReadNodePositions(string json)
    {
        var result = new Dictionary<string, (double X, double Y, bool Collapsed)>(StringComparer.Ordinal);
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("FluxMq", out var fluxMq) &&
                fluxMq.TryGetProperty("Designer", out var designer) &&
                designer.TryGetProperty("nodes", out var nodes) &&
                nodes.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                foreach (var node in nodes.EnumerateObject())
                {
                    var x = node.Value.TryGetProperty("x", out var xp) ? xp.GetDouble() : 0;
                    var y = node.Value.TryGetProperty("y", out var yp) ? yp.GetDouble() : 0;
                    var collapsed = node.Value.TryGetProperty("collapsed", out var cp) && cp.GetBoolean();
                    result[node.Name] = (x, y, collapsed);
                }
            }
        }
        catch { }
        return result;
    }

    /// <summary>
    /// Reads all connection profiles from a definition.
    /// Covers two storage shapes:
    ///   1. <c>resources[name].type == "mqtt.connection"</c> with subscription from the first <c>mqtt.trigger</c> that references it.
    ///   2. Workflow nodes that embed a <c>profile</c> object directly (e.g. <c>mqtt.live-source</c>).
    /// </summary>
    public IReadOnlyList<(MqttConnectionProfile Profile, string Subscription)> ReadConnectionsFromDefinition(string json)
    {
        var result = new List<(MqttConnectionProfile, string)>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            JsonElement flowApp;
            if (root.TryGetProperty("FluxMq", out var fluxMq) &&
                fluxMq.TryGetProperty("FlowApplication", out flowApp) &&
                flowApp.ValueKind == JsonValueKind.Object)
            { }
            else
            {
                flowApp = root;
            }

            var resourceProfiles = new Dictionary<string, MqttConnectionProfile>(StringComparer.Ordinal);
            var triggerSubscriptions = new Dictionary<string, string>(StringComparer.Ordinal);

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

            if (flowApp.TryGetProperty("workflows", out var workflows) &&
                workflows.ValueKind == JsonValueKind.Object)
            {
                foreach (var workflow in workflows.EnumerateObject())
                {
                    if (workflow.Value.ValueKind != JsonValueKind.Object) continue;
                    foreach (var node in workflow.Value.EnumerateObject())
                    {
                        if (!node.Value.TryGetProperty("type", out var nodeType)) continue;
                        var typeStr = nodeType.GetString();
                        if (!node.Value.TryGetProperty("configuration", out var conf)) continue;

                        if (typeStr == "mqtt.trigger")
                        {
                            if (!conf.TryGetProperty("connection", out var connRef) ||
                                connRef.GetString() is not { Length: > 0 } connName) continue;
                            if (!triggerSubscriptions.ContainsKey(connName))
                                triggerSubscriptions[connName] = ReadSubscriptionString(conf);
                        }
                        else if (typeStr == "mqtt.live-source")
                        {
                            if (!conf.TryGetProperty("profile", out var profileEl)) continue;
                            result.Add((ReadProfile(profileEl), ReadSubscriptionString(conf)));
                        }
                    }
                }
            }

            foreach (var (name, profile) in resourceProfiles)
                result.Add((profile, triggerSubscriptions.TryGetValue(name, out var sub) ? sub : "#"));
        }
        catch { }

        return result;
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

    private static string ReadSubscriptionString(JsonElement conf)
    {
        if (!conf.TryGetProperty("subscriptions", out var subs) ||
            subs.ValueKind != JsonValueKind.Array)
            return "#";
        var parts = new List<string>();
        foreach (var s in subs.EnumerateArray())
            if (s.GetString() is { } str) parts.Add(str);
        return parts.Count > 0 ? string.Join(", ", parts) : "#";
    }

    /// <summary>
    /// Returns the JSON with the <c>FluxMq.Designer</c> section removed so that
    /// the in-memory definition stays clean of UI-only data.
    /// </summary>
    public string StripDesignerSection(string json)
    {
        var root = ParseOrCreate(json);
        if (root["FluxMq"] is JsonObject fluxMq)
            fluxMq.Remove("Designer");
        return root.ToJsonString(Options);
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
        JsonObject nodeConfiguration)
    {
        var withResource = UpsertConnectionResource(json, resourceName, profile);
        return UpdateNodeConfiguration(withResource, nodeName, nodeConfiguration);
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

    private static void RewriteInputLink(JsonObject workflow, string nodeName, string link)
    {
        if (workflow[nodeName] is JsonObject node)
        {
            node["Input"] = link;
        }
    }

    private static bool NeedsInputLink(string componentType) => componentType switch
    {
        "mqtt.trigger" or "mqtt.live-source" or "session.source" or "replay.source" or "generated.source" or "mqtt.connection-state-trigger" => false,
        _ => true
    };

    private static string? FindDefaultInputLink(string componentType, JsonObject workflow)
    {
        if (!NeedsInputLink(componentType))
        {
            return null;
        }

        if (IsActor(componentType))
        {
            return FindPreferredMapperNode(workflow) is { Length: > 0 } mapper
                ? $"{mapper}.Output"
                : null;
        }

        return FindPreferredSourceNode(workflow) is { Length: > 0 } source
            ? $"{source}.Output"
            : null;
    }

    private static bool IsActor(string componentType)
        => componentType is "mqtt.publisher" or "mqtt.recorder" or "file.writer";

    private static string? FindPreferredSourceNode(JsonObject workflow)
    {
        foreach (var preferredName in new[] { TriggerNodeName, LiveSourceNodeName, StoredSourceNodeName })
        {
            if (workflow.ContainsKey(preferredName)) return preferredName;
        }

        foreach (var node in workflow)
        {
            if (node.Value is not JsonObject nodeObject || nodeObject["type"]?.GetValue<string?>() is not { } type)
            {
                continue;
            }

            if (!NeedsInputLink(type)) return node.Key;
        }

        return null;
    }

    private static string? FindPreferredMapperNode(JsonObject workflow)
    {
        foreach (var node in workflow)
        {
            if (node.Value is JsonObject nodeObject &&
                nodeObject["type"]?.GetValue<string?>() is "flow.mapper")
            {
                return node.Key;
            }
        }

        return null;
    }

    private static JsonObject CreateDynamicMapperConfiguration(string outputType)
        => new()
        {
            ["engine"] = "jsonata",
            ["inputType"] = "MqttEnvelope",
            ["outputType"] = outputType,
            ["outputContract"] = "typed",
            ["expression"] = DynamicMapperNodeModel.DefaultExpression(outputType, "jsonata")
        };

    private static JsonObject CreateJsonSchemaValidatorConfiguration()
        => new()
        {
            ["schemaId"] = "payload-object",
            ["schema"] = """
            {
              "type": "object"
            }
            """
        };

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

    private static string MakeUniqueNodeName(JsonObject workflow, string preferred)
    {
        if (!workflow.ContainsKey(preferred))
        {
            return preferred;
        }

        var index = 2;
        while (workflow.ContainsKey($"{preferred}{index}"))
        {
            index++;
        }

        return $"{preferred}{index}";
    }
}
