using FluxMq.UI.Models;

namespace FluxMq.UI.Services;

public static class DashboardWidgetCatalog
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
    public const string GaugeMinKey = "gauge.min";
    public const string GaugeMaxKey = "gauge.max";
    public const string GaugeTargetKey = "gauge.target";
    public const string GaugeWarningKey = "gauge.warning";
    public const string GaugeCriticalKey = "gauge.critical";
    public const string GaugeNormalColorKey = "gauge.normalColor";
    public const string GaugeWarningColorKey = "gauge.warningColor";
    public const string GaugeCriticalColorKey = "gauge.criticalColor";
    public const string ChartTypeKey = "chartType";
    public const string MetricVisualizationKey = "visualization";
    public const string KpiTitleColorKey = "kpi.titleColor";
    public const string KpiSubtitleColorKey = "kpi.subtitleColor";
    public const string KpiValueColorKey = "kpi.valueColor";
    public const string KpiTitleAlignKey = "kpi.titleAlign";
    public const string KpiValueAlignKey = "kpi.valueAlign";
    public const string KpiValuePlacementKey = "kpi.valuePlacement";
    public const string MetricMessages = "messages";
    public const string MetricRecent = "recent";
    public const string MetricCurrentRate = "currentRate";
    public const string MetricTopics = "topics";
    public const string MetricPayloadBytes = "payloadBytes";
    public const string MetricRetained = "retained";
    public const string MetricAveragePayload = "averagePayload";
    public const string GaugeStyleRing = "ring";
    public const string GaugeStyleMeter = "meter";
    public const string GaugeDefaultMin = "0";
    public const string GaugeDefaultMax = "100";
    public const string GaugeDefaultTarget = "80";
    public const string GaugeDefaultWarning = "70";
    public const string GaugeDefaultCritical = "90";
    public const string GaugeDefaultNormalColor = "#2ed3c6";
    public const string GaugeDefaultWarningColor = "#f4b642";
    public const string GaugeDefaultCriticalColor = "#ff5f6d";
    public const string ChartTypeBars = "bars";
    public const string ChartTypeLine = "line";
    public const string ChartTypeArea = "area";
    public const string ChartTypeTopics = "topics";
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

    public static string NormalizeMetricVisualization(string? value)
    {
        var normalized = NormalizeMetric(value);
        return DashboardMetricVisualizationCatalog.Find(normalized)?.Id ??
            DashboardMetricVisualizationIds.Value;
    }

    private static string NormalizeMetric(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}

public sealed record DashboardMetricDescriptor(string Id, string Label);
