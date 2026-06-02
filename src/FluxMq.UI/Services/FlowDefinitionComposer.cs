using FluxMq.Core.Models;
using FluxMq.App.Definitions;
using FluxMq.Scenarios;
using FluxMq.UI.Components.Workspace.Nodes.FlowAssertion;
using FluxMq.UI.Components.Workspace.Nodes.DynamicMapper;
using FluxMq.UI.Components.Workspace.Nodes.MetricNode;
using FluxMq.UI.Components.Workspace.Nodes.MqttTrigger;
using FluxMq.UI.Components.Workspace.Nodes.Routing;
using FluxMq.UI.Components.Workspace.Nodes.Sources;
using FluxMq.UI.Components.Workspace.Nodes.StateReducer;
using FluxMq.UI.Components.Workspace.Nodes.Timers;
using FluxMq.UI.Models;
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
    public const string PayloadInspectNodeName = "payloadInspect";
    public const string MetricsNodeName = "metrics";
    public const string FilterNodeName = "filter";
    public const string RouterNodeName = "router";
    public const string SwitchNodeName = "switch";
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
                    ["configuration"] = CreateMetricsConfiguration()
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

    public string UpdateWorkflowPortLinkCondition(
        string json,
        string? workflowName,
        string sourceNodeName,
        string sourcePortName,
        string targetNodeName,
        string targetPortName,
        string? condition)
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
            !UpdateLinkCondition(targetNode, targetPortName, sourceNodeName, sourcePortName, condition))
        {
            return json;
        }

        return root.ToJsonString(Options);
    }

    public string? GetWorkflowPortLinkCondition(
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
            return null;
        }

        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        if (flowApplication["workflows"] is not JsonObject workflows ||
            workflows[workflowName] is not JsonObject workflow ||
            workflow[targetNodeName] is not JsonObject targetNode ||
            targetNode[targetPortName] is not { } link)
        {
            return null;
        }

        return TryGetLinkCondition(link, sourceNodeName, sourcePortName, out var condition)
            ? condition
            : null;
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

    /// <summary>Adds an empty dashboard artifact with the given name if it does not already exist.</summary>
    public string AddDashboard(string json, string name)
    {
        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        var dashboards = GetOrCreateObject(flowApplication, "dashboards");
        if (!dashboards.ContainsKey(name))
        {
            dashboards[name] = CreateDashboard();
        }

        return root.ToJsonString(Options);
    }

    /// <summary>Adds an empty test scenario artifact with the given name if it does not already exist.</summary>
    public string AddTest(string json, string name)
    {
        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        var tests = GetOrCreateObject(flowApplication, "tests");
        if (!tests.ContainsKey(name))
        {
            tests[name] = new JsonObject
            {
                ["steps"] = new JsonObject()
            };
        }

        return root.ToJsonString(Options);
    }

    public string AddScenarioStep(string json, string testName, string stepType)
    {
        if (string.IsNullOrWhiteSpace(testName))
        {
            return json;
        }

        var normalizedType = string.IsNullOrWhiteSpace(stepType)
            ? ScenarioStepTypes.ExpectEvent
            : stepType.Trim();
        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        var tests = GetOrCreateObject(flowApplication, "tests");
        var scenario = GetOrCreateObject(tests, testName);
        var steps = GetOrCreateObject(scenario, "steps");
        var stepName = MakeUniqueScenarioStepName(steps, ScenarioStepNamePrefix(normalizedType));
        steps[stepName] = CreateScenarioStepObject(flowApplication, normalizedType);

        return root.ToJsonString(Options);
    }

    public string UpdateScenarioStep(
        string json,
        string testName,
        string stepName,
        string stepType,
        IReadOnlyDictionary<string, string> configuration)
    {
        if (string.IsNullOrWhiteSpace(testName) ||
            string.IsNullOrWhiteSpace(stepName))
        {
            return json;
        }

        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        if (flowApplication["tests"] is not JsonObject tests ||
            tests[testName] is not JsonObject scenario)
        {
            return json;
        }

        var steps = GetOrCreateObject(scenario, "steps");
        if (steps[stepName] is not JsonObject step)
        {
            return json;
        }

        var normalizedType = string.IsNullOrWhiteSpace(stepType)
            ? ReadString(step, "type") ?? ScenarioStepTypes.ExpectEvent
            : stepType.Trim();
        step["type"] = normalizedType;
        step["configuration"] = CreateScenarioStepConfiguration(normalizedType, configuration);
        return root.ToJsonString(Options);
    }

    public string RemoveScenarioStep(string json, string testName, string stepName)
    {
        if (string.IsNullOrWhiteSpace(testName) ||
            string.IsNullOrWhiteSpace(stepName))
        {
            return json;
        }

        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        if (flowApplication["tests"] is not JsonObject tests ||
            tests[testName] is not JsonObject scenario ||
            scenario["steps"] is not JsonObject steps ||
            !steps.Remove(stepName))
        {
            return json;
        }

        return root.ToJsonString(Options);
    }

    public string MoveScenarioStep(string json, string testName, string stepName, int offset)
    {
        if (string.IsNullOrWhiteSpace(testName) ||
            string.IsNullOrWhiteSpace(stepName) ||
            offset == 0)
        {
            return json;
        }

        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        if (flowApplication["tests"] is not JsonObject tests ||
            tests[testName] is not JsonObject scenario ||
            scenario["steps"] is not JsonObject steps)
        {
            return json;
        }

        var entries = steps
            .Select(step => (step.Key, Value: step.Value?.DeepClone()))
            .ToList();
        var currentIndex = entries.FindIndex(step => string.Equals(step.Key, stepName, StringComparison.Ordinal));
        if (currentIndex < 0)
        {
            return json;
        }

        var targetIndex = Math.Clamp(currentIndex + offset, 0, entries.Count - 1);
        if (targetIndex == currentIndex)
        {
            return json;
        }

        var moved = entries[currentIndex];
        entries.RemoveAt(currentIndex);
        entries.Insert(targetIndex, moved);

        var reordered = new JsonObject();
        foreach (var (key, value) in entries)
        {
            reordered[key] = value;
        }

        scenario["steps"] = reordered;
        return root.ToJsonString(Options);
    }

    public DashboardLayoutSnapshot? GetDashboardLayout(string json, string dashboardName)
    {
        if (string.IsNullOrWhiteSpace(dashboardName))
        {
            return null;
        }

        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        if (flowApplication["dashboards"] is not JsonObject dashboards ||
            dashboards[dashboardName] is not JsonObject dashboard)
        {
            return null;
        }

        var layout = dashboard["layout"] as JsonObject ?? new JsonObject();
        var cells = layout["cells"] as JsonObject ?? new JsonObject();
        var widgets = dashboard["widgets"] as JsonObject ?? new JsonObject();

        var columns = ReadTrackStrings(layout, "columns", ["*"]);
        var rows = ReadTrackStrings(layout, "rows", ["*"]);

        return new DashboardLayoutSnapshot(
            columns,
            rows,
            NormalizePaddingValues(ReadPaddingValues(layout, "columnPadding"), columns.Count),
            NormalizePaddingValues(ReadPaddingValues(layout, "rowPadding"), rows.Count),
            ReadDashboardCells(cells),
            ReadDashboardWidgets(widgets));
    }

    public TestScenarioSnapshot? GetTestScenario(string json, string testName)
    {
        if (string.IsNullOrWhiteSpace(testName))
        {
            return null;
        }

        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        if (flowApplication["tests"] is not JsonObject tests ||
            tests[testName] is not JsonObject scenario)
        {
            return null;
        }

        var steps = scenario["steps"] as JsonObject ?? new JsonObject();
        return new TestScenarioSnapshot(testName, ReadScenarioSteps(steps));
    }

    public string AddDashboardWidget(string json, string dashboardName, string widgetType, string? cellName = null)
    {
        if (string.IsNullOrWhiteSpace(dashboardName))
        {
            return json;
        }

        var normalizedType = string.IsNullOrWhiteSpace(widgetType)
            ? DashboardWidgetCatalog.EventCounterType
            : widgetType.Trim();
        var root = ParseOrCreate(json);
        var dashboard = GetOrCreateDashboardObject(GetFlowApplication(root), dashboardName);
        var layout = GetOrCreateObject(dashboard, "layout");
        var cells = GetOrCreateObject(layout, "cells");
        var widgets = GetOrCreateObject(dashboard, "widgets");

        var widgetName = MakeUniqueDashboardWidgetName(widgets, WidgetNamePrefix(normalizedType));
        widgets[widgetName] = CreateDashboardWidgetObject(normalizedType);
        AssignWidgetToDashboardCell(layout, cells, widgetName, cellName);

        return root.ToJsonString(Options);
    }

    public string UpdateDashboardWidgetConfiguration(
        string json,
        string dashboardName,
        string widgetName,
        IReadOnlyDictionary<string, string> configuration)
    {
        if (string.IsNullOrWhiteSpace(dashboardName) ||
            string.IsNullOrWhiteSpace(widgetName))
        {
            return json;
        }

        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        if (flowApplication["dashboards"] is not JsonObject dashboards ||
            dashboards[dashboardName] is not JsonObject dashboard ||
            dashboard["widgets"] is not JsonObject widgets ||
            widgets[widgetName] is not JsonObject widget)
        {
            return json;
        }

        widget["configuration"] = CreateConfigurationObject(configuration);
        return root.ToJsonString(Options);
    }

    public string RemoveDashboardWidget(string json, string dashboardName, string widgetName)
    {
        if (string.IsNullOrWhiteSpace(dashboardName) ||
            string.IsNullOrWhiteSpace(widgetName))
        {
            return json;
        }

        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        if (flowApplication["dashboards"] is not JsonObject dashboards ||
            dashboards[dashboardName] is not JsonObject dashboard)
        {
            return json;
        }

        var changed = false;
        if (dashboard["widgets"] is JsonObject widgets)
        {
            changed = widgets.Remove(widgetName);
        }

        if (dashboard["layout"] is JsonObject layout &&
            layout["cells"] is JsonObject cells)
        {
            foreach (var (_, cell) in cells.ToArray())
            {
                if (cell is JsonObject cellObject &&
                    string.Equals(ReadString(cellObject, "widget"), widgetName, StringComparison.Ordinal))
                {
                    cellObject.Remove("widget");
                    changed = true;
                }
            }
        }

        return changed ? root.ToJsonString(Options) : json;
    }

    public string UpdateDashboardTrack(
        string json,
        string dashboardName,
        string axis,
        int index,
        string size,
        double padding)
    {
        if (string.IsNullOrWhiteSpace(dashboardName) || index < 0)
        {
            return json;
        }

        if (padding < 0 || double.IsNaN(padding) || double.IsInfinity(padding))
        {
            throw new FormatException("Dashboard track padding must be a non-negative finite size.");
        }

        var normalizedSize = NormalizeTrackString(size, axis);
        var isRow = string.Equals(axis, "row", StringComparison.OrdinalIgnoreCase);
        var trackProperty = isRow ? "rows" : "columns";
        var paddingProperty = isRow ? "rowPadding" : "columnPadding";

        var root = ParseOrCreate(json);
        var dashboard = GetOrCreateDashboardObject(GetFlowApplication(root), dashboardName);
        var layout = GetOrCreateObject(dashboard, "layout");
        var tracks = ReadTrackStrings(layout, trackProperty, ["*"]).ToList();
        if (index >= tracks.Count)
        {
            return json;
        }

        tracks[index] = normalizedSize;
        layout[trackProperty] = CreateTrackArray(tracks);

        var paddingValues = NormalizePaddingValues(ReadPaddingValues(layout, paddingProperty), tracks.Count).ToList();
        paddingValues[index] = padding;
        layout[paddingProperty] = CreateNumberArray(paddingValues);
        GetOrCreateObject(layout, "cells");
        GetOrCreateObject(dashboard, "widgets");

        return root.ToJsonString(Options);
    }

    public string UpdateDashboardGridTracks(
        string json,
        string dashboardName,
        IEnumerable<string> columns,
        IEnumerable<string> rows)
    {
        if (string.IsNullOrWhiteSpace(dashboardName))
        {
            return json;
        }

        var normalizedColumns = NormalizeTrackStrings(columns, "column");
        var normalizedRows = NormalizeTrackStrings(rows, "row");

        var root = ParseOrCreate(json);
        var dashboard = GetOrCreateDashboardObject(GetFlowApplication(root), dashboardName);
        var layout = GetOrCreateObject(dashboard, "layout");
        layout["columns"] = CreateTrackArray(normalizedColumns);
        layout["rows"] = CreateTrackArray(normalizedRows);
        layout["columnPadding"] = CreateNumberArray(NormalizePaddingValues(ReadPaddingValues(layout, "columnPadding"), normalizedColumns.Count));
        layout["rowPadding"] = CreateNumberArray(NormalizePaddingValues(ReadPaddingValues(layout, "rowPadding"), normalizedRows.Count));
        GetOrCreateObject(layout, "cells");
        GetOrCreateObject(dashboard, "widgets");

        return root.ToJsonString(Options);
    }

    public string ResizeDashboardGrid(string json, string dashboardName, int rowCount, int columnCount)
    {
        if (string.IsNullOrWhiteSpace(dashboardName))
        {
            return json;
        }

        rowCount = Math.Clamp(rowCount, 1, 12);
        columnCount = Math.Clamp(columnCount, 1, 12);

        var root = ParseOrCreate(json);
        var dashboard = GetOrCreateDashboardObject(GetFlowApplication(root), dashboardName);
        var layout = GetOrCreateObject(dashboard, "layout");
        var columns = ResizeTrackStrings(ReadTrackStrings(layout, "columns", ["*"]), columnCount);
        var rows = ResizeTrackStrings(ReadTrackStrings(layout, "rows", ["*"]), rowCount);
        layout["columns"] = CreateTrackArray(columns);
        layout["rows"] = CreateTrackArray(rows);
        layout["columnPadding"] = CreateNumberArray(NormalizePaddingValues(ReadPaddingValues(layout, "columnPadding"), columnCount));
        layout["rowPadding"] = CreateNumberArray(NormalizePaddingValues(ReadPaddingValues(layout, "rowPadding"), rowCount));

        var cells = GetOrCreateObject(layout, "cells");
        RemoveCellsOutsideGrid(cells, rowCount, columnCount);
        GetOrCreateObject(dashboard, "widgets");

        return root.ToJsonString(Options);
    }

    public string AddDashboardRow(string json, string dashboardName)
    {
        var layout = GetDashboardLayout(json, dashboardName);
        return layout is null
            ? json
            : ResizeDashboardGrid(json, dashboardName, layout.Rows.Count + 1, layout.Columns.Count);
    }

    public string RemoveDashboardRow(string json, string dashboardName)
    {
        var layout = GetDashboardLayout(json, dashboardName);
        return layout is null
            ? json
            : ResizeDashboardGrid(json, dashboardName, Math.Max(1, layout.Rows.Count - 1), layout.Columns.Count);
    }

    public string AddDashboardColumn(string json, string dashboardName)
    {
        var layout = GetDashboardLayout(json, dashboardName);
        return layout is null
            ? json
            : ResizeDashboardGrid(json, dashboardName, layout.Rows.Count, layout.Columns.Count + 1);
    }

    public string RemoveDashboardColumn(string json, string dashboardName)
    {
        var layout = GetDashboardLayout(json, dashboardName);
        return layout is null
            ? json
            : ResizeDashboardGrid(json, dashboardName, layout.Rows.Count, Math.Max(1, layout.Columns.Count - 1));
    }

    public string AddDashboardCell(string json, string dashboardName)
    {
        if (string.IsNullOrWhiteSpace(dashboardName))
        {
            return json;
        }

        var root = ParseOrCreate(json);
        var dashboard = GetOrCreateDashboardObject(GetFlowApplication(root), dashboardName);
        var layout = GetOrCreateObject(dashboard, "layout");
        var columns = ReadTrackStrings(layout, "columns", ["*"]).ToList();
        var rows = ReadTrackStrings(layout, "rows", ["*"]).ToList();

        if (columns.Count == 0)
        {
            columns.Add("*");
        }

        if (rows.Count == 0)
        {
            rows.Add("*");
        }

        layout["columns"] = CreateTrackArray(columns);
        layout["rows"] = CreateTrackArray(rows);
        layout["columnPadding"] = CreateNumberArray(NormalizePaddingValues(ReadPaddingValues(layout, "columnPadding"), columns.Count));
        layout["rowPadding"] = CreateNumberArray(NormalizePaddingValues(ReadPaddingValues(layout, "rowPadding"), rows.Count));

        var cells = GetOrCreateObject(layout, "cells");
        var existingCells = ReadDashboardCells(cells);
        var position = FindFirstOpenDashboardCell(columns.Count, rows.Count, existingCells);
        if (position is null)
        {
            rows.Add("*");
            layout["rows"] = CreateTrackArray(rows);
            layout["rowPadding"] = CreateNumberArray(NormalizePaddingValues(ReadPaddingValues(layout, "rowPadding"), rows.Count));
            position = (rows.Count - 1, 0);
        }

        var cellName = MakeUniqueDashboardCellName(cells, "cell");
        cells[cellName] = new JsonObject
        {
            ["row"] = position.Value.Row,
            ["column"] = position.Value.Column,
            ["rowSpan"] = 1,
            ["columnSpan"] = 1
        };
        GetOrCreateObject(dashboard, "widgets");

        return root.ToJsonString(Options);
    }

    public string MergeDashboardCells(
        string json,
        string dashboardName,
        IEnumerable<DashboardCellSnapshot> selectedCells)
    {
        var selection = selectedCells.ToArray();
        if (string.IsNullOrWhiteSpace(dashboardName) || !TryGetSelectionBounds(selection, out var bounds))
        {
            return json;
        }

        var root = ParseOrCreate(json);
        var dashboard = GetOrCreateDashboardObject(GetFlowApplication(root), dashboardName);
        var layout = GetOrCreateObject(dashboard, "layout");
        var cells = GetOrCreateObject(layout, "cells");
        var selectedNames = selection
            .Where(static cell => cell.IsExplicit)
            .Select(static cell => cell.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var selectedName in selectedNames)
        {
            cells.Remove(selectedName);
        }

        var cellName = selectedNames.Count == 1
            ? selectedNames.Single()
            : MakeUniqueDashboardCellName(cells, "cell");
        var widgets = selection
            .Where(static cell => cell.IsExplicit && !string.IsNullOrWhiteSpace(cell.Widget))
            .Select(static cell => cell.Widget)
            .Distinct(StringComparer.Ordinal)
            .Take(2)
            .ToArray();
        var widget = widgets.Length == 1 ? widgets[0] : null;

        cells[cellName] = new JsonObject
        {
            ["row"] = bounds.MinRow,
            ["column"] = bounds.MinColumn,
            ["rowSpan"] = bounds.MaxRow - bounds.MinRow + 1,
            ["columnSpan"] = bounds.MaxColumn - bounds.MinColumn + 1
        };

        if (!string.IsNullOrWhiteSpace(widget))
        {
            ((JsonObject)cells[cellName]!)["widget"] = widget;
        }

        return root.ToJsonString(Options);
    }

    public string SplitDashboardCell(string json, string dashboardName, string cellName)
    {
        if (string.IsNullOrWhiteSpace(dashboardName) || string.IsNullOrWhiteSpace(cellName))
        {
            return json;
        }

        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        if (flowApplication["dashboards"] is not JsonObject dashboards ||
            dashboards[dashboardName] is not JsonObject dashboard ||
            dashboard["layout"] is not JsonObject layout ||
            layout["cells"] is not JsonObject cells ||
            cells[cellName] is not JsonObject cellObject)
        {
            return json;
        }

        var cell = new DashboardCellSnapshot(
            cellName,
            ReadInt(cellObject, "row"),
            ReadInt(cellObject, "column"),
            Math.Max(1, ReadInt(cellObject, "rowSpan", 1)),
            Math.Max(1, ReadInt(cellObject, "columnSpan", 1)),
            ReadString(cellObject, "widget"));

        if (!cell.IsMerged)
        {
            return json;
        }

        cells.Remove(cellName);
        foreach (var coordinate in cell.CoveredCoordinates())
        {
            var name = MakeUniqueDashboardCellName(cells, "cell");
            cells[name] = new JsonObject
            {
                ["row"] = coordinate.Row,
                ["column"] = coordinate.Column,
                ["rowSpan"] = 1,
                ["columnSpan"] = 1
            };
        }

        return root.ToJsonString(Options);
    }

    public string SubdivideDashboardCell(
        string json,
        string dashboardName,
        DashboardCellSnapshot selectedCell,
        int rowParts,
        int columnParts)
    {
        rowParts = Math.Clamp(rowParts, 1, 6);
        columnParts = Math.Clamp(columnParts, 1, 6);
        if (string.IsNullOrWhiteSpace(dashboardName) || rowParts == 1 && columnParts == 1)
        {
            return json;
        }

        var root = ParseOrCreate(json);
        var dashboard = GetOrCreateDashboardObject(GetFlowApplication(root), dashboardName);
        var layout = GetOrCreateObject(dashboard, "layout");
        var columns = ReadTrackStrings(layout, "columns", ["*"]).ToArray();
        var rows = ReadTrackStrings(layout, "rows", ["*"]).ToArray();
        var columnPadding = NormalizePaddingValues(ReadPaddingValues(layout, "columnPadding"), columns.Length);
        var rowPadding = NormalizePaddingValues(ReadPaddingValues(layout, "rowPadding"), rows.Length);
        var rowInsertCount = Math.Max(0, rowParts - selectedCell.RowSpan);
        var columnInsertCount = Math.Max(0, columnParts - selectedCell.ColumnSpan);

        if (rows.Length + rowInsertCount > 12 || columns.Length + columnInsertCount > 12)
        {
            return json;
        }

        layout["rows"] = CreateTrackArray(SubdivideTrackStrings(rows, selectedCell.Row, selectedCell.RowSpan, rowParts));
        layout["columns"] = CreateTrackArray(SubdivideTrackStrings(columns, selectedCell.Column, selectedCell.ColumnSpan, columnParts));
        layout["rowPadding"] = CreateNumberArray(SubdividePaddingValues(rowPadding, selectedCell.Row, selectedCell.RowSpan, rowParts));
        layout["columnPadding"] = CreateNumberArray(SubdividePaddingValues(columnPadding, selectedCell.Column, selectedCell.ColumnSpan, columnParts));

        var cells = GetOrCreateObject(layout, "cells");
        var existingCells = ReadDashboardCells(cells)
            .Where(cell => !selectedCell.IsExplicit || !string.Equals(cell.Name, selectedCell.Name, StringComparison.Ordinal))
            .Select(cell => TransformDashboardCell(cell, selectedCell, rowInsertCount, columnInsertCount))
            .ToArray();

        var nextCells = new JsonObject();
        foreach (var cell in existingCells)
        {
            nextCells[cell.Name] = CreateDashboardCellObject(cell);
        }

        foreach (var child in CreateSubdivisionCells(selectedCell, rowParts, columnParts))
        {
            var name = MakeUniqueDashboardCellName(nextCells, "cell");
            nextCells[name] = CreateDashboardCellObject(child with { Name = name, IsExplicit = true });
        }

        layout["cells"] = nextCells;
        GetOrCreateObject(dashboard, "widgets");

        return root.ToJsonString(Options);
    }

    public string RemoveDashboardCell(string json, string dashboardName, string cellName)
    {
        if (string.IsNullOrWhiteSpace(dashboardName) || string.IsNullOrWhiteSpace(cellName))
        {
            return json;
        }

        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        if (flowApplication["dashboards"] is JsonObject dashboards &&
            dashboards[dashboardName] is JsonObject dashboard &&
            dashboard["layout"] is JsonObject layout &&
            layout["cells"] is JsonObject cells)
        {
            cells.Remove(cellName);
        }

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
            "payload.inspect" => PayloadInspectNodeName,
            "mqtt.metrics" => MetricsNodeName,
            "flow.filter" => FilterNodeName,
            "flow.when" => RouterNodeName,
            RoutingNodeTypes.Switch => SwitchNodeName,
            RoutingNodeTypes.Fork => ForkNodeName,
            RoutingNodeTypes.Merge => MergeNodeName,
            "flow.assert" => AssertionNodeName,
            "json.schema-validator" => "jsonSchemaValidator",
            "json.parse" => "jsonParser",
            "json.stringify" => "jsonStringifier",
            "text.encode" => "textEncoder",
            "text.decode" => "textDecoder",
            "base64.encode" => "base64Encoder",
            "base64.decode" => "base64Decoder",
            "flow.mapper" => MakeUniqueNodeName(workflow, MapperNodeName),
            "state.reducer" => StateReducerNodeName,
            "flow.logger" => MakeUniqueNodeName(workflow, LoggerNodeName),
            "mqtt.recorder" => RecorderNodeName,
            "mqtt.publisher" => PublisherNodeName,
            "http.request" => HttpRequestNodeName,
            "file.writer" => "fileWriter",
            "mqtt.connection-state-trigger" => StateSourceNodeName,
            "replay.source" => ReplayNodeName,
            "generated.source" => GeneratedNodeName,
            "session.source" => StoredSourceNodeName,
            TimerNodeTypes.Interval => TimerIntervalNodeName,
            TimerNodeTypes.Schedule => TimerScheduleNodeName,
            TimerNodeTypes.Delay => TimerDelayNodeName,
            TimerNodeTypes.Debounce => TimerDebounceNodeName,
            TimerNodeTypes.Throttle => TimerThrottleNodeName,
            _ => MakeNodeName(componentType)
        };
        var nodeName = MakeUniqueNodeName(workflow, preferredNodeName);

        var node = new JsonObject
        {
            ["type"] = componentType
        };

        if (componentType == "flow.mapper")
        {
            node["configuration"] = CreateDynamicMapperConfiguration(
                "MqttPublishRequest",
                FindDefaultMapperInputType(workflow));
        }
        else if (componentType == "json.schema-validator")
        {
            node["configuration"] = CreateJsonSchemaValidatorConfiguration();
        }
        else if (componentType == "flow.when")
        {
            node["configuration"] = CreateConditionRouterConfiguration();
        }
        else if (componentType == RoutingNodeTypes.Switch)
        {
            node["configuration"] = CreateRoutingSwitchConfiguration(FindDefaultMapperInputType(workflow));
        }
        else if (componentType == RoutingNodeTypes.Fork)
        {
            node["configuration"] = CreateRoutingForkConfiguration(FindDefaultMapperInputType(workflow));
        }
        else if (componentType == RoutingNodeTypes.Merge)
        {
            node["configuration"] = CreateRoutingMergeConfiguration();
        }
        else if (componentType == "flow.assert")
        {
            node["configuration"] = CreateAssertionConfiguration();
        }
        else if (componentType == "state.reducer")
        {
            node["configuration"] = CreateStateReducerConfiguration();
        }
        else if (componentType == "mqtt.publisher")
        {
            node["configuration"] = CreateMqttPublisherConfiguration(FindFirstConnectionResourceName(flowApplication));
        }
        else if (componentType == "mqtt.connection-state-trigger")
        {
            node["configuration"] = CreateConnectionReferenceConfiguration(FindFirstConnectionResourceName(flowApplication));
        }
        else if (componentType == "generated.source")
        {
            node["configuration"] = CreateGeneratedSourceConfiguration();
        }
        else if (componentType == "session.source")
        {
            node["configuration"] = CreateStoredSessionSourceConfiguration();
        }
        else if (componentType == "replay.source")
        {
            node["configuration"] = CreateReplaySourceConfiguration();
        }
        else if (componentType == "flow.logger")
        {
            node["configuration"] = CreateLoggerConfiguration();
        }
        else if (componentType == "mqtt.metrics")
        {
            node["configuration"] = CreateMetricsConfiguration();
        }
        else if (componentType == "http.request")
        {
            node["configuration"] = CreateHttpRequestConfiguration();
        }
        else if (componentType == "payload.inspect")
        {
            node["configuration"] = CreatePayloadInspectConfiguration();
        }
        else if (componentType is "mqtt.recorder" or "file.writer")
        {
            node["configuration"] = CreateActorCapacityConfiguration();
        }
        else if (componentType == TimerNodeTypes.Interval)
        {
            node["configuration"] = CreateTimerIntervalConfiguration();
        }
        else if (componentType == TimerNodeTypes.Schedule)
        {
            node["configuration"] = CreateTimerScheduleConfiguration();
        }
        else if (componentType == TimerNodeTypes.Delay)
        {
            node["configuration"] = CreateTimerDelayConfiguration(FindDefaultMapperInputType(workflow));
        }
        else if (componentType == TimerNodeTypes.Debounce)
        {
            node["configuration"] = CreateTimerDebounceConfiguration(FindDefaultMapperInputType(workflow));
        }
        else if (componentType == TimerNodeTypes.Throttle)
        {
            node["configuration"] = CreateTimerThrottleConfiguration(FindDefaultMapperInputType(workflow));
        }
        else if (IsSerializationTransform(componentType))
        {
            node["configuration"] = CreateTransformCapacityConfiguration();
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
        using var doc = ParseDefinitionJson(json, "Read node positions");
        var root = doc.RootElement;
        if (root.TryGetProperty("FluxMq", out var fluxMq) &&
            fluxMq.TryGetProperty("Designer", out var designer) &&
            designer.TryGetProperty("nodes", out var nodes) &&
            nodes.ValueKind == JsonValueKind.Object)
        {
            foreach (var node in nodes.EnumerateObject())
            {
                var x = node.Value.TryGetProperty("x", out var xp) ? xp.GetDouble() : 0;
                var y = node.Value.TryGetProperty("y", out var yp) ? yp.GetDouble() : 0;
                var collapsed = node.Value.TryGetProperty("collapsed", out var cp) && cp.GetBoolean();
                result[node.Name] = (x, y, collapsed);
            }
        }

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

    private static IReadOnlyList<string> NormalizeTrackStrings(IEnumerable<string> tracks, string axis)
    {
        var normalized = tracks
            .Select(track => string.IsNullOrWhiteSpace(track) ? string.Empty : NormalizeTrackString(track, axis))
            .Where(static track => !string.IsNullOrWhiteSpace(track))
            .ToArray();

        if (normalized.Length == 0)
        {
            throw new FormatException($"Dashboard {axis} tracks must include at least one size.");
        }

        return normalized;
    }

    private static string NormalizeTrackString(string value, string axis)
    {
        var track = DashboardGridTrackDefinition.Parse(value);
        if (track.Unit == DashboardGridTrackUnit.Percent && track.Value > 100)
        {
            throw new FormatException($"Dashboard {axis} percent track cannot exceed 100%.");
        }

        return track.ToSizeString();
    }

    private static IReadOnlyList<string> ReadTrackStrings(
        JsonObject layout,
        string propertyName,
        IReadOnlyList<string> fallback)
    {
        if (layout[propertyName] is not JsonArray tracks || tracks.Count == 0)
        {
            return fallback;
        }

        var result = new List<string>();
        foreach (var track in tracks)
        {
            try
            {
                var size = ReadTrackString(track);
                if (!string.IsNullOrWhiteSpace(size))
                {
                    result.Add(size);
                }
            }
            catch
            {
                if (track is JsonValue value &&
                    value.TryGetValue<string>(out var raw) &&
                    !string.IsNullOrWhiteSpace(raw))
                {
                    result.Add(raw.Trim());
                }
            }
        }

        return result.Count > 0 ? result : fallback;
    }

    private static string ReadTrackString(JsonNode? node)
    {
        if (node is null)
        {
            return "*";
        }

        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var text))
            {
                return text.Trim();
            }

            if (value.TryGetValue<double>(out var number))
            {
                return DashboardGridTrackDefinition.Fixed(number).ToSizeString();
            }
        }

        if (node is JsonObject obj)
        {
            if (obj["size"] is JsonValue sizeValue &&
                sizeValue.TryGetValue<string>(out var size) &&
                !string.IsNullOrWhiteSpace(size))
            {
                return size.Trim();
            }

            if (obj["unit"] is JsonValue unitValue &&
                unitValue.TryGetValue<string>(out var unit) &&
                obj["value"] is JsonValue numericValue &&
                numericValue.TryGetValue<double>(out var numeric))
            {
                return unit.Trim().ToLowerInvariant() switch
                {
                    "fixed" => DashboardGridTrackDefinition.Fixed(numeric).ToSizeString(),
                    "percent" => DashboardGridTrackDefinition.Percent(numeric).ToSizeString(),
                    "star" => DashboardGridTrackDefinition.Star(numeric).ToSizeString(),
                    _ => "*"
                };
            }
        }

        return "*";
    }

    private static IReadOnlyList<double> ReadPaddingValues(JsonObject layout, string propertyName)
    {
        if (layout[propertyName] is not JsonArray values)
        {
            return [];
        }

        var result = new List<double>();
        foreach (var item in values)
        {
            if (item is JsonValue value &&
                value.TryGetValue<double>(out var number) &&
                number >= 0 &&
                double.IsFinite(number))
            {
                result.Add(number);
            }
            else
            {
                result.Add(0);
            }
        }

        return result;
    }

    private static IReadOnlyList<double> NormalizePaddingValues(IReadOnlyList<double> values, int count)
    {
        var normalized = values
            .Select(static value => value >= 0 && double.IsFinite(value) ? value : 0)
            .Take(count)
            .ToList();

        while (normalized.Count < count)
        {
            normalized.Add(0);
        }

        return normalized;
    }

    private static IReadOnlyList<DashboardCellSnapshot> ReadDashboardCells(JsonObject cells)
    {
        var result = new List<DashboardCellSnapshot>();
        foreach (var cell in cells)
        {
            if (cell.Value is not JsonObject cellObject)
            {
                continue;
            }

            result.Add(new DashboardCellSnapshot(
                cell.Key,
                ReadInt(cellObject, "row"),
                ReadInt(cellObject, "column"),
                Math.Max(1, ReadInt(cellObject, "rowSpan", 1)),
                Math.Max(1, ReadInt(cellObject, "columnSpan", 1)),
                ReadString(cellObject, "widget")));
        }

        return result
            .OrderBy(static cell => cell.Row)
            .ThenBy(static cell => cell.Column)
            .ThenBy(static cell => cell.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyDictionary<string, DashboardWidgetSnapshot> ReadDashboardWidgets(JsonObject widgets)
    {
        var result = new Dictionary<string, DashboardWidgetSnapshot>(StringComparer.Ordinal);
        foreach (var widget in widgets)
        {
            if (widget.Value is not JsonObject widgetObject)
            {
                continue;
            }

            var configuration = widgetObject["configuration"] as JsonObject ?? new JsonObject();
            result[widget.Key] = new DashboardWidgetSnapshot(
                widget.Key,
                ReadString(widgetObject, "type") ?? string.Empty,
                ReadConfigurationStrings(configuration));
        }

        return result;
    }

    private static IReadOnlyList<ScenarioStepSnapshot> ReadScenarioSteps(JsonObject steps)
    {
        var result = new List<ScenarioStepSnapshot>();
        foreach (var step in steps)
        {
            if (step.Value is not JsonObject stepObject)
            {
                continue;
            }

            var configuration = stepObject["configuration"] as JsonObject ?? new JsonObject();
            result.Add(new ScenarioStepSnapshot(
                step.Key,
                ReadString(stepObject, "type") ?? string.Empty,
                ReadConfigurationStrings(configuration)));
        }

        return result;
    }

    private static IReadOnlyDictionary<string, string> ReadConfigurationStrings(JsonObject configuration)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in configuration)
        {
            if (string.Equals(property.Key, "attributes", StringComparison.Ordinal) &&
                property.Value is JsonObject attributes)
            {
                foreach (var attribute in attributes)
                {
                    if (TryReadConfigurationString(attribute.Value, out var attributeValue))
                    {
                        result[DashboardEventFilterCatalog.AttributeFilterKey(attribute.Key)] = attributeValue;
                    }
                }

                continue;
            }

            if (TryReadConfigurationString(property.Value, out var configurationValue))
            {
                result[property.Key] = configurationValue;
            }
        }

        return result;
    }

    private static bool TryReadConfigurationString(JsonNode? node, out string value)
    {
        if (node is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue<string>(out var text))
            {
                value = text;
                return true;
            }

            if (jsonValue.TryGetValue<bool>(out var boolean))
            {
                value = boolean ? "true" : "false";
                return true;
            }

            if (jsonValue.TryGetValue<double>(out var number))
            {
                value = number.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                return true;
            }
        }

        value = node?.ToJsonString() ?? string.Empty;
        return true;
    }

    private static int ReadInt(JsonObject obj, string propertyName, int fallback = 0)
    {
        if (obj[propertyName] is not JsonValue value)
        {
            return fallback;
        }

        if (value.TryGetValue<int>(out var integer))
        {
            return integer;
        }

        return value.TryGetValue<double>(out var number) ? (int)number : fallback;
    }

    private static string? ReadString(JsonObject obj, string propertyName)
        => obj[propertyName] is JsonValue value &&
           value.TryGetValue<string>(out var text) &&
           !string.IsNullOrWhiteSpace(text)
            ? text
            : null;

    private static (int Row, int Column)? FindFirstOpenDashboardCell(
        int columnCount,
        int rowCount,
        IReadOnlyList<DashboardCellSnapshot> cells)
    {
        for (var row = 0; row < rowCount; row++)
        {
            for (var column = 0; column < columnCount; column++)
            {
                if (!cells.Any(cell => CoversDashboardSlot(cell, row, column)))
                {
                    return (row, column);
                }
            }
        }

        return null;
    }

    private static void AssignWidgetToDashboardCell(JsonObject layout, JsonObject cells, string widgetName, string? requestedCellName)
    {
        var columns = ReadTrackStrings(layout, "columns", ["*"]).ToList();
        var rows = ReadTrackStrings(layout, "rows", ["*"]).ToList();
        var existingCells = ReadDashboardCells(cells);

        if (!string.IsNullOrWhiteSpace(requestedCellName))
        {
            if (cells[requestedCellName] is JsonObject existingCell)
            {
                existingCell["widget"] = widgetName;
                return;
            }

            if (TryParseSlotCellName(requestedCellName, out var requestedRow, out var requestedColumn) &&
                requestedRow >= 0 &&
                requestedColumn >= 0 &&
                requestedRow < rows.Count &&
                requestedColumn < columns.Count)
            {
                var coveringCell = existingCells.FirstOrDefault(cell => CoversDashboardSlot(cell, requestedRow, requestedColumn));
                if (coveringCell is not null && cells[coveringCell.Name] is JsonObject coveringCellObject)
                {
                    coveringCellObject["widget"] = widgetName;
                    return;
                }

                var cellName = MakeUniqueDashboardCellName(cells, "cell");
                cells[cellName] = CreateDashboardCellObject(new DashboardCellSnapshot(
                    cellName,
                    requestedRow,
                    requestedColumn,
                    1,
                    1,
                    widgetName));
                return;
            }
        }

        var emptyCell = existingCells.FirstOrDefault(static cell => string.IsNullOrWhiteSpace(cell.Widget));
        if (emptyCell is not null && cells[emptyCell.Name] is JsonObject emptyCellObject)
        {
            emptyCellObject["widget"] = widgetName;
            return;
        }

        var openPosition = FindFirstOpenDashboardCell(columns.Count, rows.Count, existingCells);
        if (openPosition is null)
        {
            rows.Add("*");
            var rowPadding = NormalizePaddingValues(ReadPaddingValues(layout, "rowPadding"), rows.Count - 1).ToList();
            rowPadding.Add(0);
            layout["rows"] = CreateTrackArray(rows);
            layout["rowPadding"] = CreateNumberArray(rowPadding);
            openPosition = (rows.Count - 1, 0);
        }

        var name = MakeUniqueDashboardCellName(cells, "cell");
        cells[name] = CreateDashboardCellObject(new DashboardCellSnapshot(
            name,
            openPosition.Value.Row,
            openPosition.Value.Column,
            1,
            1,
            widgetName));
    }

    private static bool TryParseSlotCellName(string name, out int row, out int column)
    {
        row = 0;
        column = 0;
        var parts = name.Split(':');
        return parts.Length == 3 &&
               string.Equals(parts[0], "slot", StringComparison.Ordinal) &&
               int.TryParse(parts[1], out row) &&
               int.TryParse(parts[2], out column);
    }

    private static bool TryGetSelectionBounds(
        IReadOnlyList<DashboardCellSnapshot> selectedCells,
        out (int MinRow, int MaxRow, int MinColumn, int MaxColumn) bounds)
    {
        bounds = default;
        if (selectedCells.Count < 2)
        {
            return false;
        }

        var coordinates = selectedCells.SelectMany(static cell => cell.CoveredCoordinates()).ToArray();
        var uniqueCoordinates = coordinates.ToHashSet();
        if (uniqueCoordinates.Count != coordinates.Length)
        {
            return false;
        }

        var minRow = uniqueCoordinates.Min(static coordinate => coordinate.Row);
        var maxRow = uniqueCoordinates.Max(static coordinate => coordinate.Row);
        var minColumn = uniqueCoordinates.Min(static coordinate => coordinate.Column);
        var maxColumn = uniqueCoordinates.Max(static coordinate => coordinate.Column);
        var expectedArea = (maxRow - minRow + 1) * (maxColumn - minColumn + 1);
        if (uniqueCoordinates.Count != expectedArea)
        {
            return false;
        }

        bounds = (minRow, maxRow, minColumn, maxColumn);
        return true;
    }

    private static bool CoversDashboardSlot(DashboardCellSnapshot cell, int row, int column)
        => row >= cell.Row &&
           row < cell.Row + cell.RowSpan &&
           column >= cell.Column &&
           column < cell.Column + cell.ColumnSpan;

    private static IReadOnlyList<string> ResizeTrackStrings(IReadOnlyList<string> tracks, int count)
    {
        var result = tracks.Take(count).ToList();
        while (result.Count < count)
        {
            result.Add("*");
        }

        return result;
    }

    private static IReadOnlyList<string> SubdivideTrackStrings(
        IReadOnlyList<string> tracks,
        int start,
        int span,
        int parts)
    {
        if (parts <= span)
        {
            return tracks;
        }

        var selected = tracks.Skip(start).Take(Math.Max(1, span)).ToArray();
        var children = CreateSubdividedTrackTokens(selected, parts);

        return tracks.Take(start)
            .Concat(children)
            .Concat(tracks.Skip(start + Math.Max(1, span)))
            .ToArray();
    }

    private static IReadOnlyList<double> SubdividePaddingValues(
        IReadOnlyList<double> values,
        int start,
        int span,
        int parts)
    {
        if (parts <= span)
        {
            return values;
        }

        var selected = values.Skip(start).Take(Math.Max(1, span)).ToArray();
        var child = selected.Length == 0 ? 0 : selected.Average();

        return values.Take(start)
            .Concat(Enumerable.Repeat(child, parts))
            .Concat(values.Skip(start + Math.Max(1, span)))
            .ToArray();
    }

    private static IReadOnlyList<string> CreateSubdividedTrackTokens(IReadOnlyList<string> selectedTokens, int parts)
    {
        var parsed = selectedTokens
            .Select(token =>
            {
                try
                {
                    return DashboardGridTrackDefinition.Parse(token);
                }
                catch
                {
                    return DashboardGridTrackDefinition.Star();
                }
            })
            .ToArray();

        if (parsed.Length == 0)
        {
            return Enumerable.Repeat("*", parts).ToArray();
        }

        var first = parsed[0];
        if (parsed.Any(track => track.Unit != first.Unit))
        {
            return Enumerable.Repeat("*", parts).ToArray();
        }

        var child = first with { Value = parsed.Sum(static track => track.Value) / parts };
        return Enumerable.Repeat(child.ToSizeString(), parts).ToArray();
    }

    private static DashboardCellSnapshot TransformDashboardCell(
        DashboardCellSnapshot cell,
        DashboardCellSnapshot selected,
        int rowInsertCount,
        int columnInsertCount)
    {
        var row = TransformDashboardDimension(cell.Row, cell.RowSpan, selected.Row, selected.RowSpan, rowInsertCount);
        var column = TransformDashboardDimension(cell.Column, cell.ColumnSpan, selected.Column, selected.ColumnSpan, columnInsertCount);

        return cell with
        {
            Row = row.Start,
            RowSpan = row.Span,
            Column = column.Start,
            ColumnSpan = column.Span
        };
    }

    private static (int Start, int Span) TransformDashboardDimension(
        int start,
        int span,
        int selectedStart,
        int selectedSpan,
        int insertCount)
    {
        if (insertCount == 0)
        {
            return (start, span);
        }

        var end = start + span;
        var selectedEnd = selectedStart + selectedSpan;

        if (start >= selectedEnd)
        {
            return (start + insertCount, span);
        }

        if (end <= selectedStart)
        {
            return (start, span);
        }

        return (start, span + insertCount);
    }

    private static IReadOnlyList<DashboardCellSnapshot> CreateSubdivisionCells(
        DashboardCellSnapshot selected,
        int rowParts,
        int columnParts)
    {
        var rowSpan = selected.RowSpan + Math.Max(0, rowParts - selected.RowSpan);
        var columnSpan = selected.ColumnSpan + Math.Max(0, columnParts - selected.ColumnSpan);
        var rowBands = CreateDashboardBands(selected.Row, rowSpan, rowParts);
        var columnBands = CreateDashboardBands(selected.Column, columnSpan, columnParts);

        return rowBands
            .SelectMany(rowBand => columnBands.Select(columnBand => new DashboardCellSnapshot(
                "cell",
                rowBand.Start,
                columnBand.Start,
                rowBand.Span,
                columnBand.Span)))
            .ToArray();
    }

    private static IReadOnlyList<(int Start, int Span)> CreateDashboardBands(int start, int span, int parts)
    {
        var baseSpan = span / parts;
        var remainder = span % parts;
        var bands = new List<(int Start, int Span)>(parts);
        var cursor = start;

        for (var index = 0; index < parts; index++)
        {
            var bandSpan = baseSpan + (index < remainder ? 1 : 0);
            bands.Add((cursor, bandSpan));
            cursor += bandSpan;
        }

        return bands;
    }

    private static void RemoveCellsOutsideGrid(JsonObject cells, int rowCount, int columnCount)
    {
        foreach (var cell in ReadDashboardCells(cells))
        {
            if (cell.Row < 0 ||
                cell.Column < 0 ||
                cell.Row + cell.RowSpan > rowCount ||
                cell.Column + cell.ColumnSpan > columnCount)
            {
                cells.Remove(cell.Name);
            }
        }
    }

    private static JsonObject CreateDashboardCellObject(DashboardCellSnapshot cell)
    {
        var result = new JsonObject
        {
            ["row"] = cell.Row,
            ["column"] = cell.Column,
            ["rowSpan"] = cell.RowSpan,
            ["columnSpan"] = cell.ColumnSpan
        };

        if (!string.IsNullOrWhiteSpace(cell.Widget))
        {
            result["widget"] = cell.Widget;
        }

        return result;
    }

    private static JsonArray CreateTrackArray(IEnumerable<string> tracks)
    {
        var array = new JsonArray();
        foreach (var track in tracks)
        {
            array.Add(track);
        }

        return array;
    }

    private static JsonArray CreateNumberArray(IEnumerable<double> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add(value);
        }

        return array;
    }

    private static JsonObject CreateConfigurationObject(IReadOnlyDictionary<string, string> configuration)
    {
        var result = new JsonObject();
        foreach (var (key, value) in configuration.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                result[key] = value ?? string.Empty;
            }
        }

        return result;
    }

    private static JsonObject CreateScenarioStepObject(JsonObject flowApplication, string stepType)
        => new()
        {
            ["type"] = stepType,
            ["configuration"] = CreateScenarioStepConfiguration(
                stepType,
                CreateDefaultScenarioStepConfiguration(flowApplication, stepType))
        };

    private static IReadOnlyDictionary<string, string> CreateDefaultScenarioStepConfiguration(
        JsonObject flowApplication,
        string stepType)
        => ScenarioStepCatalog.Shared.CreateDefaultConfiguration(
            stepType,
            ReadFirstConnectionResourceName(flowApplication));

    private static JsonObject CreateScenarioStepConfiguration(
        string stepType,
        IReadOnlyDictionary<string, string> configuration)
    {
        var result = new JsonObject();
        if (IsMqttPublishScenarioStep(stepType))
        {
            AddString(result, configuration, ScenarioStepCatalog.ConnectionKey);
            AddString(result, configuration, ScenarioStepCatalog.TopicKey);
            AddPayload(result, configuration);
            AddString(result, configuration, ScenarioStepCatalog.PayloadEncodingKey);
            AddInt(result, configuration, ScenarioStepCatalog.QosKey, 0);
            AddBool(result, configuration, ScenarioStepCatalog.RetainKey, false);
            return result;
        }

        if (IsMqttTriggerScenarioStep(stepType))
        {
            AddString(result, configuration, ScenarioStepCatalog.ConnectionKey);
            AddString(result, configuration, ScenarioStepCatalog.SubscriptionsKey);
            AddInt(result, configuration, ScenarioStepCatalog.QosKey, 1);
            AddBool(result, configuration, ScenarioStepCatalog.ReceiveRetainedKey, false);
            AddBool(result, configuration, ScenarioStepCatalog.RetainAsPublishedKey, true);
            return result;
        }

        AddString(result, configuration, ScenarioStepCatalog.EventTypeKey);
        AddString(result, configuration, ScenarioStepCatalog.TopicStartsWithKey);
        AddString(result, configuration, ScenarioStepCatalog.SubjectStartsWithKey);
        AddString(result, configuration, ScenarioStepCatalog.StatusKey);
        AddString(result, configuration, ScenarioStepCatalog.SourceKey);
        AddString(result, configuration, ScenarioStepCatalog.PayloadContainsKey);
        AddInt(result, configuration, ScenarioStepCatalog.TimeoutMsKey, 5000);
        AddAttributes(result, configuration);
        return result;
    }

    private static bool IsMqttPublishScenarioStep(string stepType)
        => ScenarioStepCatalog.Shared.Find(stepType)?.EditorKind == ScenarioStepEditorKind.MqttPublish;

    private static bool IsMqttTriggerScenarioStep(string stepType)
        => ScenarioStepCatalog.Shared.Find(stepType)?.EditorKind == ScenarioStepEditorKind.MqttTrigger;

    private static void AddString(JsonObject target, IReadOnlyDictionary<string, string> configuration, string key)
        => target[key] = configuration.TryGetValue(key, out var value) ? value ?? string.Empty : string.Empty;

    private static void AddInt(
        JsonObject target,
        IReadOnlyDictionary<string, string> configuration,
        string key,
        int fallback)
    {
        target[key] = configuration.TryGetValue(key, out var value) &&
                      int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private static void AddBool(
        JsonObject target,
        IReadOnlyDictionary<string, string> configuration,
        string key,
        bool fallback)
    {
        target[key] = configuration.TryGetValue(key, out var value) &&
                      bool.TryParse(value, out var parsed)
            ? parsed
            : fallback;
    }

    private static void AddAttributes(JsonObject target, IReadOnlyDictionary<string, string> configuration)
    {
        var attributes = new JsonObject();
        foreach (var (key, value) in configuration.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            if (DashboardEventFilterCatalog.TryGetAttributeName(key, out var attributeName) &&
                !string.IsNullOrWhiteSpace(value))
            {
                attributes[attributeName] = value.Trim();
            }
        }

        if (attributes.Count > 0)
        {
            target["attributes"] = attributes;
        }
    }

    private static void AddPayload(JsonObject target, IReadOnlyDictionary<string, string> configuration)
    {
        var payload = configuration.TryGetValue(ScenarioStepCatalog.PayloadKey, out var value)
            ? value ?? string.Empty
            : string.Empty;
        var encoding = configuration.TryGetValue(ScenarioStepCatalog.PayloadEncodingKey, out var configuredEncoding)
            ? configuredEncoding
            : string.Empty;
        if (string.Equals(encoding, "json", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(payload))
        {
            try
            {
                target[ScenarioStepCatalog.PayloadKey] = JsonNode.Parse(payload);
                return;
            }
            catch (JsonException)
            {
                // Store the raw value so the user can fix it without losing their input.
            }
        }

        target[ScenarioStepCatalog.PayloadKey] = payload;
    }

    private static string ReadFirstConnectionResourceName(JsonObject flowApplication)
    {
        if (flowApplication["resources"] is not JsonObject resources)
        {
            return string.Empty;
        }

        foreach (var resource in resources)
        {
            if (resource.Value is JsonObject resourceObject &&
                string.Equals(ReadString(resourceObject, "type"), "mqtt.connection", StringComparison.Ordinal))
            {
                return resource.Key;
            }
        }

        return string.Empty;
    }

    private static JsonObject GetOrCreateDashboardObject(JsonObject flowApplication, string dashboardName)
    {
        var dashboards = GetOrCreateObject(flowApplication, "dashboards");
        if (dashboards[dashboardName] is JsonObject dashboard)
        {
            return dashboard;
        }

        dashboard = CreateDashboard();
        dashboards[dashboardName] = dashboard;
        return dashboard;
    }

    private static string MakeUniqueDashboardCellName(JsonObject cells, string preferred)
    {
        if (!cells.ContainsKey(preferred))
        {
            return preferred;
        }

        var index = 2;
        while (cells.ContainsKey($"{preferred}{index}"))
        {
            index++;
        }

        return $"{preferred}{index}";
    }

    private static string MakeUniqueDashboardWidgetName(JsonObject widgets, string preferred)
    {
        if (!widgets.ContainsKey(preferred))
        {
            return preferred;
        }

        var index = 2;
        while (widgets.ContainsKey($"{preferred}{index}"))
        {
            index++;
        }

        return $"{preferred}{index}";
    }

    private static string MakeUniqueScenarioStepName(JsonObject steps, string preferred)
    {
        if (!steps.ContainsKey(preferred))
        {
            return preferred;
        }

        var index = 2;
        while (steps.ContainsKey($"{preferred}{index}"))
        {
            index++;
        }

        return $"{preferred}{index}";
    }

    private static string WidgetNamePrefix(string widgetType)
        => widgetType switch
        {
            DashboardWidgetCatalog.EventCounterType => "eventCounter",
            DashboardWidgetCatalog.LatestEventType => "latestEvent",
            _ => "widget"
        };

    private static string ScenarioStepNamePrefix(string stepType)
        => ScenarioStepCatalog.Shared.Find(stepType)?.NamePrefix ?? "step";

    private static JsonObject CreateDashboardWidgetObject(string widgetType)
        => new()
        {
            ["type"] = widgetType,
            ["configuration"] = CreateDashboardWidgetConfiguration(widgetType)
        };

    private static JsonObject CreateDashboardWidgetConfiguration(string widgetType)
    {
        var title = widgetType switch
        {
            DashboardWidgetCatalog.EventCounterType => "Events",
            DashboardWidgetCatalog.LatestEventType => "Latest event",
            _ => null
        };
        if (title is null)
        {
            return new JsonObject();
        }

        var configuration = CreateConfigurationObject(DashboardEventFilterCatalog.Shared.CreateEmptyConfiguration());
        configuration["title"] = title;
        return configuration;
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

    private static bool TryGetLinkCondition(
        JsonNode node,
        string sourceNodeName,
        string sourcePortName,
        out string? condition)
    {
        condition = null;

        if (node is JsonValue value &&
            value.TryGetValue<string>(out var reference) &&
            ReferenceMatches(reference, sourceNodeName, sourcePortName))
        {
            return true;
        }

        if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                if (item is not null && TryGetLinkCondition(item, sourceNodeName, sourcePortName, out condition))
                {
                    return true;
                }
            }

            return false;
        }

        if (node is JsonObject obj &&
            (obj.TryGetPropertyValue("from", out var fromNode) ||
             obj.TryGetPropertyValue("From", out fromNode)) &&
            fromNode is not null &&
            ContainsLinkReference(fromNode, BuildPortReference(sourceNodeName, sourcePortName)))
        {
            if ((obj.TryGetPropertyValue("when", out var whenNode) ||
                 obj.TryGetPropertyValue("When", out whenNode)) &&
                whenNode is JsonValue whenValue &&
                whenValue.TryGetValue<string>(out var conditionValue))
            {
                condition = conditionValue;
            }

            return true;
        }

        return false;
    }

    private static bool UpdateLinkCondition(
        JsonObject targetNode,
        string targetPortName,
        string sourceNodeName,
        string sourcePortName,
        string? condition)
    {
        if (targetNode[targetPortName] is not { } existing)
        {
            return false;
        }

        var updated = UpdateLinkCondition(existing, sourceNodeName, sourcePortName, condition, out var changed);
        if (!changed)
        {
            return false;
        }

        targetNode[targetPortName] = updated;
        return true;
    }

    private static JsonNode UpdateLinkCondition(
        JsonNode node,
        string sourceNodeName,
        string sourcePortName,
        string? condition,
        out bool changed)
    {
        changed = false;
        var normalizedCondition = string.IsNullOrWhiteSpace(condition) ? null : condition.Trim();

        if (node is JsonValue value &&
            value.TryGetValue<string>(out var reference) &&
            ReferenceMatches(reference, sourceNodeName, sourcePortName))
        {
            changed = true;
            return normalizedCondition is null
                ? JsonValue.Create(reference)!
                : new JsonObject
                {
                    ["from"] = reference,
                    ["when"] = normalizedCondition
                };
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

                var updatedItem = UpdateLinkCondition(item, sourceNodeName, sourcePortName, normalizedCondition, out var itemChanged);
                changed |= itemChanged;
                updatedArray.Add(updatedItem);
            }

            return changed ? updatedArray : node.DeepClone();
        }

        if (node is JsonObject obj &&
            (obj.TryGetPropertyValue("from", out var fromNode) ||
             obj.TryGetPropertyValue("From", out fromNode)) &&
            fromNode is not null &&
            ContainsLinkReference(fromNode, BuildPortReference(sourceNodeName, sourcePortName)))
        {
            changed = true;
            var updatedObject = (JsonObject)node.DeepClone();
            updatedObject["from"] = fromNode.DeepClone();
            updatedObject.Remove("From");
            updatedObject.Remove("When");

            if (normalizedCondition is null)
            {
                updatedObject.Remove("when");
                if (updatedObject.Count == 1 &&
                    updatedObject.TryGetPropertyValue("from", out var normalizedFrom) &&
                    normalizedFrom is JsonValue normalizedValue &&
                    normalizedValue.TryGetValue<string>(out var normalizedReference))
                {
                    return JsonValue.Create(normalizedReference)!;
                }
            }
            else
            {
                updatedObject["when"] = normalizedCondition;
            }

            return updatedObject;
        }

        return node.DeepClone();
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
        "mqtt.trigger" or "session.source" or "replay.source" or "generated.source" or "mqtt.connection-state-trigger"
            or TimerNodeTypes.Interval or TimerNodeTypes.Schedule => false,
        RoutingNodeTypes.Merge => false,
        "json.parse" or "json.stringify" or "text.encode" or "text.decode" or "base64.encode" or "base64.decode" => false,
        _ => true
    };

    private static bool IsSerializationTransform(string componentType)
        => componentType is "json.parse" or "json.stringify" or "text.encode" or "text.decode" or "base64.encode" or "base64.decode";

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

        if (componentType == "state.reducer")
        {
            return FindPreferredMapperNode(workflow, "StateReducerInput") is { Length: > 0 } mapper
                ? $"{mapper}.Output"
                : null;
        }

        return FindPreferredSourceNode(workflow) is { Length: > 0 } source
            ? $"{source}.Output"
            : null;
    }

    private static bool IsActor(string componentType)
        => componentType is "mqtt.publisher" or "mqtt.recorder" or "file.writer" or "http.request" or "payload.inspect";

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

    private static JsonObject CreateDynamicMapperConfiguration(string outputType, string inputType = "MqttEnvelope")
        => new()
        {
            ["engine"] = "jsonata",
            ["inputType"] = inputType,
            ["outputType"] = outputType,
            ["outputContract"] = "typed",
            ["expression"] = DynamicMapperNodeModel.DefaultExpression(outputType, "jsonata", inputType)
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

    private static JsonObject CreateRoutingSwitchConfiguration(string inputType = RoutingSwitchNodeModel.DefaultInputType)
    {
        var normalizedInputType = RoutingNodeConfiguration.NormalizeInputType(inputType);
        return new()
        {
            ["inputType"] = normalizedInputType,
            ["expression"] = DefaultRoutingSwitchExpression(normalizedInputType),
            ["routes"] = new JsonArray("True", "False"),
            ["routeOutputs"] = new JsonObject
            {
                ["True"] = "WhenTrue",
                ["False"] = "WhenFalse"
            },
            ["emitRouteEnvelope"] = false,
            ["boundedCapacity"] = RoutingSwitchNodeModel.DefaultBoundedCapacity
        };
    }

    private static string DefaultRoutingSwitchExpression(string inputType)
        => inputType is FlowContractTypeNames.MqttEnvelope
            or FlowContractTypeNames.MqttPublishRequest
            or FlowContractTypeNames.MqttRecordingRequest
            or FlowContractTypeNames.InspectedMqttMessage
            or FlowContractTypeNames.JsonSchemaValidationResult
            ? RoutingSwitchNodeModel.DefaultExpression
            : "input != null";

    private static JsonObject CreateRoutingForkConfiguration(string inputType = RoutingForkNodeModel.DefaultInputType)
        => new()
        {
            ["inputType"] = RoutingNodeConfiguration.NormalizeInputType(inputType),
            ["outputs"] = new JsonArray("A", "B"),
            ["boundedCapacity"] = RoutingForkNodeModel.DefaultBoundedCapacity
        };

    private static JsonObject CreateRoutingMergeConfiguration(string inputType = RoutingMergeNodeModel.DefaultInputType)
        => new()
        {
            ["inputType"] = RoutingNodeConfiguration.NormalizeInputType(inputType),
            ["inputs"] = new JsonArray("Left", "Right"),
            ["boundedCapacity"] = RoutingMergeNodeModel.DefaultBoundedCapacity
        };

    private static JsonObject CreateAssertionConfiguration()
        => new()
        {
            ["assertionName"] = FlowAssertionNodeModel.DefaultAssertionName,
            ["inputType"] = FlowAssertionNodeModel.DefaultInputType,
            ["expression"] = FlowAssertionNodeModel.DefaultExpression,
            ["failureMessage"] = FlowAssertionNodeModel.DefaultFailureMessage,
            ["boundedCapacity"] = FlowAssertionNodeModel.DefaultBoundedCapacity
        };

    private static JsonObject CreateStateReducerConfiguration()
        => new()
        {
            ["engine"] = StateReducerNodeModel.DefaultEngine,
            ["reducer"] = StateReducerNodeModel.DefaultReducer,
            ["boundedCapacity"] = StateReducerNodeModel.DefaultBoundedCapacity,
            ["maxKeys"] = StateReducerNodeModel.DefaultMaxKeys
        };

    private static JsonObject CreateMqttPublisherConfiguration(string? connectionName = null)
        => new()
        {
            ["connection"] = string.IsNullOrWhiteSpace(connectionName) ? BrokerResourceName : connectionName,
            ["boundedCapacity"] = 1000
        };

    private static JsonObject CreateConnectionReferenceConfiguration(string? connectionName = null)
        => new()
        {
            ["connection"] = string.IsNullOrWhiteSpace(connectionName) ? BrokerResourceName : connectionName
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

    private static JsonObject CreateStoredSessionSourceConfiguration()
        => new()
        {
            ["sessionId"] = string.Empty,
            ["preserveTiming"] = false,
            ["speed"] = 1,
            ["boundedCapacity"] = 1000
        };

    private static JsonObject CreateTimerIntervalConfiguration()
        => new()
        {
            ["intervalMilliseconds"] = TimerIntervalNodeModel.DefaultIntervalMilliseconds,
            ["initialDelayMilliseconds"] = TimerIntervalNodeModel.DefaultInitialDelayMilliseconds,
            ["emitImmediately"] = true,
            ["boundedCapacity"] = TimerIntervalNodeModel.DefaultBoundedCapacity
        };

    private static JsonObject CreateTimerScheduleConfiguration()
        => new()
        {
            ["cron"] = TimerScheduleNodeModel.DefaultCron,
            ["timeZoneId"] = TimerScheduleNodeModel.DefaultTimeZoneId,
            ["boundedCapacity"] = TimerScheduleNodeModel.DefaultBoundedCapacity
        };

    private static JsonObject CreateTimerDelayConfiguration(string inputType = TimerDelayNodeModel.DefaultInputType)
        => new()
        {
            ["inputType"] = TimerDelayNodeModel.NormalizeInputType(inputType),
            ["delayMilliseconds"] = TimerDelayNodeModel.DefaultDelayMilliseconds,
            ["boundedCapacity"] = TimerDelayNodeModel.DefaultBoundedCapacity
        };

    private static JsonObject CreateTimerDebounceConfiguration(string inputType = TimerDelayNodeModel.DefaultInputType)
        => new()
        {
            ["inputType"] = TimerDelayNodeModel.NormalizeInputType(inputType),
            ["quietPeriodMilliseconds"] = TimerDebounceNodeModel.DefaultQuietPeriodMilliseconds,
            ["boundedCapacity"] = TimerDebounceNodeModel.DefaultBoundedCapacity
        };

    private static JsonObject CreateTimerThrottleConfiguration(string inputType = TimerDelayNodeModel.DefaultInputType)
        => new()
        {
            ["inputType"] = TimerDelayNodeModel.NormalizeInputType(inputType),
            ["intervalMilliseconds"] = TimerThrottleNodeModel.DefaultIntervalMilliseconds,
            ["emitFirstImmediately"] = true,
            ["boundedCapacity"] = TimerThrottleNodeModel.DefaultBoundedCapacity
        };

    private static JsonObject CreateLoggerConfiguration()
        => new()
        {
            ["boundedCapacity"] = 1000,
            ["maxEntries"] = 500,
            ["includePayloadPreview"] = true,
            ["maxPayloadPreviewChars"] = 512
        };

    private static JsonObject CreateMetricsConfiguration()
        => new()
        {
            ["boundedCapacity"] = MqttMetricsNodeModel.DefaultBoundedCapacity,
            ["rateWindowSeconds"] = MqttMetricsNodeModel.DefaultRateWindowSeconds,
            ["metricCardColumns"] = MqttMetricsNodeModel.DefaultMetricCardColumns,
            ["displayMetrics"] = MqttMetricsNodeModel.BuildDisplayMetrics(MqttMetricsNodeModel.DefaultDisplayMetrics)
        };

    private static JsonObject CreateHttpRequestConfiguration()
        => new()
        {
            ["defaultTimeoutMilliseconds"] = 30000,
            ["maxResponseBodyBytes"] = 1048576,
            ["followRedirects"] = true,
            ["treatNonSuccessStatusAsError"] = false,
            ["boundedCapacity"] = 128
        };

    private static JsonObject CreatePayloadInspectConfiguration()
        => new()
        {
            ["maxPreviewBytes"] = 1024,
            ["maxFormattedChars"] = 4096,
            ["detectBase64"] = true,
            ["formatJson"] = true,
            ["formatXml"] = true,
            ["boundedCapacity"] = 128
        };

    private static JsonObject CreateActorCapacityConfiguration()
        => new()
        {
            ["boundedCapacity"] = 1000
        };

    private static JsonObject CreateTransformCapacityConfiguration()
        => new()
        {
            ["boundedCapacity"] = 1000
        };

    private static JsonObject CreateDashboard()
        => new()
        {
            ["layout"] = new JsonObject
            {
                ["columns"] = new JsonArray("320", "*"),
                ["rows"] = new JsonArray("180", "*"),
                ["columnPadding"] = new JsonArray(0, 0),
                ["rowPadding"] = new JsonArray(0, 0),
                ["cells"] = new JsonObject()
            },
            ["widgets"] = new JsonObject()
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
