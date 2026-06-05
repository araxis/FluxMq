using System.Text.Json.Nodes;

namespace FluxMq.App.Definitions;

public static class FluxMqApplicationDefinitionMigrator
{
    public const int CurrentDashboardVersion = 2;
    public const int CurrentTestVersion = 2;

    private static readonly IReadOnlyList<(string Name, string Title, string Kind, int Order)> DefaultPhases =
    [
        ("setup", "Setup", "setup", 0),
        ("stimulus", "Stimulus", "stimulus", 1),
        ("observe", "Observe", "observe", 2),
        ("assert", "Assert", "assert", 3),
        ("cleanup", "Cleanup", "cleanup", 4)
    ];

    private static readonly IReadOnlyList<string> DashboardFilterKeys =
    [
        "eventType",
        "topicStartsWith",
        "topicNotStartsWith",
        "subjectStartsWith",
        "status"
    ];

    public static JsonObject MigrateRoot(JsonObject root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var flowApplication = GetFlowApplication(root);
        MigrateDashboards(flowApplication);
        MigrateTests(flowApplication);
        return root;
    }

    private static JsonObject GetFlowApplication(JsonObject root)
    {
        if (root["FluxMq"] is JsonObject fluxMq &&
            fluxMq["FlowApplication"] is JsonObject wrapped)
        {
            return wrapped;
        }

        if (root["FlowApplication"] is JsonObject direct)
        {
            return direct;
        }

        return root;
    }

    private static void MigrateDashboards(JsonObject flowApplication)
    {
        if (flowApplication["dashboards"] is not JsonObject dashboards)
        {
            return;
        }

        foreach (var dashboard in dashboards)
        {
            if (dashboard.Value is not JsonObject dashboardObject)
            {
                continue;
            }

            dashboardObject["version"] = CurrentDashboardVersion;
            var layout = GetOrCreateObject(dashboardObject, "layout");
            EnsureDashboardResponsiveDefinition(dashboardObject, layout);
            EnsureDashboardView(dashboardObject);

            var metrics = GetOrCreateObject(dashboardObject, "metrics");
            var bindings = GetOrCreateObject(dashboardObject, "bindings");
            if (dashboardObject["widgets"] is JsonObject widgets)
            {
                foreach (var widget in widgets)
                {
                    if (widget.Value is JsonObject widgetObject)
                    {
                        EnsureDashboardWidgetBinding(widget.Key, widgetObject, metrics, bindings);
                    }
                }
            }
        }
    }

    private static void EnsureDashboardResponsiveDefinition(JsonObject dashboard, JsonObject layout)
    {
        var responsive = GetOrCreateObject(dashboard, "responsive");
        if (ReadString(responsive, "defaultBreakpoint") is null)
        {
            responsive["defaultBreakpoint"] = "desktop";
        }

        var breakpoints = GetOrCreateObject(responsive, "breakpoints");
        if (!breakpoints.ContainsKey("desktop"))
        {
            breakpoints["desktop"] = new JsonObject
            {
                ["columns"] = ReadArrayCount(layout, "columns", 12),
                ["minWidth"] = 1024
            };
        }

        if (!breakpoints.ContainsKey("tablet"))
        {
            breakpoints["tablet"] = new JsonObject
            {
                ["columns"] = 8,
                ["minWidth"] = 720
            };
        }

        if (!breakpoints.ContainsKey("mobile"))
        {
            breakpoints["mobile"] = new JsonObject
            {
                ["columns"] = 4,
                ["minWidth"] = 0
            };
        }
    }

    private static void EnsureDashboardView(JsonObject dashboard)
    {
        var view = GetOrCreateObject(dashboard, "view");
        if (ReadString(view, "mode") is null)
        {
            view["mode"] = "design";
        }

        if (ReadString(view, "breakpoint") is null)
        {
            view["breakpoint"] = "desktop";
        }
    }

