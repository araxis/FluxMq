using FluxMq.App.Metrics;
using System.Globalization;
using System.Text.Json.Nodes;

namespace FluxMq.App.Definitions;

public static class FluxMqApplicationDefinitionMigrator
{
    public const int CurrentDashboardVersion = 2;
    public const int CurrentTestVersion = 2;
    private const string StatusStripType = "status.strip";
    private const string StatusValueType = "status.value";
    private const string EventGaugeType = "event.gauge";
    private const string EventChartType = "event.chart";
    private const string LineChartType = "chart.line";
    private const string AreaChartType = "chart.area";
    private const string BarChartType = "chart.bar";
    private const string KpiTileType = "kpi.tile";
    private const string QosRetainBreakdownType = "qos.retain.breakdown";
    private const string QosBreakdownType = "qos.breakdown";
    private const string RetainBreakdownType = "retain.breakdown";
    private const string PrimaryMetricKey = "primaryMetric";
    private const string DisplayMetricsKey = "displayMetrics";
    private const string MetricCardColumnsKey = "metricCardColumns";
    private const string ChartTypeKey = "chartType";

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
        NormalizeAppMetrics(flowApplication);
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
            HardSplitDashboardWidgets(dashboardObject, layout);

            var metrics = GetOrCreateObject(dashboardObject, "metrics");
            var bindings = GetOrCreateObject(dashboardObject, "bindings");
            var appMetrics = GetOrCreateObject(flowApplication, "metrics");
            if (dashboardObject["widgets"] is JsonObject widgets)
            {
                foreach (var widget in widgets)
                {
                    if (widget.Value is JsonObject widgetObject)
                    {
                        EnsureDashboardWidgetBinding(widget.Key, widgetObject, metrics, bindings, appMetrics);
                    }
                }
            }

