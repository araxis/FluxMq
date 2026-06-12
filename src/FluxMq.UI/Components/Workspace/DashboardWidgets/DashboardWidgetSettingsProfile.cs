using FluxMq.UI.Services;
using MudBlazor;

namespace FluxMq.UI.Components.Workspace;

public sealed record DashboardWidgetSettingsProfile(
    string Type,
    string Title,
    string Icon,
    bool IsEventWidget,
    bool IsTopicTreeWidget,
    bool UsesMetricVisualization = false,
    bool UsesVisualMetrics = false,
    bool UsesGaugeStyle = false,
    bool UsesChartType = false,
    bool UsesMetricAggregation = false,
    bool SupportsMetricSlots = false,
    bool UsesMetricWindow = false,
    bool UsesSubtitle = false,
    DashboardWidgetInspectorLabels? Labels = null)
{
    public bool ShowsEventTabs => IsEventWidget;

    public bool UsesMetricQuery => IsEventWidget;

    public bool UsesEventFilters => IsEventWidget;

    public DashboardWidgetInspectorLabels InspectorLabels { get; } =
        Labels ?? DashboardWidgetInspectorLabels.Default;
}

public sealed record DashboardWidgetInspectorLabels(
    string DataGroup,
    string MetricRow,
    string SeriesGroup,
    string PrimarySeriesRow,
    string SeriesRowPrefix,
    string AddSeriesRow,
    string TimeWindowGroup,
    string WindowRow,
    string FilterGroup,
    string EventRow,
    string StatusRow,
    string DisplayGroup,
    string TitleRow,
    string PrimaryCardRow,
    string CardRowPrefix,
    string AddCardRow,
    string ColumnsRow,
    string GaugeRow,
    string ChartRow,
    string TopicSystemRow)
{
    public static DashboardWidgetInspectorLabels Default { get; } = new(
        "Data",
        "Metric",
        "Series",
        "Primary series",
        "Series",
        "Add series",
        "Time window",
        "Window",
        "Filters",
        "Event",
        "Status",
        "Display",
        "Title",
        "Primary card",
        "Card",
        "Add card",
        "Columns",
        "Gauge",
        "Chart",
        "System");

    public static DashboardWidgetInspectorLabels EventRate { get; } = Default with
    {
        DataGroup = "Rate source",
        MetricRow = "Rate metric",
        TimeWindowGroup = "Rate window",
        WindowRow = "Range",
        FilterGroup = "Traffic filter",
        DisplayGroup = "Rate display",
        TitleRow = "Label"
    };

    public static DashboardWidgetInspectorLabels EventCounter { get; } = Default with
    {
        DataGroup = "Counter source",
        MetricRow = "Counter metric",
        TimeWindowGroup = "Counter query",
        FilterGroup = "Counter filter",
        DisplayGroup = "Counter display",
        TitleRow = "Header"
    };

    public static DashboardWidgetInspectorLabels StatusValue { get; } = Default with
    {
        DataGroup = "Status source",
        MetricRow = "Status metric",
        DisplayGroup = "Status display",
        TitleRow = "Header"
    };

    public static DashboardWidgetInspectorLabels EventGauge { get; } = Default with
    {
        DataGroup = "Gauge source",
        MetricRow = "Gauge metric",
        DisplayGroup = "Gauge display",
        TitleRow = "Header",
        GaugeRow = "Shape"
    };

    public static DashboardWidgetInspectorLabels LatestEvent { get; } = Default with
    {
        DataGroup = "Event source",
        MetricRow = "Match metric",
        FilterGroup = "Match rules",
        EventRow = "Event type",
        StatusRow = "Event status",
        DisplayGroup = "Latest event",
        TitleRow = "Header"
    };

    public static DashboardWidgetInspectorLabels EventTable { get; } = Default with
    {
        DataGroup = "Table source",
        MetricRow = "Rows metric",
        FilterGroup = "Row filter",
        EventRow = "Event type",
        StatusRow = "Row status",
        DisplayGroup = "Table display",
        TitleRow = "Header"
    };

    public static DashboardWidgetInspectorLabels TopicTree { get; } = Default with
    {
        DisplayGroup = "Topic tree",
        TitleRow = "Header",
        TopicSystemRow = "System topics"
    };

    public static DashboardWidgetInspectorLabels Chart { get; } = Default with
    {
        SeriesGroup = "Chart series",
        PrimarySeriesRow = "Primary series",
        SeriesRowPrefix = "Series",
        AddSeriesRow = "Add series",
        TimeWindowGroup = "Chart window",
        WindowRow = "Range",
        FilterGroup = "Chart filter",
        DisplayGroup = "Chart display",
        ChartRow = "Plot"
    };

    public static DashboardWidgetInspectorLabels Kpi { get; } = Default with
    {
        DataGroup = "KPI source",
        MetricRow = "KPI metric",
        TimeWindowGroup = "Metric query",
        WindowRow = "Window",
        DisplayGroup = "KPI display",
        TitleRow = "Title",
        PrimaryCardRow = "Value metric"
    };
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
                usesMetricVisualization: true,
                usesMetricAggregation: true,
                usesMetricWindow: true,
                usesSubtitle: true,
                labels: DashboardWidgetInspectorLabels.Kpi),
            DashboardWidgetCatalog.StatusStripType => Event(
                normalized,
                "Status strip",
                Icons.Material.Filled.ViewWeek,
                usesVisualMetrics: true,
                usesMetricWindow: true),
            DashboardWidgetCatalog.StatusValueType => Event(
                normalized,
                "Status value",
                Icons.Material.Filled.Verified,
                labels: DashboardWidgetInspectorLabels.StatusValue),
            DashboardWidgetCatalog.RateTileType => Event(
                normalized,
                "Rate tile",
                Icons.Material.Filled.QueryStats,
                usesMetricVisualization: true,
                labels: DashboardWidgetInspectorLabels.EventRate),
            DashboardWidgetCatalog.EventCounterType => Event(
                normalized,
                "Events",
                Icons.Material.Filled.Numbers,
                usesMetricVisualization: true,
                labels: DashboardWidgetInspectorLabels.EventCounter),
            DashboardWidgetCatalog.LatestEventType => Event(
                normalized,
                "Latest event",
                Icons.Material.Filled.Bolt,
                labels: DashboardWidgetInspectorLabels.LatestEvent),
            DashboardWidgetCatalog.EventRateType => Event(
                normalized,
                "Event rate",
                Icons.Material.Filled.Speed,
                usesMetricVisualization: true,
                labels: DashboardWidgetInspectorLabels.EventRate),
            DashboardWidgetCatalog.EventGaugeType => Event(
                normalized,
                "Event gauge",
                Icons.Material.Filled.DonutLarge,
                usesGaugeStyle: true,
                labels: DashboardWidgetInspectorLabels.EventGauge),
            DashboardWidgetCatalog.EventChartType => Event(
                normalized,
                "Event chart",
                Icons.Material.Filled.StackedLineChart,
                usesVisualMetrics: true,
                usesChartType: true,
                usesMetricWindow: true,
                labels: DashboardWidgetInspectorLabels.Chart),
            DashboardWidgetCatalog.LineChartType => Event(
                normalized,
                "Line chart",
                Icons.Material.Filled.StackedLineChart,
                usesVisualMetrics: true,
                usesChartType: true,
                usesMetricWindow: true,
                labels: DashboardWidgetInspectorLabels.Chart),
            DashboardWidgetCatalog.AreaChartType => Event(
                normalized,
                "Area chart",
                Icons.Material.Filled.AreaChart,
                usesVisualMetrics: true,
                usesChartType: true,
                usesMetricWindow: true,
                labels: DashboardWidgetInspectorLabels.Chart),
            DashboardWidgetCatalog.BarChartType => Event(
                normalized,
                "Bar chart",
                Icons.Material.Filled.BarChart,
                usesVisualMetrics: true,
                usesChartType: true,
                usesMetricWindow: true,
                labels: DashboardWidgetInspectorLabels.Chart),
            DashboardWidgetCatalog.DonutChartType => Event(
                normalized,
                "Donut chart",
                Icons.Material.Filled.DonutLarge,
                usesVisualMetrics: true),
            DashboardWidgetCatalog.EventTableType => Event(
                normalized,
                "Event table",
                Icons.Material.Filled.TableRows,
                labels: DashboardWidgetInspectorLabels.EventTable),
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
            DashboardWidgetCatalog.QosBreakdownType => Event(
                normalized,
                "QoS breakdown",
                Icons.Material.Filled.PieChart,
                usesVisualMetrics: true),
            DashboardWidgetCatalog.RetainBreakdownType => Event(
                normalized,
                "Retain breakdown",
                Icons.Material.Filled.PushPin,
                usesVisualMetrics: true),
            DashboardWidgetCatalog.TopicTreeType => new(
                normalized,
                "Topic tree",
                Icons.Material.Filled.AccountTree,
                IsEventWidget: false,
                IsTopicTreeWidget: true,
                Labels: DashboardWidgetInspectorLabels.TopicTree),
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
        bool usesMetricVisualization = false,
        bool usesVisualMetrics = false,
        bool usesGaugeStyle = false,
        bool usesChartType = false,
        bool usesMetricAggregation = false,
        bool supportsMetricSlots = false,
        bool usesMetricWindow = false,
        bool usesSubtitle = false,
        DashboardWidgetInspectorLabels? labels = null)
        => new(
            type,
            title,
            icon,
            IsEventWidget: true,
            IsTopicTreeWidget: false,
            usesMetricVisualization,
            usesVisualMetrics,
            usesGaugeStyle,
            usesChartType,
            usesMetricAggregation,
            supportsMetricSlots,
            usesMetricWindow,
            usesSubtitle,
            labels);
}