    private static void EnsureDashboardWidgetBinding(
        string widgetName,
        JsonObject widget,
        JsonObject metrics,
        JsonObject bindings)
    {
        var configuration = GetOrCreateObject(widget, "configuration");
        var metricName = ReadString(configuration, "metric");
        if (string.IsNullOrWhiteSpace(metricName))
        {
            metricName = MakeUniqueName(metrics, $"{NormalizeIdentifier(widgetName)}Metric");
            configuration["metric"] = metricName;
        }

        if (!metrics.ContainsKey(metricName))
        {
            metrics[metricName] = CreateMetricFromWidget(widget, configuration);
        }

        if (bindings[widgetName] is not JsonObject binding)
        {
            bindings[widgetName] = new JsonObject
            {
                ["primaryMetric"] = metricName,
                ["metrics"] = new JsonArray(metricName)
            };
            return;
        }

        if (ReadString(binding, "primaryMetric") is null)
        {
            binding["primaryMetric"] = metricName;
        }

        if (binding["metrics"] is not JsonArray metricArray || metricArray.Count == 0)
        {
            binding["metrics"] = new JsonArray(metricName);
        }
    }

    private static JsonObject CreateMetricFromWidget(JsonObject widget, JsonObject configuration)
    {
        var filters = new JsonObject();
        foreach (var key in DashboardFilterKeys)
        {
            if (configuration[key]?.DeepClone() is { } value)
            {
                filters[key] = value;
            }
        }

        var attributes = new JsonObject();
        foreach (var item in configuration)
        {
            if (item.Key.StartsWith("attribute:", StringComparison.Ordinal) &&
                item.Value?.DeepClone() is { } attributeValue)
            {
                attributes[item.Key["attribute:".Length..]] = attributeValue;
            }
        }

        if (attributes.Count > 0)
        {
            filters["attributes"] = attributes;
        }

        var type = ReadString(widget, "type") ?? string.Empty;
        var aggregation = type.Contains("rate", StringComparison.OrdinalIgnoreCase)
            ? "rate"
            : "count";
        var groupBy = type.Contains("topic", StringComparison.OrdinalIgnoreCase)
            ? "topic"
            : null;

        var metric = new JsonObject
        {
            ["source"] = type.Contains("topic", StringComparison.OrdinalIgnoreCase)
                ? "topicProjection"
                : "runtimeEvents",
            ["aggregation"] = aggregation,
            ["window"] = "60s",
            ["filters"] = filters,
            ["format"] = new JsonObject
            {
                ["unit"] = aggregation == "rate" ? "/s" : "events"
            }
        };

        if (!string.IsNullOrWhiteSpace(groupBy))
        {
            metric["groupBy"] = groupBy;
        }

        return metric;
    }

    private static void MigrateTests(JsonObject flowApplication)
    {
        if (flowApplication["tests"] is not JsonObject tests)
        {
            return;
        }

        foreach (var test in tests)
        {
            if (test.Value is not JsonObject scenario)
            {
                continue;
            }

            scenario["version"] = CurrentTestVersion;
            EnsureRunProfile(scenario);
            EnsurePhases(scenario);
            EnsureScenarioHistoryMetadata(scenario);
        }
    }

    private static void EnsureRunProfile(JsonObject scenario)
    {
        var runProfile = GetOrCreateObject(scenario, "runProfile");
        if (ReadString(runProfile, "mode") is null)
        {
            runProfile["mode"] = "local";
        }

        if (!runProfile.ContainsKey("timeoutMs"))
        {
            runProfile["timeoutMs"] = 30000;
        }

        if (!runProfile.ContainsKey("stopOnFailure"))
        {
            runProfile["stopOnFailure"] = true;
        }
    }

