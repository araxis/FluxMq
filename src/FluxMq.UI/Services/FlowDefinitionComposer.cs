using FluxMq.App.Definitions;
using FluxMq.UI.Components.Workspace.Nodes.Routing;
using FluxMq.UI.Components.Workspace.Nodes.Timers;
using FluxMq.UI.Models;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FluxMq.UI.Services;

/// <summary>
/// Builds JSON flow definitions for the desktop workspace. The default shape uses
/// explicit live, stored-session, replay, and generated source nodes.
/// </summary>
public sealed partial class FlowDefinitionComposer
{
    public const string BrokerResourceName = "broker";
    public const string TriggerNodeName = "trigger";
    public const string StoredSourceNodeName = "stored";
    public const string InspectorNodeName = "inspect";
    public const string PayloadInspectNodeName = "payloadInspect";
    public const string MetricsNodeName = "metrics";
    public const string FilterNodeName = "filter";
    public const string RouterNodeName = "router";
    public const string SwitchNodeName = "switch";
    public const string CorrelationNodeName = "correlation";
    public const string WindowNodeName = "window";
    public const string JoinNodeName = "join";
    public const string ForkNodeName = "fork";
    public const string MergeNodeName = "merge";
    public const string AssertionNodeName = "assertion";
    public const string MapperNodeName = "mapper";
    public const string StateReducerNodeName = "stateReducer";
    public const string LoggerNodeName = "logger";
    public const string RecorderNodeName = "recorder";
    public const string PublisherNodeName = "publisher";
    public const string HttpRequestNodeName = "http";
    public const string StateSourceNodeName = "state";
    public const string ReplayNodeName = "replay";
    public const string GeneratedNodeName = "generated";
    public const string TimerIntervalNodeName = "timer";
    public const string TimerScheduleNodeName = "schedule";
    public const string TimerDelayNodeName = "delay";
    public const string TimerDebounceNodeName = "debounce";
    public const string TimerThrottleNodeName = "throttle";
    public const string DefaultWorkflowName = "inspectPayloads";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public string CreateEmptyDefinition() => CreateRoot().ToJsonString(Options);

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

    /// <summary>Returns (name, type) pairs for all nodes in a specific workflow.</summary>
    public IReadOnlyList<(string Name, string Type)> GetWorkflowNodes(string json, string workflowName)
    {
        var result = new List<(string, string)>();
        using var doc = ParseDefinitionJson(json, "Read workflow nodes");
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

        return result;
    }

    /// <summary>Returns the ordered list of workflow names in the definition.</summary>
    public IReadOnlyList<string> GetWorkflowNames(string json)
        => GetNamedObjectKeys(json, "workflows");

    /// <summary>Returns the ordered list of dashboard names in the definition.</summary>
    public IReadOnlyList<string> GetDashboardNames(string json)
        => GetNamedObjectKeys(json, "dashboards");

    /// <summary>Returns the ordered list of test scenario names in the definition.</summary>
    public IReadOnlyList<string> GetTestNames(string json)
        => GetNamedObjectKeys(json, "tests");

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
        {
            workflows.Remove(name);
        }

