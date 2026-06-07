using FluxMq.UI.Models;
using MudBlazor;

namespace FluxMq.UI.Services;

public sealed class DashboardWidgetCatalog
{
    public const string EventCounterType = "event.counter";
    public const string KpiTileType = "kpi.tile";
    public const string StatusStripType = "status.strip";
    public const string StatusValueType = "status.value";
    public const string RateTileType = "rate.tile";
    public const string LatestEventType = "event.latest";
    public const string EventRateType = "event.rate";
    public const string EventGaugeType = "event.gauge";
    public const string EventChartType = "event.chart";
    public const string LineChartType = "chart.line";
    public const string AreaChartType = "chart.area";
    public const string BarChartType = "chart.bar";
    public const string DonutChartType = "chart.donut";
    public const string EventTableType = "event.table";
    public const string TopicActivityType = "topic.activity";
    public const string PayloadDistributionType = "payload.size.distribution";
    public const string QosRetainBreakdownType = "qos.retain.breakdown";
    public const string QosBreakdownType = "qos.breakdown";
    public const string RetainBreakdownType = "retain.breakdown";
    public const string TopicTreeType = "topic.tree";
    public const string ExcludeSystemTopicsKey = "excludeSystemTopics";
    public const string PrimaryMetricKey = "primaryMetric";
    public const string DisplayMetricsKey = "displayMetrics";
    public const string MetricCardColumnsKey = "metricCardColumns";
    public const string GaugeStyleKey = "gaugeStyle";
    public const string ChartTypeKey = "chartType";
    public const string MetricVisualizationKey = "visualization";
    public const string KpiTitleColorKey = "kpi.titleColor";
    public const string KpiSubtitleColorKey = "kpi.subtitleColor";
    public const string KpiValueColorKey = "kpi.valueColor";
    public const string KpiTitleAlignKey = "kpi.titleAlign";
    public const string KpiValueAlignKey = "kpi.valueAlign";
    public const string KpiValuePlacementKey = "kpi.valuePlacement";
    public const string MetricValueTitleKey = "metric.value.title";
    public const string MetricValueSubtitleKey = "metric.value.subtitle";
    public const string MetricValueShowTitleKey = "metric.value.showTitle";
    public const string MetricValueShowSubtitleKey = "metric.value.showSubtitle";
    public const string MetricValueShowUnitKey = "metric.value.showUnit";
    public const string MetricValueUnitTextKey = "metric.value.unitText";
    public const string MetricValueTitleColorKey = "metric.value.titleColor";
    public const string MetricValueSubtitleColorKey = "metric.value.subtitleColor";
    public const string MetricValueValueColorKey = "metric.value.valueColor";
    public const string MetricValueUnitColorKey = "metric.value.unitColor";
    public const string MetricValueTitleAlignKey = "metric.value.titleAlign";
    public const string MetricValueValueAlignKey = "metric.value.valueAlign";
    public const string MetricValueValuePlacementKey = "metric.value.valuePlacement";
    public const string MetricDigitalLabelKey = "metric.digital.label";
    public const string MetricDigitalShowLabelKey = "metric.digital.showLabel";
    public const string MetricDigitalLabelPlacementKey = "metric.digital.labelPlacement";
    public const string MetricDigitalStyleKey = "metric.digital.style";
    public const string MetricDigitalGlowKey = "metric.digital.glow";
    public const string MetricDigitalBackgroundColorKey = "metric.digital.backgroundColor";
    public const string MetricDigitalSegmentColorKey = "metric.digital.segmentColor";
    public const string MetricDigitalInactiveSegmentColorKey = "metric.digital.inactiveSegmentColor";
    public const string MetricDigitalLabelColorKey = "metric.digital.labelColor";
    public const string MetricDigitalDigitsKey = "metric.digital.digits";
    public const string MetricDigitalBorderColorKey = "metric.digital.borderColor";
    public const string MetricDigitalBorderWidthKey = "metric.digital.borderWidth";
    public const string MetricDigitalRadiusKey = "metric.digital.radius";
    public const string MetricDigitalPaddingKey = "metric.digital.padding";
    public const string MetricDigitalFitModeKey = "metric.digital.fitMode";
    public const string MetricMessages = "messages";
    public const string MetricRecent = "recent";
    public const string MetricCurrentRate = "currentRate";
    public const string MetricTopics = "topics";
    public const string MetricPayloadBytes = "payloadBytes";
    public const string MetricRetained = "retained";
    public const string MetricAveragePayload = "averagePayload";
    public const string GaugeStyleRing = "ring";
    public const string GaugeStyleMeter = "meter";
    public const string GaugeStyleTiles = "tiles";
    public const string ChartTypeBars = "bars";
    public const string ChartTypeLine = "line";
    public const string ChartTypeArea = "area";
    public const string ChartTypeTopics = "topics";
    public const string KpiAlignLeft = "left";
    public const string KpiAlignCenter = "center";
    public const string KpiAlignRight = "right";
    public const string KpiValuePlacementTop = "top";
    public const string KpiValuePlacementMiddle = "middle";
    public const string KpiValuePlacementBottom = "bottom";
    public const string KpiDefaultTitleColor = "#f3f7fb";
    public const string KpiDefaultSubtitleColor = "#9fb0c5";
    public const string KpiDefaultValueColor = "#f3f7fb";
    public const string MetricValueDefaultTitle = "Messages";
    public const string MetricValueDefaultSubtitle = "Total matching events";
    public const string MetricValueDefaultUnitText = "";
    public const string MetricDigitalDefaultBackgroundColor = "#040609";
    public const string MetricDigitalDefaultSegmentColor = "#db8b98";
    public const string MetricDigitalDefaultInactiveSegmentColor = "#351820";
    public const string MetricDigitalDefaultLabelColor = "#7f928b";
    public const string MetricDigitalDefaultBorderColor = "#1d4850";
    public const int MetricDigitalDefaultBorderWidth = 1;
    public const int MetricDigitalDefaultRadius = 7;
    public const int MetricDigitalDefaultPadding = 10;
    public const int MetricDigitalDefaultDigits = 4;
    public const int MetricDigitalMinDigits = 1;
    public const int MetricDigitalMaxDigits = 8;
    public const string MetricDigitalLabelPlacementTop = "top";
    public const string MetricDigitalLabelPlacementBottom = "bottom";
    public const string MetricDigitalLabelPlacementHidden = "hidden";
    public const string MetricDigitalStylePanel = "panel";
    public const string MetricDigitalStyleSegment = "segment";
    public const string MetricDigitalStyleTerminal = "terminal";
    public const string MetricDigitalGlowOff = "off";
    public const string MetricDigitalGlowSoft = "soft";
    public const string MetricDigitalGlowStrong = "strong";
    public const string MetricDigitalFitCompact = "compact";
    public const string MetricDigitalFitFill = "fill";

