using FluxFlow.Engine.Components;
using FluxMq.Core.Models;
using FluxMq.UI.Models;
using FluxMq.UI.Services;
using MudBlazor;
using System.Globalization;

namespace FluxMq.UI.Components.Workspace;

public static class DashboardWidgetFormatting
{
    public static string WidgetTitle(DashboardWidgetSnapshot widget)
        => widget.ReadString("title") ?? widget.Type switch
        {
            DashboardWidgetCatalog.KpiTileType => "KPI tile",
            DashboardWidgetCatalog.StatusStripType => "Status strip",
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
            DashboardWidgetCatalog.TopicTreeType => "Topic tree",
            _ => widget.Name
        };

    public static string WidgetSubtitle(DashboardWidgetSnapshot widget)
        => DashboardWidgetCatalog.IsTopicTreeWidget(widget.Type)
            ? "Live topic map"
            : EventFilterSummary(widget);

    public static string EventFilterSummary(DashboardWidgetSnapshot widget)
    {
        var parts = new List<string>();
        AddPart(parts, widget.ReadString(DashboardEventFilterCatalog.EventTypeKey));
        AddPart(parts, widget.ReadString(DashboardEventFilterCatalog.TopicStartsWithKey), "topic ");
        AddPart(parts, widget.ReadString(DashboardEventFilterCatalog.TopicNotStartsWithKey), "exclude ");
        AddPart(parts, widget.ReadString(DashboardEventFilterCatalog.StatusKey), "status ");
        return parts.Count == 0 ? "All runtime events" : string.Join(" / ", parts);
    }

    public static string WidgetIcon(DashboardWidgetSnapshot widget)
        => widget.Type switch
        {
            DashboardWidgetCatalog.KpiTileType => Icons.Material.Filled.Speed,
            DashboardWidgetCatalog.StatusStripType => Icons.Material.Filled.ViewWeek,
            DashboardWidgetCatalog.RateTileType => Icons.Material.Filled.QueryStats,
            DashboardWidgetCatalog.EventCounterType => Icons.Material.Filled.Numbers,
            DashboardWidgetCatalog.LatestEventType => Icons.Material.Filled.Bolt,
            DashboardWidgetCatalog.EventRateType => Icons.Material.Filled.Speed,
            DashboardWidgetCatalog.EventGaugeType => Icons.Material.Filled.DonutLarge,
            DashboardWidgetCatalog.EventChartType => Icons.Material.Filled.StackedLineChart,
            DashboardWidgetCatalog.LineChartType => Icons.Material.Filled.StackedLineChart,
            DashboardWidgetCatalog.AreaChartType => Icons.Material.Filled.AreaChart,
            DashboardWidgetCatalog.BarChartType => Icons.Material.Filled.BarChart,
            DashboardWidgetCatalog.DonutChartType => Icons.Material.Filled.DonutLarge,
            DashboardWidgetCatalog.EventTableType => Icons.Material.Filled.TableRows,
            DashboardWidgetCatalog.TopicActivityType => Icons.Material.Filled.GridOn,
            DashboardWidgetCatalog.PayloadDistributionType => Icons.Material.Filled.DataArray,
            DashboardWidgetCatalog.QosRetainBreakdownType => Icons.Material.Filled.PieChart,
            DashboardWidgetCatalog.TopicTreeType => Icons.Material.Filled.AccountTree,
            _ => Icons.Material.Filled.Widgets
        };

    public static string WidgetClass(DashboardWidgetSnapshot widget)
        => widget.Type switch
        {
            DashboardWidgetCatalog.KpiTileType => "kpi-tile",
            DashboardWidgetCatalog.StatusStripType => "status-strip",
            DashboardWidgetCatalog.RateTileType => "rate-tile",
            DashboardWidgetCatalog.EventCounterType => "event-counter",
            DashboardWidgetCatalog.LatestEventType => "latest-event",
            DashboardWidgetCatalog.EventRateType => "event-rate",
            DashboardWidgetCatalog.EventGaugeType => "event-gauge",
            DashboardWidgetCatalog.EventChartType => "event-chart",
            DashboardWidgetCatalog.LineChartType => "event-chart line-chart",
            DashboardWidgetCatalog.AreaChartType => "event-chart area-chart",
            DashboardWidgetCatalog.BarChartType => "event-chart bar-chart",
            DashboardWidgetCatalog.DonutChartType => "donut-chart",
            DashboardWidgetCatalog.EventTableType => "event-table",
            DashboardWidgetCatalog.TopicActivityType => "topic-activity",
            DashboardWidgetCatalog.PayloadDistributionType => "payload-distribution",
            DashboardWidgetCatalog.QosRetainBreakdownType => "qos-retain-breakdown",
            DashboardWidgetCatalog.TopicTreeType => "topic-tree-widget",
            _ => "unknown"
        };