        return root.ToJsonString(Options);
    }

    /// <summary>Adds a component and, when appropriate, wires it to the current explicit source node.</summary>
    public string AddComponent(string json, string componentType, string? targetWorkflowName = null)
    {
        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        var workflows = GetOrCreateObject(flowApplication, "workflows");
        var workflow = GetOrCreateObject(workflows, targetWorkflowName ?? DefaultWorkflowName);

        var componentMetadata = FlowComponentMetadataRegistry.Find(componentType);
        var preferredNodeName = componentMetadata is { } metadata
            ? metadata.MakePreferredNodeNameUnique
                ? MakeUniqueNodeName(workflow, metadata.PreferredNodeName)
                : metadata.PreferredNodeName
            : MakeNodeName(componentType);
        var nodeName = MakeUniqueNodeName(workflow, preferredNodeName);

        var node = new JsonObject
        {
            ["type"] = componentType
        };

        var configurationContext = new FlowComponentDefaultConfigurationContext(
            FindDefaultMapperInputType(workflow),
            FindFirstConnectionResourceName(flowApplication));
        if (FlowComponentMetadataRegistry.CreateDefaultConfiguration(componentType, configurationContext) is { } configuration)
        {
            node["configuration"] = configuration;
        }

        if (FindDefaultInputLink(componentType, workflow) is { Length: > 0 } inputLink)
        {
            node["Input"] = inputLink;
        }

        workflow[nodeName] = node;

        return root.ToJsonString(Options);
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

    private static bool IsDefinitionProperty(string name)
        => string.Equals(name, "type", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(name, "configuration", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(name, "when", StringComparison.OrdinalIgnoreCase);

    private static bool NeedsInputLink(string componentType)
        => GetDefaultInputLink(componentType) != FlowComponentDefaultInputLink.None;

    private static string? FindDefaultInputLink(string componentType, JsonObject workflow)
    {
        return GetDefaultInputLink(componentType) switch
        {
            FlowComponentDefaultInputLink.None => null,
            FlowComponentDefaultInputLink.PreferredMapper =>
                FindPreferredMapperNode(workflow) is { Length: > 0 } mapper
                ? $"{mapper}.Output"
                : null,
            FlowComponentDefaultInputLink.StateReducerMapper =>
                FindPreferredMapperNode(workflow, "StateReducerInput") is { Length: > 0 } mapper
                ? $"{mapper}.Output"
                : null,
            _ => FindPreferredSourceNode(workflow) is { Length: > 0 } source
                ? $"{source}.Output"
                : null
        };
    }

    private static FlowComponentDefaultInputLink GetDefaultInputLink(string componentType)
        => FlowComponentMetadataRegistry.Find(componentType)?.DefaultInputLink
           ?? FlowComponentDefaultInputLink.PreferredSource;

    private static string? FindPreferredSourceNode(JsonObject workflow)
    {
        foreach (var preferredName in new[] { TriggerNodeName, StoredSourceNodeName, ReplayNodeName, GeneratedNodeName, TimerIntervalNodeName, TimerScheduleNodeName })
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
        => FindPreferredMapperNode(workflow, outputType: null);

    private static string? FindPreferredMapperNode(JsonObject workflow, string? outputType)
    {
        foreach (var node in workflow)
        {
            if (node.Value is JsonObject nodeObject &&
                nodeObject["type"]?.GetValue<string?>() is "flow.mapper" &&
                (outputType is null || MapperOutputTypeMatches(nodeObject, outputType)))
            {
                return node.Key;
            }
        }

        return null;
    }

    private static bool MapperOutputTypeMatches(JsonObject mapperNode, string outputType)
    {
        if (mapperNode["configuration"] is not JsonObject configuration)
        {
            return false;
        }

        return string.Equals(
            configuration["outputType"]?.GetValue<string?>(),
            outputType,
            StringComparison.Ordinal);
    }

    private static string FindDefaultMapperInputType(JsonObject workflow)
    {
        if (FindPreferredSourceNode(workflow) is not { Length: > 0 } source ||
            workflow[source] is not JsonObject sourceNode ||
            sourceNode["type"]?.GetValue<string?>() is not { } sourceType)
        {
            return "MqttEnvelope";
        }

        return sourceType switch
        {
            TimerNodeTypes.Interval => "TimerTick",
            TimerNodeTypes.Schedule => "ScheduleTick",
            _ => "MqttEnvelope"
        };
    }

    private static JsonObject CreateDefaultComponentConfiguration(string componentType)
        => FlowComponentMetadataRegistry.CreateDefaultConfiguration(componentType, FlowComponentDefaultConfigurationContext.Empty)
           ?? new JsonObject();

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

    private static IReadOnlyList<string> GetNamedObjectKeys(string json, string propertyName)
    {
        using var doc = ParseDefinitionJson(json, $"Read {propertyName} names");
        var root = doc.RootElement;
        var flowApp = TryGetFlowApplication(root, out var app) ? app : root;

        if (flowApp.TryGetProperty(propertyName, out var artifacts) &&
            artifacts.ValueKind == JsonValueKind.Object)
        {
            return artifacts.EnumerateObject().Select(artifact => artifact.Name).ToArray();
        }

        return [];
    }

    private static JsonDocument ParseDefinitionJson(string json, string operation)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"{operation} failed because the flow definition JSON is invalid: {exception.Message}", exception);
        }
    }

    private static bool TryGetFlowApplication(JsonElement root, out JsonElement flowApplication)
    {
        if (root.TryGetProperty("FluxMq", out var fluxMq) &&
            fluxMq.TryGetProperty("FlowApplication", out flowApplication) &&
            flowApplication.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        flowApplication = default;
        return false;
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