    public const int DefaultMetricCardColumns = 4;
    public const int MinMetricCardColumns = 1;
    public const int MaxMetricCardColumns = 4;

    private static readonly IReadOnlyList<string> DefaultDisplayMetrics =
    [
        MetricMessages,
        MetricRecent,
        MetricCurrentRate,
        MetricPayloadBytes
    ];

    private static readonly HashSet<string> KnownDisplayMetrics = new(StringComparer.Ordinal)
    {
        MetricMessages,
        MetricRecent,
        MetricCurrentRate,
        MetricTopics,
        MetricPayloadBytes,
        MetricRetained,
        MetricAveragePayload
    };

    public static IReadOnlyList<DashboardMetricDescriptor> MetricOptions { get; } =
    [
        new(MetricMessages, "Messages"),
        new(MetricRecent, "Recent"),
        new(MetricCurrentRate, "Now / sec"),
        new(MetricPayloadBytes, "Payload"),
        new(MetricTopics, "Topics"),
        new(MetricRetained, "Retained"),
        new(MetricAveragePayload, "Avg payload")
    ];

    private readonly IReadOnlyList<DashboardWidgetDescriptor> _widgets =
    [
        new(
            KpiTileType,
            "KPI Tile",
            "Metrics",
            "Shows one named operational metric with compact comparison context.",
            Icons.Material.Filled.Speed,
            "Single metric tile",
            DashboardWidgetRendererKind.Kpi,
            DashboardWidgetEditorKind.MetricTile,
            ["runtimeEvents"]),
        new(
            StatusValueType,
            "Status Value",
            "Metrics",
            "Shows one operational status or selected metric value.",
            Icons.Material.Filled.Verified,
            "Single status value",
            DashboardWidgetRendererKind.StatusValue,
            DashboardWidgetEditorKind.MetricTile,
            ["runtimeEvents"]),
        new(
            RateTileType,
            "Rate Tile",
            "Metrics",
            "Shows current message throughput for selected runtime events.",
            Icons.Material.Filled.QueryStats,
            "Live rate",
            DashboardWidgetRendererKind.Rate,
            DashboardWidgetEditorKind.MetricTile,
            ["runtimeEvents"]),
        new(
            EventCounterType,
            "Event Counter",
            "Events",
            "Counts runtime events with optional event type and topic filters.",
            Icons.Material.Filled.Numbers,
            "Total events",
            DashboardWidgetRendererKind.Kpi,
            DashboardWidgetEditorKind.MetricTile,
            ["runtimeEvents"]),
        new(
            LatestEventType,
            "Latest Event",
            "Events",
            "Shows the latest runtime event that matches optional filters.",
            Icons.Material.Filled.Bolt,
            "Latest match",
            DashboardWidgetRendererKind.LatestEvent,
            DashboardWidgetEditorKind.Basic,
            ["runtimeEvents"]),
        new(
            EventRateType,
            "Event Rate",
            "Events",
            "Shows the current event rate for matching runtime events.",
            Icons.Material.Filled.Speed,
            "Events per second",
            DashboardWidgetRendererKind.Rate,
            DashboardWidgetEditorKind.MetricTile,
            ["runtimeEvents"]),
        new(
            EventGaugeType,
            "Event Gauge",
            "Events",
            "Shows matching runtime events as a compact activity gauge.",
            Icons.Material.Filled.DonutLarge,
            "Activity gauge",
            DashboardWidgetRendererKind.Gauge,
            DashboardWidgetEditorKind.Gauge,
            ["runtimeEvents"]),
        new(
            LineChartType,
            "Line Chart",
            "Charts",
            "Shows runtime activity as a line trend.",
            Icons.Material.Filled.StackedLineChart,
            "Line trend",
            DashboardWidgetRendererKind.Chart,
            DashboardWidgetEditorKind.Chart,
            ["runtimeEvents"]),
        new(
            AreaChartType,
            "Area Chart",
            "Charts",
            "Shows runtime activity as a filled trend.",
            Icons.Material.Filled.AreaChart,
            "Area trend",
            DashboardWidgetRendererKind.Chart,
            DashboardWidgetEditorKind.Chart,
            ["runtimeEvents"]),
        new(
            BarChartType,
            "Bar Chart",
            "Charts",
            "Shows runtime activity as time buckets.",
            Icons.Material.Filled.BarChart,
            "Bucket bars",
            DashboardWidgetRendererKind.Chart,
            DashboardWidgetEditorKind.Chart,
            ["runtimeEvents"]),
        new(
            DonutChartType,
            "Donut Chart",
            "Charts",
            "Shows categorical operational breakdowns.",
            Icons.Material.Filled.DonutLarge,
            "Breakdown",
            DashboardWidgetRendererKind.Donut,
            DashboardWidgetEditorKind.Breakdown,
            ["runtimeEvents"]),
        new(
            EventTableType,
            "Event Table",
            "Events",
            "Lists recent matching runtime events with payload previews.",
            Icons.Material.Filled.TableRows,
            "Recent rows",
            DashboardWidgetRendererKind.EventTable,
            DashboardWidgetEditorKind.Table,
            ["runtimeEvents"]),
        new(
            TopicActivityType,
            "Topic Activity",
            "Topics",
            "Shows matching runtime event volume as a topic heatmap.",
            Icons.Material.Filled.GridOn,
            "Topic heatmap",
            DashboardWidgetRendererKind.TopicActivity,
            DashboardWidgetEditorKind.Chart,
            ["topicProjection"]),
        new(
            PayloadDistributionType,
            "Payload Size Distribution",
            "MQTT Ops",
            "Shows matching message payload size buckets.",
            Icons.Material.Filled.DataArray,
            "Payload buckets",
            DashboardWidgetRendererKind.PayloadDistribution,
            DashboardWidgetEditorKind.Payload,
            ["runtimeEvents", "payload"]),
        new(
            QosBreakdownType,
            "QoS Breakdown",
            "MQTT Ops",
            "Shows QoS distribution for matching MQTT events.",
            Icons.Material.Filled.PieChart,
            "QoS distribution",
            DashboardWidgetRendererKind.QosBreakdown,
            DashboardWidgetEditorKind.Breakdown,
            ["runtimeEvents", "mqttAttributes"]),
        new(
            RetainBreakdownType,
            "Retain Breakdown",
            "MQTT Ops",
            "Shows retained-message distribution for matching MQTT events.",
            Icons.Material.Filled.PushPin,
            "Retain distribution",
            DashboardWidgetRendererKind.RetainBreakdown,
            DashboardWidgetEditorKind.Breakdown,
            ["runtimeEvents", "mqttAttributes"]),
        new(
            TopicTreeType,
            "Topic Tree",
            "Topics",
            "Shows live MQTT topics as a dashboard tree.",
            Icons.Material.Filled.AccountTree,
            "Topic hierarchy",
            DashboardWidgetRendererKind.TopicTree,
            DashboardWidgetEditorKind.TopicTree,
            ["topicProjection"])
    ];

