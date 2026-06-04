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

        return result;
    }

    public static JsonObject CreateWidget(string widgetType)
        => new()
        {
            ["type"] = widgetType,
            ["configuration"] = CreateWidgetConfiguration(widgetType)
        };

    public static JsonObject CreateWidgetConfiguration(string widgetType)
    {
        var title = widgetType switch
        {
            DashboardWidgetCatalog.EventCounterType => "Events",
            DashboardWidgetCatalog.LatestEventType => "Latest event",
            DashboardWidgetCatalog.EventRateType => "Event rate",
            DashboardWidgetCatalog.EventGaugeType => "Event gauge",
            DashboardWidgetCatalog.EventChartType => "Event chart",
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
        else if (string.Equals(widgetType, DashboardWidgetCatalog.EventChartType, StringComparison.Ordinal))
        {
            configuration[DashboardWidgetCatalog.PrimaryMetricKey] = DashboardWidgetCatalog.MetricMessages;
            configuration[DashboardWidgetCatalog.ChartTypeKey] = DashboardWidgetCatalog.ChartTypeBars;
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

    public static string WidgetNamePrefix(string widgetType)
        => widgetType switch
        {
            DashboardWidgetCatalog.EventCounterType => "eventCounter",
            DashboardWidgetCatalog.LatestEventType => "latestEvent",
            DashboardWidgetCatalog.EventRateType => "eventRate",
            DashboardWidgetCatalog.EventGaugeType => "eventGauge",
            DashboardWidgetCatalog.EventChartType => "eventChart",
            DashboardWidgetCatalog.TopicTreeType => "topicTree",
            _ => "widget"
        };
}
