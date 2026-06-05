using FluxMq.UI.Services;
using MudBlazor;

namespace FluxMq.UI.Components.Workspace;

public sealed record DashboardWidgetSettingsProfile(
    string Type,
    string Title,
    string Icon,
    bool IsEventWidget,
    bool IsTopicTreeWidget,
    bool UsesVisualMetrics = false,
    bool UsesGaugeStyle = false,
    bool UsesChartType = false)
{
    public bool ShowsEventTabs => IsEventWidget;
}

public static class DashboardWidgetSettingsProfiles
{
    public static DashboardWidgetSettingsProfile For(string? type)
    {
        var normalized = string.IsNullOrWhiteSpace(type) ? string.Empty : type.Trim();
        return normalized switch
        {
            DashboardWidgetCatalog.KpiTileType => Event(
                normalized,
                "KPI tile",
                Icons.Material.Filled.Speed,
                usesVisualMetrics: true),
            DashboardWidgetCatalog.StatusStripType => Event(
                normalized,
                "Status strip",
                Icons.Material.Filled.ViewWeek,
                usesVisualMetrics: true),
            DashboardWidgetCatalog.RateTileType => Event(
                normalized,
                "Rate tile",
                Icons.Material.Filled.QueryStats,
                usesVisualMetrics: true),
            DashboardWidgetCatalog.EventCounterType => Event(
                normalized,
                "Events",
                Icons.Material.Filled.Numbers),
            DashboardWidgetCatalog.LatestEventType => Event(
                normalized,
                "Latest event",
                Icons.Material.Filled.Bolt),
            DashboardWidgetCatalog.EventRateType => Event(
                normalized,
                "Event rate",
                Icons.Material.Filled.Speed),
            DashboardWidgetCatalog.EventGaugeType => Event(
                normalized,
                "Event gauge",
                Icons.Material.Filled.DonutLarge,
                usesVisualMetrics: true,
                usesGaugeStyle: true),
            DashboardWidgetCatalog.EventChartType => Event(
                normalized,
                "Event chart",
                Icons.Material.Filled.StackedLineChart,
                usesVisualMetrics: true,
                usesChartType: true),
            DashboardWidgetCatalog.LineChartType => Event(
                normalized,
                "Line chart",
                Icons.Material.Filled.StackedLineChart,
                usesVisualMetrics: true,
                usesChartType: true),
            DashboardWidgetCatalog.AreaChartType => Event(
                normalized,
                "Area chart",
                Icons.Material.Filled.AreaChart,
                usesVisualMetrics: true,
                usesChartType: true),
            DashboardWidgetCatalog.BarChartType => Event(
                normalized,
                "Bar chart",
                Icons.Material.Filled.BarChart,
                usesVisualMetrics: true,
                usesChartType: true),
            DashboardWidgetCatalog.DonutChartType => Event(
                normalized,
                "Donut chart",
                Icons.Material.Filled.DonutLarge,
                usesVisualMetrics: true),
            DashboardWidgetCatalog.EventTableType => Event(
                normalized,
                "Event table",
                Icons.Material.Filled.TableRows),
            DashboardWidgetCatalog.TopicActivityType => Event(
                normalized,
                "Topic activity",
                Icons.Material.Filled.GridOn),
            DashboardWidgetCatalog.PayloadDistributionType => Event(
                normalized,
                "Payload sizes",
                Icons.Material.Filled.DataArray,
                usesVisualMetrics: true),
            DashboardWidgetCatalog.QosRetainBreakdownType => Event(
                normalized,
                "QoS / retain",
                Icons.Material.Filled.PieChart,
                usesVisualMetrics: true),
            DashboardWidgetCatalog.TopicTreeType => new(
                normalized,
                "Topic tree",
                Icons.Material.Filled.AccountTree,
                IsEventWidget: false,
                IsTopicTreeWidget: true),
            _ => new(
                normalized,
                "Widget",
                Icons.Material.Filled.Widgets,
                IsEventWidget: false,
                IsTopicTreeWidget: false)
        };
    }

    private static DashboardWidgetSettingsProfile Event(
        string type,
        string title,
        string icon,
        bool usesVisualMetrics = false,
        bool usesGaugeStyle = false,
        bool usesChartType = false)
        => new(
            type,
            title,
            icon,
            IsEventWidget: true,
            IsTopicTreeWidget: false,
            usesVisualMetrics,
            usesGaugeStyle,
            usesChartType);
}