    private readonly IReadOnlyList<DashboardWidgetDescriptor> _legacyWidgets =
    [
        new(
            StatusStripType,
            "Status Strip",
            "Compatibility",
            "Legacy multi-metric strip. Existing dashboards migrate to focused status values.",
            Icons.Material.Filled.ViewWeek,
            "Legacy metric strip",
            DashboardWidgetRendererKind.StatusStrip,
            DashboardWidgetEditorKind.StatusStrip,
            ["runtimeEvents"]),
        new(
            EventChartType,
            "Event Chart",
            "Compatibility",
            "Legacy generic chart. Existing dashboards migrate to line, area, or bar charts.",
            Icons.Material.Filled.BarChart,
            "Legacy chart",
            DashboardWidgetRendererKind.Chart,
            DashboardWidgetEditorKind.Chart,
            ["runtimeEvents"]),
        new(
            QosRetainBreakdownType,
            "QoS / Retain Breakdown",
            "Compatibility",
            "Legacy combined breakdown. Existing dashboards migrate to focused QoS and retain widgets.",
            Icons.Material.Filled.PieChart,
            "Legacy breakdown",
            DashboardWidgetRendererKind.QosRetainBreakdown,
            DashboardWidgetEditorKind.Breakdown,
            ["runtimeEvents", "mqttAttributes"])
    ];

