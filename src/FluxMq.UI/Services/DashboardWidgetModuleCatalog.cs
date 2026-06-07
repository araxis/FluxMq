using FluxMq.UI.Components.Workspace;
using FluxMq.UI.Models;
using MudBlazor;

namespace FluxMq.UI.Services;

public static class DashboardWidgetModuleCatalog
{
    public static IReadOnlyList<DashboardWidgetModule> CreateModules()
    {
        var eventFilter = EventFilterProperties();
        return
        [
            MetricModule(
                DashboardWidgetCatalog.KpiTileType,
                "KPI Tile",
                "Metrics",
                "Shows one named operational metric with compact comparison context.",
                Icons.Material.Filled.Speed,
                typeof(DashboardKpiTileModuleView),
                "Messages",
                "messages",
                [KpiMetricGroup(), VisualizationGroup(), FormatGroup(), ThresholdGroup()]),
            MetricModule(
                DashboardWidgetCatalog.StatusValueType,
                "Status Value",
                "Metrics",
                "Shows one operational status or selected metric value.",
                Icons.Material.Filled.Verified,
                typeof(DashboardStatusValueModuleView),
                "Status value",
                "recent",
                [MetricGroup("status-source", "Status source"), FormatGroup()],
                [DashboardWidgetCatalog.StatusStripType]),
            MetricModule(
                DashboardWidgetCatalog.RateTileType,
                "Rate Tile",
                "Metrics",
                "Shows one named rate metric.",
                Icons.Material.Filled.QueryStats,
                typeof(DashboardRateTileModuleView),
                "Rate tile",
                "currentRate",
                [MetricGroup("metric", "Metric"), WindowGroup(), FormatGroup(), ThresholdGroup()]),
            EventModule(
                DashboardWidgetCatalog.EventCounterType,
                "Event Counter",
                "Events",
                "Counts matching runtime events.",
                Icons.Material.Filled.Numbers,
                typeof(DashboardEventCounterModuleView),
                "Events",
                [MetricQueryGroup("counter-source", "Counter source"), FormatGroup("count-format", "Count display")]),
            EventModule(
                DashboardWidgetCatalog.LatestEventType,
                "Latest Event",
                "Events",
                "Shows the latest matching runtime event.",
                Icons.Material.Filled.Bolt,
                typeof(DashboardLatestEventModuleView),
                "Latest event",
                [eventFilter, FieldGroup("fields", "Fields")],
                preferredColumns: 2,
                preferredRows: 1),
            EventModule(
                DashboardWidgetCatalog.EventRateType,
                "Event Rate",
                "Events",
                "Shows the current runtime event rate.",
                Icons.Material.Filled.Speed,
                typeof(DashboardEventRateModuleView),
                "Event rate",
                [MetricQueryGroup("rate-source", "Rate source"), FormatGroup("rate-format", "Rate display")],
                preferredColumns: 2),
            EventModule(
                DashboardWidgetCatalog.EventGaugeType,
                "Event Gauge",
                "Events",
                "Renders one metric as a gauge.",
                Icons.Material.Filled.DonutLarge,
                typeof(DashboardEventGaugeModuleView),
                "Event gauge",
                [MetricGroup("metric", "Metric"), GaugeGroup(), ThresholdGroup()],
                preferredColumns: 2),
            ChartModule(
                DashboardWidgetCatalog.LineChartType,
                "Line Chart",
                "Charts",
                "Shows one time-series metric as a line.",
                Icons.Material.Filled.StackedLineChart,
                typeof(DashboardLineChartModuleView),
                "Line chart",
                DashboardWidgetCatalog.ChartTypeLine,
                [MetricGroup("metric", "Metric"), WindowGroup(), AxisGroup(), ChartLineGroup()],
                [DashboardWidgetCatalog.EventChartType]),
            ChartModule(
                DashboardWidgetCatalog.AreaChartType,
                "Area Chart",
                "Charts",
                "Shows one time-series metric as a filled area.",
                Icons.Material.Filled.AreaChart,
                typeof(DashboardAreaChartModuleView),
                "Area chart",
                DashboardWidgetCatalog.ChartTypeArea,
                [MetricGroup("metric", "Metric"), WindowGroup(), AxisGroup(), ChartFillGroup()]),
            ChartModule(
                DashboardWidgetCatalog.BarChartType,
                "Bar Chart",
                "Charts",
                "Shows one bucketed metric as bars.",
                Icons.Material.Filled.BarChart,
                typeof(DashboardBarChartModuleView),
                "Bar chart",
                DashboardWidgetCatalog.ChartTypeBars,
                [MetricGroup("metric", "Metric"), WindowGroup(), AxisGroup(), BarGroup()]),
            EventModule(
                DashboardWidgetCatalog.DonutChartType,
                "Donut Chart",
                "Charts",
                "Shows one categorical breakdown.",
                Icons.Material.Filled.DonutLarge,
                typeof(DashboardDonutChartModuleView),
                "Donut chart",
                [MetricGroup("metric", "Metric"), CategoryGroup()],
                preferredColumns: 2),
            EventModule(
                DashboardWidgetCatalog.EventTableType,
                "Event Table",
                "Events",
                "Lists recent matching runtime events.",
                Icons.Material.Filled.TableRows,
                typeof(DashboardEventTableModuleView),
                "Event table",
                [eventFilter, TableGroup()],
                preferredColumns: 2,
                preferredRows: 2),
            EventModule(
                DashboardWidgetCatalog.TopicActivityType,
                "Topic Activity",
                "Topics",
                "Shows top topic activity.",
                Icons.Material.Filled.GridOn,
                typeof(DashboardTopicActivityModuleView),
                "Topic activity",
                [MetricGroup("topic-metric", "Topic metric"), CategoryGroup("top-topics", "Top topics")],
                dataRequirements: ["topicProjection"],
                preferredColumns: 2,
                preferredRows: 2),
            EventModule(
                DashboardWidgetCatalog.PayloadDistributionType,
                "Payload Size Distribution",
                "MQTT Ops",
                "Shows payload size buckets.",
                Icons.Material.Filled.DataArray,
                typeof(DashboardPayloadDistributionModuleView),
                "Payload sizes",
                [MetricGroup("source", "Source"), BucketGroup()],
                dataRequirements: ["runtimeEvents", "payload"],
                preferredColumns: 2),
            EventModule(
                DashboardWidgetCatalog.QosBreakdownType,
                "QoS Breakdown",
                "MQTT Ops",
                "Shows QoS distribution.",
                Icons.Material.Filled.PieChart,
                typeof(DashboardQosBreakdownModuleView),
                "QoS breakdown",
                [MetricGroup("source", "Source"), BreakdownGroup()],
                dataRequirements: ["runtimeEvents", "mqttAttributes"],
                preferredColumns: 2,
                compatibilityTypeIds: [DashboardWidgetCatalog.QosRetainBreakdownType]),
            EventModule(
                DashboardWidgetCatalog.RetainBreakdownType,
                "Retain Breakdown",
                "MQTT Ops",
                "Shows retained-message distribution.",
                Icons.Material.Filled.PushPin,
                typeof(DashboardRetainBreakdownModuleView),
                "Retain breakdown",
                [MetricGroup("source", "Source"), BreakdownGroup()],
                dataRequirements: ["runtimeEvents", "mqttAttributes"],
                preferredColumns: 2),
            new(
                new DashboardWidgetDescriptor(
                    DashboardWidgetCatalog.TopicTreeType,
                    "Topic Tree",
                    "Topics",
                    "Shows live MQTT topics as a dashboard tree.",
                    Icons.Material.Filled.AccountTree,
                    "Topic hierarchy",
                    DashboardWidgetRendererKind.TopicTree,
                    DashboardWidgetEditorKind.TopicTree,
                    ["topicProjection"]),
                typeof(DashboardTopicTreeModuleView),
                typeof(DashboardTopicTreeModuleView),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["title"] = "Topic tree",
                    [DashboardWidgetCatalog.ExcludeSystemTopicsKey] = "true"
                },
                [TopicTreeGroup()],
                DefaultStyle(),
                new DashboardWidgetLayoutContract(1, 2, 2, 2))
        ];
    }

    public static DashboardWidgetModule? Find(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return null;
        }

        var normalized = type.Trim();
        return CreateModules().FirstOrDefault(module =>
            string.Equals(module.Type, normalized, StringComparison.Ordinal) ||
            module.CompatibilityTypeIds.Contains(normalized, StringComparer.Ordinal));
    }

    private static DashboardWidgetModule MetricModule(
        string type,
        string displayName,
        string category,
        string description,
        string icon,
        Type component,
        string title,
        string primaryMetric,
        IReadOnlyList<DashboardWidgetPropertyGroupDefinition> groups,
        IReadOnlyList<string>? compatibilityTypeIds = null)
    {
        var configuration = BaseMetricConfiguration(title, primaryMetric);
        if (string.Equals(type, DashboardWidgetCatalog.KpiTileType, StringComparison.Ordinal))
        {
            configuration.Remove(DashboardWidgetCatalog.PrimaryMetricKey);
            configuration["subtitle"] = "Total matching events";
            foreach (var (key, value) in DashboardMetricVisualizationCatalog.Find(DashboardMetricVisualizationIds.Value)!.DefaultConfiguration)
            {
                configuration[key] = value;
            }
        }
        else if (string.Equals(type, DashboardWidgetCatalog.RateTileType, StringComparison.Ordinal) ||
                 string.Equals(type, DashboardWidgetCatalog.StatusValueType, StringComparison.Ordinal))
        {
            configuration.Remove(DashboardWidgetCatalog.PrimaryMetricKey);
        }

        return new DashboardWidgetModule(
            new DashboardWidgetDescriptor(type, displayName, category, description, icon, displayName, DashboardWidgetRendererKind.Kpi, DashboardWidgetEditorKind.MetricTile, ["runtimeEvents"]),
            component,
            component,
            configuration,
            groups,
            DefaultStyle(),
            new DashboardWidgetLayoutContract(),
            compatibilityTypeIds);
    }

    private static DashboardWidgetModule EventModule(
        string type,
        string displayName,
        string category,
        string description,
        string icon,
        Type component,
        string title,
        IReadOnlyList<DashboardWidgetPropertyGroupDefinition> groups,
        IReadOnlyList<string>? dataRequirements = null,
        int preferredColumns = 1,
        int preferredRows = 1,
        IReadOnlyList<string>? compatibilityTypeIds = null)
    {
        var configuration = UsesFocusedMetricQuery(type)
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["title"] = title
            }
            : EventConfiguration(title);
        if (string.Equals(type, DashboardWidgetCatalog.EventGaugeType, StringComparison.Ordinal))
        {
            configuration[DashboardWidgetCatalog.PrimaryMetricKey] = DashboardWidgetCatalog.MetricRecent;
            configuration[DashboardWidgetCatalog.GaugeStyleKey] = DashboardWidgetCatalog.GaugeStyleRing;
        }

        return new DashboardWidgetModule(
            new DashboardWidgetDescriptor(type, displayName, category, description, icon, displayName, RendererKind(type), DashboardWidgetEditorKind.Basic, dataRequirements ?? ["runtimeEvents"]),
            component,
            component,
            configuration,
            groups,
            DefaultStyle(),
            new DashboardWidgetLayoutContract(1, 1, preferredColumns, preferredRows),
            compatibilityTypeIds);
    }

    private static bool UsesFocusedMetricQuery(string type)
        => string.Equals(type, DashboardWidgetCatalog.EventCounterType, StringComparison.Ordinal) ||
           string.Equals(type, DashboardWidgetCatalog.EventRateType, StringComparison.Ordinal);

    private static DashboardWidgetModule ChartModule(
        string type,
        string displayName,
        string category,
        string description,
        string icon,
        Type component,
        string title,
        string chartType,
        IReadOnlyList<DashboardWidgetPropertyGroupDefinition> groups,
        IReadOnlyList<string>? compatibilityTypeIds = null)
    {
        var configuration = EventConfiguration(title);
        configuration[DashboardWidgetCatalog.PrimaryMetricKey] = DashboardWidgetCatalog.MetricMessages;
        configuration[DashboardWidgetCatalog.ChartTypeKey] = chartType;
        return new DashboardWidgetModule(
            new DashboardWidgetDescriptor(type, displayName, category, description, icon, displayName, DashboardWidgetRendererKind.Chart, DashboardWidgetEditorKind.Chart, ["runtimeEvents"]),
            component,
            component,
            configuration,
            groups,
            DefaultStyle(),
            new DashboardWidgetLayoutContract(2, 1, 2, 1),
            compatibilityTypeIds);
    }

    private static Dictionary<string, string> EventConfiguration(string title)
    {
        var configuration = new Dictionary<string, string>(DashboardEventFilterCatalog.Shared.CreateEmptyConfiguration(), StringComparer.Ordinal)
        {
            ["title"] = title,
            [DashboardWidgetCatalog.PrimaryMetricKey] = DashboardWidgetCatalog.MetricMessages
        };
        return configuration;
    }

    private static Dictionary<string, string> BaseMetricConfiguration(string title, string primaryMetric)
        => new(StringComparer.Ordinal)
        {
            ["title"] = title,
            [DashboardWidgetCatalog.PrimaryMetricKey] = primaryMetric
        };

    private static DashboardWidgetRendererKind RendererKind(string type)
        => type switch
        {
            DashboardWidgetCatalog.EventRateType => DashboardWidgetRendererKind.Rate,
            DashboardWidgetCatalog.EventGaugeType => DashboardWidgetRendererKind.Gauge,
            DashboardWidgetCatalog.EventTableType => DashboardWidgetRendererKind.EventTable,
            DashboardWidgetCatalog.LatestEventType => DashboardWidgetRendererKind.LatestEvent,
            DashboardWidgetCatalog.TopicActivityType => DashboardWidgetRendererKind.TopicActivity,
            DashboardWidgetCatalog.PayloadDistributionType => DashboardWidgetRendererKind.PayloadDistribution,
            DashboardWidgetCatalog.QosBreakdownType => DashboardWidgetRendererKind.QosBreakdown,
            DashboardWidgetCatalog.RetainBreakdownType => DashboardWidgetRendererKind.RetainBreakdown,
            DashboardWidgetCatalog.DonutChartType => DashboardWidgetRendererKind.Donut,
            _ => DashboardWidgetRendererKind.Kpi
        };

    private static DashboardWidgetStyleDefinition DefaultStyle() => new();

    private static DashboardWidgetPropertyGroupDefinition MetricGroup(string id, string title)
        => new(id, title, [
            new("metric", "Metric", DashboardWidgetPropertyEditorKind.Metric, HelpText: "Named metric used by this widget."),
            new(DashboardWidgetCatalog.PrimaryMetricKey, "Value", DashboardWidgetPropertyEditorKind.Select, Options: MetricOptions())
        ]);

    private static DashboardWidgetPropertyGroupDefinition KpiMetricGroup()
        => new("data", "Data", [
            new("metric", "Metric", DashboardWidgetPropertyEditorKind.Metric, HelpText: "Named scalar metric shown by this KPI.")
        ]);

    private static DashboardWidgetPropertyGroupDefinition VisualizationGroup()
        => new("visualization", "Visualization", [
            new(
                DashboardWidgetCatalog.MetricVisualizationKey,
                "Visualization",
                DashboardWidgetPropertyEditorKind.Select,
                HelpText: "Visual representation used for the metric value.",
                DefaultValue: DashboardMetricVisualizationIds.Value,
                Options: MetricVisualizationOptions())
        ]);

    private static DashboardWidgetPropertyGroupDefinition MetricQueryGroup(string id, string title)
        => new(id, title, [
            new("metric", "Metric query", DashboardWidgetPropertyEditorKind.Metric, HelpText: "Reusable metric query used by this widget.")
        ]);

    private static DashboardWidgetPropertyGroupDefinition EventFilterProperties()
        => new("filters", "Filters", [
            new(DashboardEventFilterCatalog.EventTypeKey, "Event", DashboardWidgetPropertyEditorKind.Select),
            new(DashboardEventFilterCatalog.StatusKey, "Status", DashboardWidgetPropertyEditorKind.Select),
            new(DashboardEventFilterCatalog.TopicStartsWithKey, "Topic starts", DashboardWidgetPropertyEditorKind.TopicFilter),
            new(DashboardEventFilterCatalog.TopicNotStartsWithKey, "Exclude topic", DashboardWidgetPropertyEditorKind.TopicFilter)
        ]);

    private static DashboardWidgetPropertyGroupDefinition WindowGroup()
        => new("window", "Window", [
            new("window", "Window", DashboardWidgetPropertyEditorKind.Select, Options: [
                new("30s", "30 sec"),
                new("60s", "1 min"),
                new("300s", "5 min"),
                new("900s", "15 min")
            ])
        ]);

    private static DashboardWidgetPropertyGroupDefinition FormatGroup(string id = "format", string title = "Format")
        => new(id, title, [
            new("title", "Title", DashboardWidgetPropertyEditorKind.Text),
            new("unit", "Unit", DashboardWidgetPropertyEditorKind.Text),
            new("precision", "Decimals", DashboardWidgetPropertyEditorKind.Number)
        ]);

    private static DashboardWidgetPropertyGroupDefinition ThresholdGroup()
        => new("threshold", "Threshold", [
            new("warning", "Warning", DashboardWidgetPropertyEditorKind.Number),
            new("critical", "Critical", DashboardWidgetPropertyEditorKind.Number)
        ], StartCollapsed: true);

    private static DashboardWidgetPropertyGroupDefinition GaugeGroup()
        => new("gauge", "Gauge", [
            new(DashboardWidgetCatalog.GaugeStyleKey, "Shape", DashboardWidgetPropertyEditorKind.Segmented, Options: [
                new(DashboardWidgetCatalog.GaugeStyleRing, "Ring"),
                new(DashboardWidgetCatalog.GaugeStyleMeter, "Meter")
            ]),
            new("min", "Min", DashboardWidgetPropertyEditorKind.Number),
            new("max", "Max", DashboardWidgetPropertyEditorKind.Number),
            new("target", "Target", DashboardWidgetPropertyEditorKind.Number)
        ]);

    private static DashboardWidgetPropertyGroupDefinition AxisGroup()
        => new("axis", "Axis", [
            new("showGrid", "Grid", DashboardWidgetPropertyEditorKind.Toggle),
            new("showLabels", "Labels", DashboardWidgetPropertyEditorKind.Toggle)
        ]);

    private static DashboardWidgetPropertyGroupDefinition ChartLineGroup()
        => new("line", "Line", [
            new("lineColor", "Line", DashboardWidgetPropertyEditorKind.Color),
            new("showPoints", "Points", DashboardWidgetPropertyEditorKind.Toggle)
        ]);

    private static DashboardWidgetPropertyGroupDefinition ChartFillGroup()
        => new("fill", "Fill", [
            new("fillColor", "Fill", DashboardWidgetPropertyEditorKind.Color),
            new("fillOpacity", "Opacity", DashboardWidgetPropertyEditorKind.Number)
        ]);

    private static DashboardWidgetPropertyGroupDefinition BarGroup()
        => new("bars", "Bars", [
            new("barColor", "Bar", DashboardWidgetPropertyEditorKind.Color),
            new("orientation", "Orientation", DashboardWidgetPropertyEditorKind.Select, Options: [
                new("vertical", "Vertical"),
                new("horizontal", "Horizontal")
            ])
        ]);

    private static DashboardWidgetPropertyGroupDefinition CategoryGroup(string id = "categories", string title = "Categories")
        => new(id, title, [
            new("groupBy", "Group by", DashboardWidgetPropertyEditorKind.Select),
            new("limit", "Limit", DashboardWidgetPropertyEditorKind.Number),
            new("palette", "Palette", DashboardWidgetPropertyEditorKind.Select)
        ]);

    private static DashboardWidgetPropertyGroupDefinition TableGroup()
        => new("table", "Table", [
            new("rowCount", "Rows", DashboardWidgetPropertyEditorKind.Number),
            new("density", "Density", DashboardWidgetPropertyEditorKind.Select),
            new("payloadPreview", "Payload", DashboardWidgetPropertyEditorKind.Toggle)
        ]);

    private static DashboardWidgetPropertyGroupDefinition FieldGroup(string id, string title)
        => new(id, title, [
            new("showTopic", "Topic", DashboardWidgetPropertyEditorKind.Toggle),
            new("showStatus", "Status", DashboardWidgetPropertyEditorKind.Toggle),
            new("showPayload", "Payload", DashboardWidgetPropertyEditorKind.Toggle),
            new("timestampFormat", "Time", DashboardWidgetPropertyEditorKind.Select)
        ]);

    private static DashboardWidgetPropertyGroupDefinition BucketGroup()
        => new("buckets", "Buckets", [
            new("bucketMode", "Mode", DashboardWidgetPropertyEditorKind.Select),
            new("bucketCount", "Count", DashboardWidgetPropertyEditorKind.Number),
            new("unit", "Unit", DashboardWidgetPropertyEditorKind.Select)
        ]);

    private static DashboardWidgetPropertyGroupDefinition BreakdownGroup()
        => new("breakdown", "Breakdown", [
            new("displayMode", "Mode", DashboardWidgetPropertyEditorKind.Select),
            new("showLegend", "Legend", DashboardWidgetPropertyEditorKind.Toggle),
            new("palette", "Palette", DashboardWidgetPropertyEditorKind.Select)
        ]);

    private static DashboardWidgetPropertyGroupDefinition TopicTreeGroup()
        => new("topic-tree", "Topic tree", [
            new("title", "Title", DashboardWidgetPropertyEditorKind.Text),
            new(DashboardWidgetCatalog.ExcludeSystemTopicsKey, "System topics", DashboardWidgetPropertyEditorKind.Toggle),
            new("depth", "Depth", DashboardWidgetPropertyEditorKind.Number),
            new("badges", "Badges", DashboardWidgetPropertyEditorKind.Select)
        ]);

    private static IReadOnlyList<DashboardWidgetPropertyOption> MetricOptions()
        => [.. DashboardWidgetCatalog.MetricOptions.Select(static metric => new DashboardWidgetPropertyOption(metric.Id, metric.Label))];

    private static IReadOnlyList<DashboardWidgetPropertyOption> MetricVisualizationOptions()
        => [.. DashboardMetricVisualizationCatalog.CreateModules().Select(static visual => new DashboardWidgetPropertyOption(visual.Id, visual.DisplayName))];
}
