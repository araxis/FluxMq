using FluxMq.UI.Models;
using System.Text.Json.Nodes;

namespace FluxMq.UI.Services;

public static class FlowDashboardDefinitionFactory
{
    public static JsonObject CreateDashboard()
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

    public static JsonObject CreateCell(DashboardCellSnapshot cell)
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

        if (cell.Style.Count > 0)
        {
            result["style"] = CreateConfiguration(cell.Style);
        }

        return result;
    }

    public static JsonObject CreateWidget(string widgetType)
        => new()
        {
            ["type"] = DashboardWidgetCatalog.NormalizeWidgetTypeForAdd(widgetType),
            ["configuration"] = CreateWidgetConfiguration(widgetType)
        };

    public static JsonObject CreateWidgetConfiguration(string widgetType)
    {
        var normalizedType = DashboardWidgetCatalog.NormalizeWidgetTypeForAdd(widgetType);
        var module = DashboardWidgetModuleCatalog.Find(normalizedType);
        if (module is not null)
        {
            return CreateConfiguration(module.DefaultConfiguration);
        }

        var title = widgetType switch
        {
            DashboardWidgetCatalog.KpiTileType => "KPI tile",
            DashboardWidgetCatalog.StatusStripType => "Status strip",
            DashboardWidgetCatalog.StatusValueType => "Status value",
            DashboardWidgetCatalog.RateTileType => "Rate tile",
            DashboardWidgetCatalog.EventCounterType => "Events",
            DashboardWidgetCatalog.LatestEventType => "Latest event",
            DashboardWidgetCatalog.EventRateType => "Event rate",
            DashboardWidgetCatalog.EventGaugeType => "Event gauge",
            DashboardWidgetCatalog.EventChartType => "Event chart",
            DashboardWidgetCatalog.LineChartType => "Line chart",
            DashboardWidgetCatalog.AreaChartType => "Area chart",
            DashboardWidgetCatalog.BarChartType => "Bar chart",
            DashboardWidgetCatalog.DonutChartType => "Donut chart",
            DashboardWidgetCatalog.EventTableType => "Event table",
            DashboardWidgetCatalog.TopicActivityType => "Topic activity",
            DashboardWidgetCatalog.PayloadDistributionType => "Payload sizes",
            DashboardWidgetCatalog.QosRetainBreakdownType => "QoS / retain",
            DashboardWidgetCatalog.QosBreakdownType => "QoS breakdown",
            DashboardWidgetCatalog.RetainBreakdownType => "Retain breakdown",
            DashboardWidgetCatalog.TopicTreeType => "Topic tree",
            _ => null
        };
        if (title is null)
        {
            return new JsonObject();
        }

        if (DashboardWidgetCatalog.IsTopicTreeWidget(widgetType))
        {
            return new JsonObject
            {
                ["title"] = title,
                [DashboardWidgetCatalog.ExcludeSystemTopicsKey] = "true"
            };
        }

        var configuration = DashboardWidgetCatalog.IsEventWidget(widgetType)
            ? CreateConfiguration(DashboardEventFilterCatalog.Shared.CreateEmptyConfiguration())
            : new JsonObject();
        configuration["title"] = title;
        if (DashboardWidgetCatalog.IsVisualEventWidget(widgetType))
        {
            configuration[DashboardWidgetCatalog.PrimaryMetricKey] = DashboardWidgetCatalog.MetricRecent;
            configuration[DashboardWidgetCatalog.DisplayMetricsKey] =
                DashboardWidgetCatalog.BuildDisplayMetrics(null);
            configuration[DashboardWidgetCatalog.MetricCardColumnsKey] =
                DashboardWidgetCatalog.DefaultMetricCardColumns.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (string.Equals(widgetType, DashboardWidgetCatalog.EventGaugeType, StringComparison.Ordinal))
        {
            configuration[DashboardWidgetCatalog.GaugeStyleKey] = DashboardWidgetCatalog.GaugeStyleRing;
        }
        else if (DashboardWidgetCatalog.IsChartWidget(widgetType))
        {
            configuration[DashboardWidgetCatalog.PrimaryMetricKey] = DashboardWidgetCatalog.MetricMessages;
            configuration[DashboardWidgetCatalog.ChartTypeKey] = widgetType switch
            {
                DashboardWidgetCatalog.LineChartType => DashboardWidgetCatalog.ChartTypeLine,
                DashboardWidgetCatalog.AreaChartType => DashboardWidgetCatalog.ChartTypeArea,
                DashboardWidgetCatalog.TopicActivityType => DashboardWidgetCatalog.ChartTypeTopics,
                _ => DashboardWidgetCatalog.ChartTypeBars
            };
        }
        else if (DashboardWidgetCatalog.IsBreakdownWidget(widgetType))
        {
            configuration[DashboardWidgetCatalog.PrimaryMetricKey] = DashboardWidgetCatalog.MetricMessages;
            configuration[DashboardWidgetCatalog.DisplayMetricsKey] =
                DashboardWidgetCatalog.BuildDisplayMetrics(null);
            configuration[DashboardWidgetCatalog.MetricCardColumnsKey] =
                DashboardWidgetCatalog.DefaultMetricCardColumns.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return configuration;
    }

    public static JsonObject CreateConfiguration(IReadOnlyDictionary<string, string> configuration)
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

    public static JsonObject CreateMetric(DashboardMetricQueryDefinition query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var filters = new JsonObject();
        AddIfPresent(filters, DashboardEventFilterCatalog.EventTypeKey, query.EventType);
        AddIfPresent(filters, DashboardEventFilterCatalog.TopicStartsWithKey, query.TopicStartsWith);
        AddIfPresent(filters, DashboardEventFilterCatalog.TopicNotStartsWithKey, query.TopicNotStartsWith);
        AddIfPresent(filters, DashboardEventFilterCatalog.StatusKey, query.Status);
        foreach (var (key, value) in query.AdditionalFilters.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            AddIfPresent(filters, key, value);
        }

        var result = new JsonObject
        {
            ["source"] = Normalize(query.Source, "runtimeEvents"),
            ["aggregation"] = Normalize(query.Aggregation, "count"),
            ["window"] = Normalize(query.Window, "60s"),
            ["filters"] = filters,
            ["format"] = new JsonObject
            {
                ["unit"] = Normalize(query.Format, "number")
            }
        };

        if (!string.IsNullOrWhiteSpace(query.GroupBy))
        {
            result["groupBy"] = query.GroupBy.Trim();
        }

        return result;
    }

    public static string WidgetNamePrefix(string widgetType)
        => widgetType switch
        {
            DashboardWidgetCatalog.KpiTileType => "kpiTile",
            DashboardWidgetCatalog.StatusStripType => "statusStrip",
            DashboardWidgetCatalog.StatusValueType => "statusValue",
            DashboardWidgetCatalog.RateTileType => "rateTile",
            DashboardWidgetCatalog.EventCounterType => "eventCounter",
            DashboardWidgetCatalog.LatestEventType => "latestEvent",
            DashboardWidgetCatalog.EventRateType => "eventRate",
            DashboardWidgetCatalog.EventGaugeType => "eventGauge",
            DashboardWidgetCatalog.EventChartType => "eventChart",
            DashboardWidgetCatalog.LineChartType => "lineChart",
            DashboardWidgetCatalog.AreaChartType => "areaChart",
            DashboardWidgetCatalog.BarChartType => "barChart",
            DashboardWidgetCatalog.DonutChartType => "donutChart",
            DashboardWidgetCatalog.EventTableType => "eventTable",
            DashboardWidgetCatalog.TopicActivityType => "topicActivity",
            DashboardWidgetCatalog.PayloadDistributionType => "payloadDistribution",
            DashboardWidgetCatalog.QosRetainBreakdownType => "qosRetainBreakdown",
            DashboardWidgetCatalog.QosBreakdownType => "qosBreakdown",
            DashboardWidgetCatalog.RetainBreakdownType => "retainBreakdown",
            DashboardWidgetCatalog.TopicTreeType => "topicTree",
            _ => "widget"
        };

    private static void AddIfPresent(JsonObject target, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            target[key] = value.Trim();
        }
    }

    private static string Normalize(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}