    public IReadOnlyList<DashboardWidgetDescriptor> Widgets => _widgets;

    public DashboardWidgetDescriptor? Find(string type)
        => _widgets
            .Concat(_legacyWidgets)
            .FirstOrDefault(widget => string.Equals(widget.Type, type, StringComparison.Ordinal));

    public static bool IsEventWidget(string type)
        => string.Equals(type, EventCounterType, StringComparison.Ordinal) ||
           string.Equals(type, KpiTileType, StringComparison.Ordinal) ||
           string.Equals(type, StatusStripType, StringComparison.Ordinal) ||
           string.Equals(type, StatusValueType, StringComparison.Ordinal) ||
           string.Equals(type, RateTileType, StringComparison.Ordinal) ||
           string.Equals(type, LatestEventType, StringComparison.Ordinal) ||
           string.Equals(type, EventRateType, StringComparison.Ordinal) ||
           string.Equals(type, EventGaugeType, StringComparison.Ordinal) ||
           string.Equals(type, EventChartType, StringComparison.Ordinal) ||
           string.Equals(type, LineChartType, StringComparison.Ordinal) ||
           string.Equals(type, AreaChartType, StringComparison.Ordinal) ||
           string.Equals(type, BarChartType, StringComparison.Ordinal) ||
           string.Equals(type, DonutChartType, StringComparison.Ordinal) ||
           string.Equals(type, EventTableType, StringComparison.Ordinal) ||
           string.Equals(type, TopicActivityType, StringComparison.Ordinal) ||
           string.Equals(type, PayloadDistributionType, StringComparison.Ordinal) ||
           string.Equals(type, QosRetainBreakdownType, StringComparison.Ordinal) ||
           string.Equals(type, QosBreakdownType, StringComparison.Ordinal) ||
           string.Equals(type, RetainBreakdownType, StringComparison.Ordinal);