            PromoteDashboardMetrics(flowApplication, dashboard.Key, dashboardObject, metrics, bindings);
        }
    }

    private static void PromoteDashboardMetrics(
        JsonObject flowApplication,
        string dashboardName,
        JsonObject dashboard,
        JsonObject dashboardMetrics,
        JsonObject bindings)
    {
        if (dashboardMetrics.Count == 0)
        {
            return;
        }

        var appMetrics = GetOrCreateObject(flowApplication, "metrics");
        var promotedNames = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var metric in dashboardMetrics)
        {
            if (metric.Value is not JsonObject metricObject)
            {
                continue;
            }

            var promotedName = MakeMetricArtifactName(dashboardName, metric.Key);
            promotedNames[metric.Key] = promotedName;
            appMetrics[promotedName] = CreateMetricResource(promotedName, metric.Key, metricObject);
        }

        if (promotedNames.Count == 0)
        {
            return;
        }

        foreach (var binding in bindings)
        {
            if (binding.Value is not JsonObject bindingObject)
            {
                continue;
            }

            if (ReadString(bindingObject, "primaryMetric") is { } primaryMetric &&
                promotedNames.TryGetValue(primaryMetric, out var promotedPrimary))
            {
                bindingObject["primaryMetric"] = promotedPrimary;
            }

            if (bindingObject["metrics"] is JsonArray metricArray)
            {
                for (var index = 0; index < metricArray.Count; index++)
                {
                    if (metricArray[index] is JsonValue value &&
                        value.TryGetValue<string>(out var metricName) &&
                        promotedNames.TryGetValue(metricName, out var promotedMetric))
                    {
                        metricArray[index] = promotedMetric;
                    }
                }
            }
        }

        if (dashboard["widgets"] is JsonObject widgets)
        {
            foreach (var widget in widgets)
            {
                if (widget.Value is not JsonObject widgetObject ||
                    widgetObject["configuration"] is not JsonObject configuration ||
                    ReadString(configuration, "metric") is not { } metricName ||
                    !promotedNames.TryGetValue(metricName, out var promotedMetric))
                {
                    continue;
                }

                configuration["metric"] = promotedMetric;
            }
        }

        dashboard.Remove("metrics");
    }

    // Builds a typeId + flat-parameters metric resource from a legacy dashboard-local metric
    // ({ source, aggregation, window, groupBy, filters{...}, format{...} }).
    private static JsonObject CreateMetricResource(
        string promotedName,
        string localMetricName,
        JsonObject metric)
    {
        var filters = metric["filters"] as JsonObject ?? new JsonObject();
        var parameters = new JsonObject
        {
            ["window"] = ReadString(metric, "window") ?? "60s"
        };
        AddParameter(parameters, "eventType", ReadString(filters, "eventType"));
        AddParameter(parameters, "topicStartsWith", ReadString(filters, "topicStartsWith"));
        AddParameter(parameters, "topicNotStartsWith", ReadString(filters, "topicNotStartsWith"));
        AddParameter(parameters, "status", ReadString(filters, "status"));
        AddParameter(parameters, "groupBy", ReadString(metric, "groupBy"));
        AddParameter(parameters, "format", ReadString(metric["format"] as JsonObject ?? new JsonObject(), "unit"));
        LiftAttributeParameters(parameters, filters);

        return new JsonObject
        {
            ["typeId"] = MeasureToTypeId(ReadString(metric, "aggregation") ?? "count"),
            ["displayName"] = ToDisplayName(localMetricName),
            ["description"] = string.Empty,
            ["parameters"] = parameters,
            ["labels"] = new JsonObject
            {
                ["promotedFrom"] = promotedName
            },
            ["exportPolicy"] = new JsonObject
            {
                ["enabled"] = false
            }
        };
    }

    // Migrates app-level metric entries to the resource shape. Legacy artifact entries (with a nested
    // "definition") are converted to typeId + parameters; entries that already carry a "typeId" are left as-is.
    private static void NormalizeAppMetrics(JsonObject flowApplication)
    {
        if (flowApplication["metrics"] is not JsonObject metrics)
        {
            return;
        }

        foreach (var metric in metrics.ToArray())
        {
            if (metric.Value is JsonObject metricObject && metricObject["definition"] is JsonObject)
            {
                metrics[metric.Key] = ConvertArtifactToResource(metricObject);
            }
        }
    }

    private static JsonObject ConvertArtifactToResource(JsonObject artifact)
    {
        var definition = artifact["definition"] as JsonObject ?? new JsonObject();
        var parameters = new JsonObject
        {
            ["window"] = ReadString(definition, "window") ?? "60s"
        };
        AddParameter(parameters, "eventType", ReadString(definition, "eventType"));
        AddParameter(parameters, "topicStartsWith", ReadString(definition, "topicStartsWith"));
        AddParameter(parameters, "topicNotStartsWith", ReadString(definition, "topicNotStartsWith"));
        AddParameter(parameters, "status", ReadString(definition, "status"));
        if (definition["additionalFilters"] is JsonObject additionalFilters)
        {
            LiftAttributeParameters(parameters, additionalFilters);
        }

        return new JsonObject
        {
            ["typeId"] = MeasureToTypeId(ReadString(definition, "measure") ?? "count"),
            ["displayName"] = ReadString(artifact, "displayName") ?? ReadString(definition, "name") ?? "Metric",
            ["description"] = ReadString(artifact, "description") ?? string.Empty,
            ["parameters"] = parameters,
            ["labels"] = (artifact["labels"] as JsonObject)?.DeepClone() as JsonObject ?? new JsonObject(),
            ["exportPolicy"] = (artifact["exportPolicy"] as JsonObject)?.DeepClone() as JsonObject
                ?? new JsonObject { ["enabled"] = false }
        };
    }

    private static string MeasureToTypeId(string measure)
        => measure switch
        {
            "rate" => EventRateMetricType.Id,
            "topics" => UniqueTopicCountMetricType.Id,
            "payloadBytes" => PayloadBytesMetricType.Id,
            "averagePayload" => AveragePayloadMetricType.Id,
            "retained" => RetainedCountMetricType.Id,
            _ => EventCountMetricType.Id
        };

    private static void AddParameter(JsonObject parameters, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parameters[key] = value;
        }
    }

    // The new event filter model keeps qos and retain as first-class flat parameters; accepts both a nested
    // "attributes" object and flat "attribute:<name>" keys, and drops attributes the model no longer supports.
    private static void LiftAttributeParameters(JsonObject parameters, JsonObject source)
    {
        if (source["attributes"] is JsonObject attributes)
        {
            foreach (var attribute in attributes)
            {
                AddAttributeParameter(parameters, attribute.Key, attribute.Value);
            }
        }

        foreach (var item in source)
        {
            if (item.Key.StartsWith("attribute:", StringComparison.Ordinal))
            {
                AddAttributeParameter(parameters, item.Key["attribute:".Length..], item.Value);
            }
        }
    }

    private static void AddAttributeParameter(JsonObject parameters, string name, JsonNode? value)
    {
        if (!string.Equals(name, "qos", StringComparison.Ordinal) &&
            !string.Equals(name, "retain", StringComparison.Ordinal))
        {
            return;
        }

        if (TryReadScalarString(value, out var text))
        {
            parameters[name] = text;
        }
    }

    private static string MakeMetricArtifactName(string dashboardName, string metricName)
        => FluxMetricNaming.ToDashboardScopedId(dashboardName, metricName);

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

    private static void HardSplitDashboardWidgets(JsonObject dashboard, JsonObject layout)
    {
        if (dashboard["widgets"] is not JsonObject widgets)
        {
            return;
        }

        foreach (var item in widgets.ToArray())
        {
            if (item.Value is not JsonObject widget)
            {
                continue;
            }

            var type = ReadString(widget, "type") ?? string.Empty;
            var configuration = GetOrCreateObject(widget, "configuration");
            switch (type)
            {
                case StatusStripType:
                    SplitMetricStrip(widgets, layout, item.Key, widget, configuration, StatusValueType, "Status value");
                    break;
                case EventGaugeType:
                    SplitGaugeMetricCards(widgets, layout, item.Key, configuration);
                    RemoveCompositeMetricKeys(configuration);
                    break;
                case EventChartType:
                    widget["type"] = ChartTypeFromConfiguration(configuration);
                    RemoveCompositeMetricKeys(configuration);
                    break;
                case QosRetainBreakdownType:
                    widget["type"] = QosBreakdownType;
                    configuration["title"] = "QoS breakdown";
                    AddSplitWidget(widgets, layout, item.Key, RetainBreakdownType, "Retain breakdown", configuration);
                    RemoveCompositeMetricKeys(configuration);
                    break;
            }
        }
    }

    private static void SplitMetricStrip(
        JsonObject widgets,
        JsonObject layout,
        string widgetName,
        JsonObject widget,
        JsonObject configuration,
        string focusedType,
        string focusedTitle)
    {
        var primaryMetric = ReadString(configuration, PrimaryMetricKey) ?? "recent";
        var metrics = ReadCsv(configuration, DisplayMetricsKey);
        widget["type"] = focusedType;
        configuration["title"] = focusedTitle;
        configuration[PrimaryMetricKey] = primaryMetric;
        RemoveCompositeMetricKeys(configuration);

        foreach (var metric in metrics.Where(metric => !string.Equals(metric, primaryMetric, StringComparison.Ordinal)))
        {
            AddSplitWidget(widgets, layout, widgetName, focusedType, MetricTitle(metric), configuration, metric);
        }
    }

    private static void SplitGaugeMetricCards(
        JsonObject widgets,
        JsonObject layout,
        string widgetName,
        JsonObject configuration)
    {
        var primaryMetric = ReadString(configuration, PrimaryMetricKey) ?? "recent";
        foreach (var metric in ReadCsv(configuration, DisplayMetricsKey)
                     .Where(metric => !string.Equals(metric, primaryMetric, StringComparison.Ordinal)))
        {
            AddSplitWidget(widgets, layout, widgetName, KpiTileType, MetricTitle(metric), configuration, metric);
        }
    }

    private static void AddSplitWidget(
        JsonObject widgets,
        JsonObject layout,
        string sourceWidgetName,
        string type,
        string title,
        JsonObject sourceConfiguration,
        string? primaryMetric = null)
    {
        var widgetName = MakeUniqueName(widgets, NormalizeIdentifier(type));
        var configuration = CloneObject(sourceConfiguration);
        configuration["title"] = title;
        if (!string.IsNullOrWhiteSpace(primaryMetric))
        {
            configuration[PrimaryMetricKey] = primaryMetric;
        }

        RemoveCompositeMetricKeys(configuration);
        widgets[widgetName] = new JsonObject
        {
            ["type"] = type,
            ["configuration"] = configuration
        };

        AppendWidgetCell(layout, sourceWidgetName, widgetName);
    }

    private static void AppendWidgetCell(JsonObject layout, string sourceWidgetName, string widgetName)
    {
        var rows = GetOrCreateArray(layout, "rows");
        var columns = GetOrCreateArray(layout, "columns");
        if (columns.Count == 0)
        {
            columns.Add("*");
        }

        var cells = GetOrCreateObject(layout, "cells");
        var sourceCell = cells.FirstOrDefault(cell =>
            cell.Value is JsonObject cellObject &&
            string.Equals(ReadString(cellObject, "widget"), sourceWidgetName, StringComparison.Ordinal));
        var row = sourceCell.Value is JsonObject sourceObject
            ? ReadInt(sourceObject, "row", rows.Count)
            : rows.Count;
        var column = FindOpenColumn(cells, row, columns.Count);
        if (column < 0)
        {
            row = rows.Count;
            rows.Add("*");
            column = 0;
        }

        var cellName = MakeUniqueName(cells, "cell");
        cells[cellName] = new JsonObject
        {
            ["row"] = row,
            ["column"] = column,
            ["rowSpan"] = 1,
            ["columnSpan"] = 1,
            ["widget"] = widgetName
        };
    }

    private static int FindOpenColumn(JsonObject cells, int row, int columnCount)
    {
        for (var column = 0; column < columnCount; column++)
        {
            var occupied = cells.Any(cell =>
                cell.Value is JsonObject cellObject &&
                ReadInt(cellObject, "row", -1) == row &&
                ReadInt(cellObject, "column", -1) == column);
            if (!occupied)
            {
                return column;
            }
        }

        return -1;
    }

    private static string ChartTypeFromConfiguration(JsonObject configuration)
        => ReadString(configuration, ChartTypeKey) switch
        {
            "line" => LineChartType,
            "area" => AreaChartType,
            _ => BarChartType
        };

    private static void RemoveCompositeMetricKeys(JsonObject configuration)
    {
        configuration.Remove(DisplayMetricsKey);
        configuration.Remove(MetricCardColumnsKey);
    }

    private static IReadOnlyList<string> ReadCsv(JsonObject obj, string propertyName)
        => (ReadString(obj, propertyName) ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static string MetricTitle(string metric)
        => metric switch
        {
            "messages" => "Messages",
            "recent" => "Recent events",
            "currentRate" => "Event rate",
            "topics" => "Topics",
            "payloadBytes" => "Payload bytes",
            "retained" => "Retained messages",
            "averagePayload" => "Average payload",
            _ => metric
        };

    private static void EnsureDashboardWidgetBinding(
        string widgetName,
        JsonObject widget,
        JsonObject metrics,
        JsonObject bindings,
        JsonObject appMetrics)
    {
        var configuration = GetOrCreateObject(widget, "configuration");
        var metricName = ReadString(configuration, "metric");
        if (string.IsNullOrWhiteSpace(metricName))
        {
            metricName = MakeUniqueName(metrics, MetricNamePrefix(widgetName));
            configuration["metric"] = metricName;
        }

        if (!metrics.ContainsKey(metricName) && !appMetrics.ContainsKey(metricName))
        {
            metrics[metricName] = CreateMetricFromWidget(widget, configuration);
        }

        if (string.Equals(ReadString(widget, "type"), KpiTileType, StringComparison.Ordinal))
        {
            var legacyMetric = ReadString(configuration, PrimaryMetricKey);
            if (!string.IsNullOrWhiteSpace(legacyMetric) &&
                string.Equals(ReadString(configuration, "title"), "KPI tile", StringComparison.OrdinalIgnoreCase))
            {
                configuration["title"] = MetricTitle(legacyMetric);
            }

            configuration.Remove(PrimaryMetricKey);
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
        var aggregation = MetricAggregationFromWidget(type, configuration);
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
                ["unit"] = MetricFormatFromAggregation(aggregation)
            }
        };

        if (!string.IsNullOrWhiteSpace(groupBy))
        {
            metric["groupBy"] = groupBy;
        }

        return metric;
    }

    private static string MetricAggregationFromWidget(string type, JsonObject configuration)
    {
        if (string.Equals(type, KpiTileType, StringComparison.Ordinal))
        {
            return ReadString(configuration, PrimaryMetricKey) switch
            {
                "currentRate" => "rate",
                "topics" => "topics",
                "payloadBytes" => "payloadBytes",
                "averagePayload" => "averagePayload",
                "retained" => "retained",
                _ => "count"
            };
        }

        return type.Contains("rate", StringComparison.OrdinalIgnoreCase)
            ? "rate"
            : "count";
    }

    private static string MetricFormatFromAggregation(string aggregation)
        => aggregation is "payloadBytes" or "averagePayload"
            ? "bytes"
            : "number";

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

    private static JsonArray GetOrCreateArray(JsonObject parent, string propertyName)
    {
        if (parent[propertyName] is JsonArray existing)
        {
            return existing;
        }

        var created = new JsonArray();
        parent[propertyName] = created;
        return created;
    }

    private static string? ReadString(JsonObject obj, string propertyName)
        => obj[propertyName] is JsonValue value &&
           value.TryGetValue<string>(out var text) &&
           !string.IsNullOrWhiteSpace(text)
            ? text
            : null;

    private static bool TryReadScalarString(JsonNode? node, out string value)
    {
        value = string.Empty;
        if (node is not JsonValue jsonValue)
        {
            return false;
        }

        if (jsonValue.TryGetValue<string>(out var text))
        {
            value = text;
        }
        else if (jsonValue.TryGetValue<bool>(out var boolean))
        {
            value = boolean ? "true" : "false";
        }
        else if (jsonValue.TryGetValue<long>(out var integer))
        {
            value = integer.ToString(CultureInfo.InvariantCulture);
        }
        else if (jsonValue.TryGetValue<double>(out var number))
        {
            value = number.ToString(CultureInfo.InvariantCulture);
        }

        value = value.Trim();
        return !string.IsNullOrWhiteSpace(value);
    }

    private static int ReadInt(JsonObject obj, string propertyName, int fallback)
        => obj[propertyName] is JsonValue value &&
           value.TryGetValue<int>(out var number)
            ? number
            : fallback;

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

    private static string MetricNamePrefix(string widgetName)
    {
        var identifier = NormalizeIdentifier(widgetName);
        var suffixStart = identifier.Length;
        while (suffixStart > 0 && char.IsDigit(identifier[suffixStart - 1]))
        {
            suffixStart--;
        }

        if (suffixStart == identifier.Length)
        {
            return $"{identifier}Metric";
        }

        return $"{identifier[..suffixStart]}Metric{identifier[suffixStart..]}";
    }

    private static string ToTitle(string value)
        => string.IsNullOrWhiteSpace(value)
            ? "Phase"
            : char.ToUpperInvariant(value[0]) + value[1..];

    private static string ToDisplayName(string value)
        => string.IsNullOrWhiteSpace(value)
            ? "Metric"
            : value
                .Replace('.', ' ')
                .Replace('_', ' ')
                .Replace('-', ' ')
                .Trim();

    private static int PhaseOrder(string phaseName)
        => DefaultPhases.FirstOrDefault(phase => string.Equals(phase.Name, phaseName, StringComparison.Ordinal)).Order;
}
