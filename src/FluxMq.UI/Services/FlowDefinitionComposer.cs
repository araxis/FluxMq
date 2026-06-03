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

    /// <summary>Returns the ordered list of dashboard names in the definition.</summary>
    public IReadOnlyList<string> GetDashboardNames(string json)
        => GetNamedObjectKeys(json, "dashboards");

    /// <summary>Returns the ordered list of test scenario names in the definition.</summary>
    public IReadOnlyList<string> GetTestNames(string json)
        => GetNamedObjectKeys(json, "tests");

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


}