    public static bool IsTopicTreeWidget(string type)
        => string.Equals(type, TopicTreeType, StringComparison.Ordinal);

    public static bool IsVisualEventWidget(string type)
        => string.Equals(type, EventGaugeType, StringComparison.Ordinal) ||
           string.Equals(type, EventChartType, StringComparison.Ordinal) ||
           string.Equals(type, StatusStripType, StringComparison.Ordinal) ||
           string.Equals(type, StatusValueType, StringComparison.Ordinal) ||
           string.Equals(type, KpiTileType, StringComparison.Ordinal) ||
           string.Equals(type, RateTileType, StringComparison.Ordinal) ||
           string.Equals(type, LineChartType, StringComparison.Ordinal) ||
           string.Equals(type, AreaChartType, StringComparison.Ordinal) ||
           string.Equals(type, BarChartType, StringComparison.Ordinal) ||
           string.Equals(type, DonutChartType, StringComparison.Ordinal) ||
           string.Equals(type, PayloadDistributionType, StringComparison.Ordinal) ||
           string.Equals(type, QosRetainBreakdownType, StringComparison.Ordinal) ||
           string.Equals(type, QosBreakdownType, StringComparison.Ordinal) ||
           string.Equals(type, RetainBreakdownType, StringComparison.Ordinal);

    public static bool IsChartWidget(string type)
        => string.Equals(type, EventChartType, StringComparison.Ordinal) ||
           string.Equals(type, LineChartType, StringComparison.Ordinal) ||
           string.Equals(type, AreaChartType, StringComparison.Ordinal) ||
           string.Equals(type, BarChartType, StringComparison.Ordinal) ||
           string.Equals(type, TopicActivityType, StringComparison.Ordinal) ||
           string.Equals(type, PayloadDistributionType, StringComparison.Ordinal);

    public static bool IsBreakdownWidget(string type)
        => string.Equals(type, DonutChartType, StringComparison.Ordinal) ||
           string.Equals(type, QosRetainBreakdownType, StringComparison.Ordinal) ||
           string.Equals(type, QosBreakdownType, StringComparison.Ordinal) ||
           string.Equals(type, RetainBreakdownType, StringComparison.Ordinal);

    public static string NormalizeWidgetTypeForAdd(string? type)
        => string.IsNullOrWhiteSpace(type)
            ? EventCounterType
            : type.Trim() switch
            {
                StatusStripType => StatusValueType,
                EventChartType => BarChartType,
                QosRetainBreakdownType => QosBreakdownType,
                var value => value
            };

