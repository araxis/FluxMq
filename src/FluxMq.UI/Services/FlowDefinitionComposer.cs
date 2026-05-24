using FluxMq.Core.Models;
using FluxMq.UI.Components.Workspace.Nodes.DynamicMapper;
using FluxMq.UI.Components.Workspace.Nodes.MqttTrigger;
using FluxMq.UI.Components.Workspace.Nodes.Sources;
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
    public const string StoredSourceNodeName = "stored";
    public const string InspectorNodeName = "inspect";
    public const string MetricsNodeName = "metrics";
    public const string FilterNodeName = "filter";
    public const string RouterNodeName = "router";
    public const string MapperNodeName = "mapper";
    public const string LoggerNodeName = "logger";
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
    public string UpdateNodeConfiguration(string json, string nodeName, JsonObject configuration, string? workflowName = null)
    {
        if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(nodeName))
        {
            return json;
        }

        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);

        if (!string.IsNullOrWhiteSpace(workflowName) &&
            TryUpdateWorkflowNodeConfiguration(flowApplication, workflowName, nodeName, configuration))
        {
            return root.ToJsonString(Options);
        }

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
                if (workflow.Value is JsonObject workflowObject &&
                    UpdateWorkflowNodeConfiguration(workflowObject, nodeName, configuration))
                {
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

    public string ConnectWorkflowPorts(
        string json,
        string? workflowName,
        string sourceNodeName,
        string sourcePortName,
        string targetNodeName,
        string targetPortName,
        bool replaceTargetPortLinks = true)
    {
        if (string.IsNullOrWhiteSpace(workflowName) ||
            string.IsNullOrWhiteSpace(sourceNodeName) ||
            string.IsNullOrWhiteSpace(targetNodeName) ||
            string.IsNullOrWhiteSpace(targetPortName) ||
            string.Equals(sourceNodeName, targetNodeName, StringComparison.Ordinal))
        {
            return json;
        }

        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        if (flowApplication["workflows"] is not JsonObject workflows ||
            workflows[workflowName] is not JsonObject workflow ||
            workflow[sourceNodeName] is not JsonObject ||
            workflow[targetNodeName] is not JsonObject targetNode)
        {
            return json;
        }

        var reference = BuildPortReference(sourceNodeName, sourcePortName);
        if (replaceTargetPortLinks)
        {
            targetNode[targetPortName] = reference;
        }
        else
        {
            AppendLinkReference(targetNode, targetPortName, reference);
        }

        return root.ToJsonString(Options);
    }

    public string RemoveWorkflowPortLink(
        string json,
        string? workflowName,
        string sourceNodeName,
        string sourcePortName,
        string targetNodeName,
        string targetPortName)
    {
        if (string.IsNullOrWhiteSpace(workflowName) ||
            string.IsNullOrWhiteSpace(sourceNodeName) ||
            string.IsNullOrWhiteSpace(targetNodeName) ||
            string.IsNullOrWhiteSpace(targetPortName))
        {
            return json;
        }

        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        if (flowApplication["workflows"] is not JsonObject workflows ||
            workflows[workflowName] is not JsonObject workflow ||
            workflow[targetNodeName] is not JsonObject targetNode ||
            !RemoveLinkReference(targetNode, targetPortName, sourceNodeName, sourcePortName))
        {
            return json;
        }

        return root.ToJsonString(Options);
    }

    public string RemoveWorkflowNode(string json, string? workflowName, string nodeName)
    {
        if (string.IsNullOrWhiteSpace(workflowName) || string.IsNullOrWhiteSpace(nodeName))
        {
            return json;
        }

        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        if (flowApplication["workflows"] is not JsonObject workflows ||
            workflows[workflowName] is not JsonObject workflow ||
            !workflow.Remove(nodeName))
        {
            return json;
        }

        foreach (var (_, node) in workflow.AsEnumerable().ToList())
        {
            if (node is not JsonObject nodeObject)
            {
                continue;
            }

            foreach (var (portName, portValue) in nodeObject.AsEnumerable().ToList())
            {
                if (IsDefinitionProperty(portName))
                {
                    continue;
                }

                if (portValue is null)
                {
                    continue;
                }

                var updated = RemoveReferencesFromSourceNode(portValue, nodeName, out var removed);
                if (!removed)
                {
                    continue;
                }

                if (updated is null)
                {
                    nodeObject.Remove(portName);
                }
                else
                {
                    nodeObject[portName] = updated;
                }
            }
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

        var preferredNodeName = componentType switch
        {
            "mqtt.payload-inspector" => InspectorNodeName,
            "mqtt.metrics" => MetricsNodeName,
            "mqtt.message-filter" => FilterNodeName,
            "mqtt.condition-router" => RouterNodeName,
            "json.schema-validator" => "jsonSchemaValidator",
            "flow.mapper" => MakeUniqueNodeName(workflow, MapperNodeName),
            "flow.logger" => MakeUniqueNodeName(workflow, LoggerNodeName),
            "mqtt.recording-request" => RecorderNodeName,
            "mqtt.recorder" => RecorderNodeName,
            "mqtt.publish-request" => PublisherNodeName,
            "mqtt.publisher" => PublisherNodeName,
            "file.write-request" => "fileWriteRequest",
            "file.writer" => "fileWriter",
            "mqtt.connection-state-trigger" => StateSourceNodeName,
            "replay.source" => ReplayNodeName,
            "generated.source" => GeneratedNodeName,
            "session.source" => StoredSourceNodeName,
            _ => MakeNodeName(componentType)
        };
        var nodeName = MakeUniqueNodeName(workflow, preferredNodeName);

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
        else if (componentType == "mqtt.condition-router")
        {
            node["configuration"] = CreateConditionRouterConfiguration();
        }
        else if (componentType == "mqtt.publisher")
        {
            node["configuration"] = CreateMqttPublisherConfiguration(FindFirstConnectionResourceName(flowApplication));
        }
        else if (componentType == "generated.source")
        {
            node["configuration"] = CreateGeneratedSourceConfiguration();
        }
        else if (componentType == "replay.source")
        {
            node["configuration"] = CreateReplaySourceConfiguration();
        }
        else if (componentType == "flow.logger")
        {
            node["configuration"] = CreateLoggerConfiguration();
        }
        else if (componentType is "mqtt.recorder" or "file.writer")
        {
            node["configuration"] = CreateActorCapacityConfiguration();
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
        JsonObject nodeConfiguration,
        string? workflowName = null)
    {
        var withResource = UpsertConnectionResource(json, resourceName, profile);
        return UpdateNodeConfiguration(withResource, nodeName, nodeConfiguration, workflowName);
    }

    private static bool TryUpdateWorkflowNodeConfiguration(
        JsonObject flowApplication,
        string workflowName,
        string nodeName,
        JsonObject configuration)
    {
        if (flowApplication["workflows"] is not JsonObject workflows ||
            workflows[workflowName] is not JsonObject workflow)
        {
            return false;
        }

        return UpdateWorkflowNodeConfiguration(workflow, nodeName, configuration);
    }

    private static bool UpdateWorkflowNodeConfiguration(JsonObject workflow, string nodeName, JsonObject configuration)
    {
        if (workflow[nodeName] is not JsonObject workflowNode)
        {
            return false;
        }

        workflowNode["configuration"] = configuration;
        return true;
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

    private static bool IsDefinitionProperty(string name)
        => string.Equals(name, "type", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(name, "configuration", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(name, "when", StringComparison.OrdinalIgnoreCase);

    private static string BuildPortReference(string sourceNodeName, string sourcePortName)
    {
        var sourcePort = string.IsNullOrWhiteSpace(sourcePortName) ? "Output" : sourcePortName.Trim();
        return $"{sourceNodeName.Trim()}.{sourcePort}";
    }

    private static void AppendLinkReference(JsonObject targetNode, string targetPortName, string reference)
    {
        if (targetNode[targetPortName] is not { } existing)
        {
            targetNode[targetPortName] = reference;
            return;
        }

        if (ContainsLinkReference(existing, reference))
        {
            return;
        }

        if (existing is JsonArray existingArray)
        {
            existingArray.Add(JsonValue.Create(reference));
            return;
        }

        targetNode[targetPortName] = new JsonArray(existing.DeepClone(), JsonValue.Create(reference));
    }

    private static bool RemoveLinkReference(
        JsonObject targetNode,
        string targetPortName,
        string sourceNodeName,
        string sourcePortName)
    {
        if (targetNode[targetPortName] is not { } existing)
        {
            return false;
        }

        var updated = RemoveLinkReference(existing, sourceNodeName, sourcePortName, out var removed);
        if (!removed)
        {
            return false;
        }

        if (updated is null)
        {
            targetNode.Remove(targetPortName);
        }
        else
        {
            targetNode[targetPortName] = updated;
        }

        return true;
    }

    private static JsonNode? RemoveLinkReference(
        JsonNode node,
        string sourceNodeName,
        string sourcePortName,
        out bool removed)
    {
        removed = false;

        if (node is JsonValue value &&
            value.TryGetValue<string>(out var reference) &&
            ReferenceMatches(reference, sourceNodeName, sourcePortName))
        {
            removed = true;
            return null;
        }

        if (node is JsonArray array)
        {
            var updatedArray = new JsonArray();
            foreach (var item in array)
            {
                if (item is null)
                {
                    updatedArray.Add(null);
                    continue;
                }

                var updatedItem = RemoveLinkReference(item, sourceNodeName, sourcePortName, out var itemRemoved);
                removed |= itemRemoved;
                if (updatedItem is not null)
                {
                    updatedArray.Add(updatedItem);
                }
                else if (!itemRemoved)
                {
                    updatedArray.Add(item.DeepClone());
                }
            }

            return removed
                ? updatedArray.Count == 0 ? null : updatedArray
                : node.DeepClone();
        }

        if (node is JsonObject obj &&
            (obj.TryGetPropertyValue("from", out var fromNode) ||
             obj.TryGetPropertyValue("From", out fromNode)) &&
            fromNode is not null)
        {
            var updatedFrom = RemoveLinkReference(fromNode, sourceNodeName, sourcePortName, out removed);
            if (!removed)
            {
                return node.DeepClone();
            }

            if (updatedFrom is null)
            {
                return null;
            }

            var updatedObject = (JsonObject)node.DeepClone();
            updatedObject["from"] = updatedFrom;
            updatedObject.Remove("From");
            return updatedObject;
        }

        return node.DeepClone();
    }

    private static JsonNode? RemoveReferencesFromSourceNode(
        JsonNode node,
        string sourceNodeName,
        out bool removed)
    {
        removed = false;

        if (node is JsonValue value &&
            value.TryGetValue<string>(out var reference) &&
            ReferenceNodeMatches(reference, sourceNodeName))
        {
            removed = true;
            return null;
        }

        if (node is JsonArray array)
        {
            var updatedArray = new JsonArray();
            foreach (var item in array)
            {
                if (item is null)
                {
                    updatedArray.Add(null);
                    continue;
                }

                var updatedItem = RemoveReferencesFromSourceNode(item, sourceNodeName, out var itemRemoved);
                removed |= itemRemoved;
                if (updatedItem is not null)
                {
                    updatedArray.Add(updatedItem);
                }
                else if (!itemRemoved)
                {
                    updatedArray.Add(item.DeepClone());
                }
            }

            return removed
                ? updatedArray.Count == 0 ? null : updatedArray
                : node.DeepClone();
        }

        if (node is JsonObject obj &&
            (obj.TryGetPropertyValue("from", out var fromNode) ||
             obj.TryGetPropertyValue("From", out fromNode)) &&
            fromNode is not null)
        {
            var updatedFrom = RemoveReferencesFromSourceNode(fromNode, sourceNodeName, out removed);
            if (!removed)
            {
                return node.DeepClone();
            }

            if (updatedFrom is null)
            {
                return null;
            }

            var updatedObject = (JsonObject)node.DeepClone();
            updatedObject["from"] = updatedFrom;
            updatedObject.Remove("From");
            return updatedObject;
        }

        return node.DeepClone();
    }

    private static bool ContainsLinkReference(JsonNode node, string reference)
    {
        if (node is JsonValue value &&
            value.TryGetValue<string>(out var existingReference) &&
            string.Equals(existingReference, reference, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (node is JsonArray array)
        {
            return array.Any(item => item is not null && ContainsLinkReference(item, reference));
        }

        if (node is JsonObject obj &&
            (obj.TryGetPropertyValue("from", out var fromNode) ||
             obj.TryGetPropertyValue("From", out fromNode)) &&
            fromNode is not null)
        {
            return ContainsLinkReference(fromNode, reference);
        }

        return false;
    }

    private static bool ReferenceMatches(string reference, string sourceNodeName, string sourcePortName)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return false;
        }

        var parts = reference.Trim().Split('.', 2, StringSplitOptions.TrimEntries);
        var referenceNode = parts[0];
        var referencePort = parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1])
            ? parts[1]
            : "Output";
        var sourcePort = string.IsNullOrWhiteSpace(sourcePortName) ? "Output" : sourcePortName.Trim();

        return string.Equals(referenceNode, sourceNodeName.Trim(), StringComparison.Ordinal) &&
               string.Equals(referencePort, sourcePort, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ReferenceNodeMatches(string reference, string sourceNodeName)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return false;
        }

        var referenceNode = reference.Trim().Split('.', 2, StringSplitOptions.TrimEntries)[0];
        return string.Equals(referenceNode, sourceNodeName.Trim(), StringComparison.Ordinal);
    }

    private static bool NeedsInputLink(string componentType) => componentType switch
    {
        "mqtt.trigger" or "session.source" or "replay.source" or "generated.source" or "mqtt.connection-state-trigger" => false,
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
        foreach (var preferredName in new[] { TriggerNodeName, StoredSourceNodeName, ReplayNodeName, GeneratedNodeName })
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

    private static JsonObject CreateConditionRouterConfiguration()
        => new()
        {
            ["expression"] = "qos >= 1",
            ["boundedCapacity"] = 1000
        };

    private static JsonObject CreateMqttPublisherConfiguration(string? connectionName = null)
        => new()
        {
            ["connection"] = string.IsNullOrWhiteSpace(connectionName) ? BrokerResourceName : connectionName,
            ["boundedCapacity"] = 1000
        };

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

    private static JsonObject CreateGeneratedSourceConfiguration()
        => new()
        {
            ["messages"] = SourceNodeConfiguration.BuildGeneratedMessages(
            [
                new GeneratedMessageDraft
                {
                    Topic = "factory/sample",
                    Payload = """{"value":21.7,"unit":"c","status":"ok"}"""
                }
            ]),
            ["boundedCapacity"] = 1000
        };

    private static JsonObject CreateReplaySourceConfiguration()
        => new()
        {
            ["sessionId"] = string.Empty,
            ["speed"] = 1,
            ["boundedCapacity"] = 1000
        };

    private static JsonObject CreateLoggerConfiguration()
        => new()
        {
            ["boundedCapacity"] = 1000,
            ["maxEntries"] = 500,
            ["includePayloadPreview"] = true,
            ["maxPayloadPreviewChars"] = 512
        };

    private static JsonObject CreateActorCapacityConfiguration()
        => new()
        {
            ["boundedCapacity"] = 1000
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
