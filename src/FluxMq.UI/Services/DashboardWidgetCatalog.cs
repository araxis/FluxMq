using FluxMq.UI.Models;
using MudBlazor;

namespace FluxMq.UI.Services;

public sealed class DashboardWidgetCatalog
{
    public const string EventCounterType = "event.counter";
    public const string LatestEventType = "event.latest";
    public const string EventRateType = "event.rate";
    public const string EventGaugeType = "event.gauge";
    public const string EventChartType = "event.chart";
    public const string TopicTreeType = "topic.tree";
    public const string ExcludeSystemTopicsKey = "excludeSystemTopics";
    public const string PrimaryMetricKey = "primaryMetric";
    public const string DisplayMetricsKey = "displayMetrics";
    public const string MetricCardColumnsKey = "metricCardColumns";
    public const string GaugeStyleKey = "gaugeStyle";
    public const string ChartTypeKey = "chartType";
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
            EventCounterType,
            "Event Counter",
            "Events",
            "Counts runtime events with optional event type and topic filters.",
            Icons.Material.Filled.Numbers),
        new(
            LatestEventType,
            "Latest Event",
            "Events",
            "Shows the latest runtime event that matches optional filters.",
            Icons.Material.Filled.Bolt),
        new(
            EventRateType,
            "Event Rate",
            "Events",
            "Shows the current event rate for matching runtime events.",
            Icons.Material.Filled.Speed),
        new(
            EventGaugeType,
            "Event Gauge",
            "Events",
            "Shows matching runtime events as a compact activity gauge.",
            Icons.Material.Filled.DonutLarge),
        new(
            EventChartType,
            "Event Chart",
            "Events",
            "Shows matching runtime event activity over the last minute.",
            Icons.Material.Filled.BarChart),
        new(
            TopicTreeType,
            "Topic Tree",
            "Topics",
            "Shows live MQTT topics as a dashboard tree.",
            Icons.Material.Filled.AccountTree)
    ];

    public IReadOnlyList<DashboardWidgetDescriptor> Widgets => _widgets;

    public DashboardWidgetDescriptor? Find(string type)
        => _widgets.FirstOrDefault(widget => string.Equals(widget.Type, type, StringComparison.Ordinal));

    public static bool IsEventWidget(string type)
        => string.Equals(type, EventCounterType, StringComparison.Ordinal) ||
           string.Equals(type, LatestEventType, StringComparison.Ordinal) ||
           string.Equals(type, EventRateType, StringComparison.Ordinal) ||
           string.Equals(type, EventGaugeType, StringComparison.Ordinal) ||
           string.Equals(type, EventChartType, StringComparison.Ordinal);

    public static bool IsTopicTreeWidget(string type)
        => string.Equals(type, TopicTreeType, StringComparison.Ordinal);

    public static bool IsVisualEventWidget(string type)
        => string.Equals(type, EventGaugeType, StringComparison.Ordinal) ||
           string.Equals(type, EventChartType, StringComparison.Ordinal);

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

    private static string NormalizeMetric(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}

public sealed record DashboardMetricDescriptor(string Id, string Label);