    public static IReadOnlyList<string> NormalizeDisplayMetrics(string? metrics)
        => NormalizeDisplayMetrics((metrics ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    public static IReadOnlyList<string> NormalizeDisplayMetrics(IEnumerable<string>? metrics)
    {
        if (metrics is null)
        {
            return DefaultDisplayMetrics;
        }

        var result = new List<string>();
        foreach (var metric in metrics)
        {
            var normalized = NormalizeMetric(metric);
            if (!string.IsNullOrWhiteSpace(normalized) &&
                KnownDisplayMetrics.Contains(normalized) &&
                !result.Contains(normalized, StringComparer.Ordinal))
            {
                result.Add(normalized);
            }
        }

        return result.Count == 0 ? DefaultDisplayMetrics : result;
    }

    public static string BuildDisplayMetrics(IEnumerable<string>? metrics)
        => string.Join(",", NormalizeDisplayMetrics(metrics));

    public static int NormalizeMetricCardColumns(string? value)
    {
        if (!int.TryParse(value, out var columns))
        {
            columns = DefaultMetricCardColumns;
        }

        return NormalizeMetricCardColumns(columns);
    }

    public static int NormalizeMetricCardColumns(int columns)
        => Math.Clamp(columns, MinMetricCardColumns, MaxMetricCardColumns);

    public static string NormalizePrimaryMetric(string? value)
    {
        var metric = NormalizeMetric(value);
        return KnownDisplayMetrics.Contains(metric) ? metric : MetricRecent;
    }

    public static string NormalizeGaugeStyle(string? value)
        => value switch
        {
            GaugeStyleMeter => GaugeStyleMeter,
            GaugeStyleTiles => GaugeStyleTiles,
            _ => GaugeStyleRing
        };

    public static string NormalizeChartType(string? value)
        => value switch
        {
            ChartTypeLine => ChartTypeLine,
            ChartTypeArea => ChartTypeArea,
            ChartTypeTopics => ChartTypeTopics,
            _ => ChartTypeBars
        };

    public static string NormalizeKpiHorizontalAlignment(string? value)
        => value switch
        {
            KpiAlignCenter => KpiAlignCenter,
            KpiAlignRight => KpiAlignRight,
            _ => KpiAlignLeft
        };

    public static string NormalizeKpiValuePlacement(string? value)
        => value switch
        {
            KpiValuePlacementMiddle => KpiValuePlacementMiddle,
            KpiValuePlacementBottom => KpiValuePlacementBottom,
            _ => KpiValuePlacementTop
        };

    public static string NormalizeMetricVisualization(string? value)
    {
        var normalized = NormalizeMetric(value);
        return DashboardMetricVisualizationCatalog.Find(normalized)?.Id ??
            DashboardMetricVisualizationIds.Value;
    }

    public static string NormalizeMetricDigitalStyle(string? value)
        => value switch
        {
            MetricDigitalStyleSegment => MetricDigitalStyleSegment,
            MetricDigitalStyleTerminal => MetricDigitalStyleTerminal,
            _ => MetricDigitalStylePanel
        };

    public static string NormalizeMetricDigitalGlow(string? value)
        => value switch
        {
            MetricDigitalGlowOff => MetricDigitalGlowOff,
            MetricDigitalGlowStrong => MetricDigitalGlowStrong,
            _ => MetricDigitalGlowSoft
        };

    public static string NormalizeMetricDigitalLabelPlacement(string? value)
        => value switch
        {
            MetricDigitalLabelPlacementTop => MetricDigitalLabelPlacementTop,
            MetricDigitalLabelPlacementHidden => MetricDigitalLabelPlacementHidden,
            _ => MetricDigitalLabelPlacementBottom
        };

    public static string NormalizeMetricDigitalFitMode(string? value)
        => value switch
        {
            MetricDigitalFitFill => MetricDigitalFitFill,
            _ => MetricDigitalFitCompact
        };

    public static int NormalizeMetricDigitalDigits(string? value)
        => int.TryParse(value, out var digits)
            ? NormalizeMetricDigitalDigits(digits)
            : MetricDigitalDefaultDigits;

    public static int NormalizeMetricDigitalDigits(int value)
        => Math.Clamp(value, MetricDigitalMinDigits, MetricDigitalMaxDigits);

    private static string NormalizeMetric(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}

public sealed record DashboardMetricDescriptor(string Id, string Label);