    public static string MetricGridStyle(DashboardWidgetSnapshot widget)
        => $"--metric-columns:{DashboardWidgetCatalog.NormalizeMetricCardColumns(widget.ReadString(DashboardWidgetCatalog.MetricCardColumnsKey))};";

    public static IReadOnlyList<DashboardMetricDisplayCard> MetricCards(
        DashboardWidgetSnapshot widget,
        DashboardEventSnapshot snapshot)
        => DashboardWidgetCatalog.NormalizeDisplayMetrics(widget.ReadString(DashboardWidgetCatalog.DisplayMetricsKey))
            .Select(metric => ToMetricDisplayCard(metric, snapshot))
            .ToArray();

    public static DashboardMetricDisplayCard PrimaryMetricCard(
        DashboardWidgetSnapshot widget,
        DashboardEventSnapshot snapshot)
        => ToMetricDisplayCard(
            DashboardWidgetCatalog.NormalizePrimaryMetric(widget.ReadString(DashboardWidgetCatalog.PrimaryMetricKey)),
            snapshot);

    public static DashboardMetricDisplayCard ToMetricDisplayCard(string metric, DashboardEventSnapshot snapshot)
        => metric switch
        {
            DashboardWidgetCatalog.MetricCurrentRate => new(
                Icons.Material.Filled.Speed,
                FormatRate(snapshot.EventsPerSecond),
                "now / sec",
                "metric-rate"),
            DashboardWidgetCatalog.MetricRecent => new(
                Icons.Material.Filled.Update,
                FormatNumber(snapshot.RecentCount),
                "recent",
                "metric-recent"),
            DashboardWidgetCatalog.MetricPayloadBytes => new(
                Icons.Material.Filled.DataUsage,
                FormatBytes(snapshot.TotalPayloadBytes),
                "payload",
                "metric-payload"),
            DashboardWidgetCatalog.MetricTopics => new(
                Icons.Material.Filled.Topic,
                FormatNumber(snapshot.UniqueTopicCount),
                "topics",
                "metric-topics"),
            DashboardWidgetCatalog.MetricRetained => new(
                Icons.Material.Filled.PushPin,
                FormatNumber(snapshot.RetainedCount),
                "retained",
                "metric-retained"),
            DashboardWidgetCatalog.MetricAveragePayload => new(
                Icons.Material.Filled.DataUsage,
                FormatBytes(snapshot.AveragePayloadBytes),
                "avg payload",
                "metric-payload"),
            _ => new(
                Icons.Material.Filled.MailOutline,
                FormatNumber(snapshot.Count),
                "messages",
                "metric-messages")
        };

    public static IReadOnlyList<DashboardChartBucket> ChartBuckets(DashboardEventSnapshot snapshot)
    {
        var max = Math.Max(1, snapshot.BucketCounts.DefaultIfEmpty(0).Max());
        return snapshot.BucketCounts
            .Select(count =>
            {
                var height = count == 0 ? 8 : Math.Clamp((count / (double)max) * 100, 14, 100);
                return new DashboardChartBucket(
                    $"height:{height.ToString("0.###", CultureInfo.InvariantCulture)}%;",
                    $"{FormatNumber(count)} events");
            })
            .ToArray();
    }