    private static void EnsurePhases(JsonObject scenario)
    {
        var legacySteps = scenario["steps"] as JsonObject;
        var phases = GetOrCreateObject(scenario, "phases");
        if (phases.Count == 0)
        {
            if (legacySteps is { Count: > 0 })
            {
                phases["imported"] = new JsonObject
                {
                    ["title"] = "Imported",
                    ["kind"] = "imported",
                    ["order"] = 0,
                    ["steps"] = CloneObject(legacySteps)
                };
            }
            else
            {
                foreach (var phase in DefaultPhases)
                {
                    phases[phase.Name] = CreatePhase(phase.Title, phase.Kind, phase.Order);
                }
            }
        }
        else
        {
            foreach (var phase in phases)
            {
                if (phase.Value is not JsonObject phaseObject)
                {
                    continue;
                }

                if (ReadString(phaseObject, "title") is null)
                {
                    phaseObject["title"] = ToTitle(phase.Key);
                }

                if (ReadString(phaseObject, "kind") is null)
                {
                    phaseObject["kind"] = phase.Key;
                }

                if (!phaseObject.ContainsKey("order"))
                {
                    phaseObject["order"] = PhaseOrder(phase.Key);
                }

                GetOrCreateObject(phaseObject, "steps");
            }
        }

        if (legacySteps is null)
        {
            scenario["steps"] = ClonePhaseSteps(phases);
        }
    }

    private static JsonObject CreatePhase(string title, string kind, int order)
        => new()
        {
            ["title"] = title,
            ["kind"] = kind,
            ["order"] = order,
            ["steps"] = new JsonObject()
        };

    private static JsonObject ClonePhaseSteps(JsonObject phases)
    {
        var result = new JsonObject();
        foreach (var phase in phases.OrderBy(static phase => PhaseOrder(phase.Key)))
        {
            if (phase.Value is not JsonObject phaseObject ||
                phaseObject["steps"] is not JsonObject steps)
            {
                continue;
            }

            foreach (var step in steps)
            {
                result[step.Key] = step.Value?.DeepClone();
            }
        }

        return result;
    }

    private static void EnsureScenarioHistoryMetadata(JsonObject scenario)
    {
        if (scenario["runHistory"] is not JsonArray)
        {
            scenario["runHistory"] = new JsonArray();
        }

        if (scenario["reportSnapshots"] is not JsonArray)
        {
            scenario["reportSnapshots"] = new JsonArray();
        }
    }

    private static JsonObject CloneObject(JsonObject source)
    {
        var result = new JsonObject();
        foreach (var item in source)
        {
            result[item.Key] = item.Value?.DeepClone();
        }

        return result;
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

    private static string? ReadString(JsonObject obj, string propertyName)
        => obj[propertyName] is JsonValue value &&
           value.TryGetValue<string>(out var text) &&
           !string.IsNullOrWhiteSpace(text)
            ? text
            : null;

    private static int ReadArrayCount(JsonObject obj, string propertyName, int fallback)
        => obj[propertyName] is JsonArray array && array.Count > 0
            ? array.Count
            : fallback;

    private static string MakeUniqueName(JsonObject obj, string preferred)
    {
        if (!obj.ContainsKey(preferred))
        {
            return preferred;
        }

        var index = 2;
        while (obj.ContainsKey($"{preferred}{index}"))
        {
            index++;
        }

        return $"{preferred}{index}";
    }

    private static string NormalizeIdentifier(string value)
    {
        var result = new string(value
            .Where(static character => char.IsLetterOrDigit(character))
            .ToArray());

        if (string.IsNullOrWhiteSpace(result))
        {
            return "widget";
        }

        return char.ToLowerInvariant(result[0]) + result[1..];
    }

    private static string ToTitle(string value)
        => string.IsNullOrWhiteSpace(value)
            ? "Phase"
            : char.ToUpperInvariant(value[0]) + value[1..];

    private static int PhaseOrder(string phaseName)
        => DefaultPhases.FirstOrDefault(phase => string.Equals(phase.Name, phaseName, StringComparison.Ordinal)).Order;
}