    public static IReadOnlyList<DashboardTopicBar> TopicBars(DashboardEventSnapshot snapshot, int limit)
    {
        var max = Math.Max(
            1,
            snapshot.TopicCounts
                .DefaultIfEmpty(new DashboardTopicMetric(string.Empty, 0))
                .Max(static topic => topic.Count));
        return snapshot.TopicCounts
            .Take(limit)
            .Select(topic =>
            {
                var width = topic.Count == 0 ? 0 : Math.Clamp(topic.Count / (double)max * 100, 10, 100);
                return new DashboardTopicBar(
                    topic.Topic,
                    topic.Count,
                    $"width:{width.ToString("0.###", CultureInfo.InvariantCulture)}%;");
            })
            .ToArray();
    }

    public static string ChartLinePoints(DashboardEventSnapshot snapshot)
    {
        var buckets = snapshot.BucketCounts.Count == 0 ? [0] : snapshot.BucketCounts;
        var max = Math.Max(1, buckets.Max());
        var lastIndex = Math.Max(1, buckets.Count - 1);
        return string.Join(" ", buckets.Select((count, index) =>
        {
            var x = index * 100d / lastIndex;
            var y = count == 0 ? 41d : 42d - Math.Clamp(count / (double)max * 36d, 8d, 36d);
            return $"{x.ToString("0.###", CultureInfo.InvariantCulture)},{y.ToString("0.###", CultureInfo.InvariantCulture)}";
        }));
    }

    public static string ChartAreaPoints(DashboardEventSnapshot snapshot)
        => $"0,44 {ChartLinePoints(snapshot)} 100,44";

    public static string GaugeProgressStyle(DashboardEventSnapshot snapshot)
    {
        var maxBucket = Math.Max(1, snapshot.BucketCounts.DefaultIfEmpty(0).Max());
        var capacity = Math.Max(1, maxBucket * Math.Max(1, snapshot.BucketCounts.Count));
        var percent = snapshot.RecentCount == 0
            ? 0
            : Math.Clamp(snapshot.RecentCount / (double)capacity * 100, 12, 100);
        return $"--gauge-progress:{percent.ToString("0.###", CultureInfo.InvariantCulture)}%;";
    }

    public static string RateTrackStyle(DashboardEventSnapshot snapshot)
    {
        var percent = snapshot.RecentCount == 0
            ? 0
            : Math.Clamp(snapshot.EventsPerSecond * 320, 8, 100);
        return $"width:{percent.ToString("0.###", CultureInfo.InvariantCulture)}%;";
    }

    public static int TopicCount(IReadOnlyList<MqttEnvelope> messages)
        => messages.Select(static message => message.Topic).Distinct(StringComparer.Ordinal).Count();

    public static string CounterMeta(DashboardEventSnapshot snapshot)
        => snapshot.LatestEvent is null
            ? "waiting"
            : $"latest {FormatEventTime(snapshot.LatestEvent)}";

    public static string RateMeta(DashboardEventSnapshot snapshot)
        => snapshot.RecentCount == 0
            ? "waiting for traffic"
            : $"{FormatNumber(snapshot.RecentCount)} in {FormatDuration(snapshot.RateWindow)}";

    public static string EventMeta(FlowEvent flowEvent)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(flowEvent.Channel))
        {
            parts.Add(flowEvent.Channel);
        }
        else if (!string.IsNullOrWhiteSpace(flowEvent.Subject))
        {
            parts.Add(flowEvent.Subject);
        }

        if (!string.IsNullOrWhiteSpace(flowEvent.Status))
        {
            parts.Add(flowEvent.Status);
        }

        if (flowEvent.PayloadBytes is { } bytes)
        {
            parts.Add($"{bytes} B");
        }

        parts.Add(FormatEventTime(flowEvent));
        return string.Join(" / ", parts);
    }

    public static string FormatEventTime(FlowEvent flowEvent)
        => flowEvent.Timestamp.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    public static string FormatNumber(long value) => value switch
    {
        < 1_000 => value.ToString(CultureInfo.InvariantCulture),
        < 1_000_000 => $"{value / 1_000.0:0.#}k",
        < 1_000_000_000 => $"{value / 1_000_000.0:0.#}M",
        _ => $"{value / 1_000_000_000.0:0.#}B"
    };

    public static string FormatBytes(long bytes) => FormatBytes((double)bytes);

    public static string FormatBytes(double bytes) => bytes switch
    {
        < 1_024 => $"{bytes.ToString("0.#", CultureInfo.InvariantCulture)} B",
        < 1_048_576 => $"{bytes / 1_024.0:0.#} KB",
        < 1_073_741_824 => $"{bytes / 1_048_576.0:0.#} MB",
        _ => $"{bytes / 1_073_741_824.0:0.#} GB"
    };

    public static string FormatRate(double eventsPerSecond)
        => eventsPerSecond.ToString(eventsPerSecond switch
        {
            >= 10 => "0/s",
            >= 1 => "0.0/s",
            _ => "0.00/s"
        }, CultureInfo.InvariantCulture);

    public static string FormatDuration(TimeSpan duration)
        => duration.TotalMinutes >= 1
            ? $"{duration.TotalMinutes.ToString("0", CultureInfo.InvariantCulture)} min"
            : $"{duration.TotalSeconds.ToString("0", CultureInfo.InvariantCulture)} sec";

    public static string TrimTopic(string topic)
        => topic.Length <= 28 ? topic : "..." + topic[^25..];

    private static void AddPart(List<string> parts, string? value, string prefix = "")
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parts.Add($"{prefix}{value}");
        }
    }
}

public sealed record DashboardMetricDisplayCard(string Icon, string Value, string Label, string CssClass);

public readonly record struct DashboardChartBucket(string Style, string Label);

public readonly record struct DashboardTopicBar(string Topic, int Count, string Style);
