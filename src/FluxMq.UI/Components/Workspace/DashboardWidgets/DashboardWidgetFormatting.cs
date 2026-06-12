using FluxFlow.Engine.Components;
using FluxMq.Core.Models;
using FluxMq.UI.Models;
using FluxMq.UI.Services;
using MudBlazor;
using System.Globalization;
using System.Text;

namespace FluxMq.UI.Components.Workspace;

public static class DashboardWidgetFormatting
{
    public static string WidgetTitle(DashboardWidgetSnapshot widget)
    {
        var configuredTitle = widget.ReadString(DashboardWidgetCatalog.MetricValueTitleKey) ??
            widget.ReadString(DashboardWidgetCatalog.MetricDigitalLabelKey) ??
            widget.ReadString("title");
        if (string.Equals(widget.Type, DashboardWidgetCatalog.KpiTileType, StringComparison.Ordinal) &&
            (string.IsNullOrWhiteSpace(configuredTitle) ||
             string.Equals(configuredTitle, "KPI tile", StringComparison.OrdinalIgnoreCase)))
        {
            return KpiDefaultTitle(widget);
        }

        return configuredTitle ?? widget.Type switch
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
            _ => widget.Name
        };
    }

    public static string WidgetSubtitle(DashboardWidgetSnapshot widget)
        => string.Equals(widget.Type, DashboardWidgetCatalog.KpiTileType, StringComparison.Ordinal)
            ? KpiSubtitle(widget)
            : string.Equals(widget.Type, DashboardWidgetCatalog.TopicTreeType, StringComparison.Ordinal)
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

    private static string KpiDefaultTitle(DashboardWidgetSnapshot widget)
        => widget.ReadString(DashboardWidgetCatalog.PrimaryMetricKey) switch
        {
            DashboardWidgetCatalog.MetricCurrentRate => "Event rate",
            DashboardWidgetCatalog.MetricRecent => "Recent messages",
            DashboardWidgetCatalog.MetricPayloadBytes => "Payload size",
            DashboardWidgetCatalog.MetricTopics => "Topic count",
            DashboardWidgetCatalog.MetricRetained => "Retained messages",
            DashboardWidgetCatalog.MetricAveragePayload => "Average payload",
            _ => "Messages"
        };

    private static string KpiSubtitle(DashboardWidgetSnapshot widget)
    {
        var subtitle = widget.ReadString(DashboardWidgetCatalog.MetricValueSubtitleKey) ??
            widget.ReadString("subtitle");
        return string.IsNullOrWhiteSpace(subtitle) ? "Total matching events" : subtitle;
    }

    public static string WidgetIcon(DashboardWidgetSnapshot widget)
        => widget.Type switch
        {
            DashboardWidgetCatalog.KpiTileType => Icons.Material.Filled.Speed,
            DashboardWidgetCatalog.StatusStripType => Icons.Material.Filled.ViewWeek,
            DashboardWidgetCatalog.StatusValueType => Icons.Material.Filled.Verified,
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
            DashboardWidgetCatalog.QosBreakdownType => Icons.Material.Filled.PieChart,
            DashboardWidgetCatalog.RetainBreakdownType => Icons.Material.Filled.PushPin,
            DashboardWidgetCatalog.TopicTreeType => Icons.Material.Filled.AccountTree,
            _ => Icons.Material.Filled.Widgets
        };

    public static string WidgetClass(DashboardWidgetSnapshot widget)
        => widget.Type switch
        {
            DashboardWidgetCatalog.KpiTileType => "kpi-tile",
            DashboardWidgetCatalog.StatusStripType => "status-strip",
            DashboardWidgetCatalog.StatusValueType => "status-value",
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
            DashboardWidgetCatalog.QosBreakdownType => "qos-breakdown",
            DashboardWidgetCatalog.RetainBreakdownType => "retain-breakdown",
            DashboardWidgetCatalog.TopicTreeType => "topic-tree-widget",
            _ => "unknown"
        };

    public static string WidgetStyle(DashboardWidgetSnapshot widget)
    {
        var parts = new List<string>();
        AddCssVariable(parts, "--dashboard-widget-bg", widget.ReadString("style.background"), IsSafeCssToken);
        AddCssVariable(parts, "--dashboard-widget-surface", widget.ReadString("style.surface"), IsSafeCssToken);
        AddCssVariable(parts, "--dashboard-widget-accent", widget.ReadString("style.accent"), IsSafeCssToken);
        AddCssVariable(parts, "--dashboard-widget-text", BodyTextColor(widget), IsSafeCssToken);
        AddCssVariable(parts, "--dashboard-widget-muted", SubtitleColor(widget), IsSafeCssToken);
        AddCssVariable(parts, "--dashboard-widget-title", TitleColor(widget), IsSafeCssToken);
        AddCssVariable(parts, "--dashboard-widget-subtitle", SubtitleColor(widget), IsSafeCssToken);
        AddCssVariable(parts, "--dashboard-widget-value", ValueColor(widget), IsSafeCssToken);
        AddCssVariable(parts, "--dashboard-widget-border", widget.ReadString("style.border"), IsSafeCssToken);
        AddCssVariable(parts, "--dashboard-widget-border-width", BorderWidthValue(widget), static value => value.EndsWith("px", StringComparison.Ordinal));
        AddCssVariable(parts, "--dashboard-widget-radius", PixelValue(widget.ReadString("style.radius")), static value => value.EndsWith("px", StringComparison.Ordinal));
        AddCssVariable(parts, "--dashboard-widget-padding", WidgetPaddingValue(widget), static value => value.EndsWith("px", StringComparison.Ordinal));
        AddKpiCssVariables(parts, widget);
        return string.Join(string.Empty, parts);
    }

    public static string MetricGridStyle(DashboardWidgetSnapshot widget)
        => $"--metric-columns:{DashboardWidgetCatalog.NormalizeMetricCardColumns(widget.ReadString(DashboardWidgetCatalog.MetricCardColumnsKey))};";

    public static string MetricDisplayName(string metricName)
    {
        var raw = string.IsNullOrWhiteSpace(metricName)
            ? "metric"
            : metricName.Trim();
        var ordinal = ExtractTrailingDigits(raw, out var stem);

        if (stem.EndsWith("Metric", StringComparison.OrdinalIgnoreCase))
        {
            stem = stem[..^"Metric".Length];
            if (string.IsNullOrWhiteSpace(ordinal))
            {
                ordinal = ExtractTrailingDigits(stem, out stem);
            }
        }

        if (string.IsNullOrWhiteSpace(stem))
        {
            return string.IsNullOrWhiteSpace(ordinal)
                ? "Metric"
                : $"Metric #{ordinal}";
        }

        var label = ToReadableIdentifier(stem);
        return string.IsNullOrWhiteSpace(ordinal)
            ? $"{label} metric"
            : $"{label} metric #{ordinal}";
    }

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

    public static DashboardGaugeVisualState GaugeVisualState(
        DashboardWidgetSnapshot widget,
        DashboardMetricValue metric)
    {
        var min = ReadGaugeNumber(widget, DashboardWidgetCatalog.GaugeMinKey, DashboardWidgetCatalog.GaugeDefaultMin);
        var max = ReadGaugeNumber(widget, DashboardWidgetCatalog.GaugeMaxKey, DashboardWidgetCatalog.GaugeDefaultMax);
        if (max <= min)
        {
            max = min + 1;
        }

        var target = Math.Clamp(
            ReadGaugeNumber(widget, DashboardWidgetCatalog.GaugeTargetKey, DashboardWidgetCatalog.GaugeDefaultTarget),
            min,
            max);
        var warning = Math.Clamp(
            ReadGaugeNumber(widget, DashboardWidgetCatalog.GaugeWarningKey, DashboardWidgetCatalog.GaugeDefaultWarning),
            min,
            max);
        var critical = Math.Clamp(
            ReadGaugeNumber(widget, DashboardWidgetCatalog.GaugeCriticalKey, DashboardWidgetCatalog.GaugeDefaultCritical),
            min,
            max);
        if (critical < warning)
        {
            (warning, critical) = (critical, warning);
        }

        var progress = Math.Clamp((metric.Value - min) / (max - min) * 100, 0, 100);
        var targetProgress = Math.Clamp((target - min) / (max - min) * 100, 0, 100);
        var fillColor = metric.Value >= critical
            ? ReadGaugeColor(widget, DashboardWidgetCatalog.GaugeCriticalColorKey, DashboardWidgetCatalog.GaugeDefaultCriticalColor)
            : metric.Value >= warning
                ? ReadGaugeColor(widget, DashboardWidgetCatalog.GaugeWarningColorKey, DashboardWidgetCatalog.GaugeDefaultWarningColor)
                : ReadGaugeColor(widget, DashboardWidgetCatalog.GaugeNormalColorKey, DashboardWidgetCatalog.GaugeDefaultNormalColor);

        var style = string.Concat(
            "--gauge-progress:",
            progress.ToString("0.###", CultureInfo.InvariantCulture),
            "%;--gauge-target:",
            targetProgress.ToString("0.###", CultureInfo.InvariantCulture),
            "%;--gauge-target-angle:",
            (targetProgress * 3.6).ToString("0.###", CultureInfo.InvariantCulture),
            "deg;--gauge-fill:",
            fillColor,
            ";");

        return new DashboardGaugeVisualState(
            style,
            $"{FormatGaugeNumber(min)} - {FormatGaugeNumber(max)}",
            FormatGaugeNumber(target),
            progress,
            targetProgress);
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

    private static void AddCssVariable(List<string> parts, string name, string? value, Func<string, bool> isValid)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            var normalized = value.Trim();
            if (isValid(normalized))
            {
                parts.Add($"{name}:{normalized};");
            }
        }
    }

    private static string? PixelValue(string? value)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            return null;
        }

        return $"{Math.Clamp(number, 0, 64).ToString("0.###", CultureInfo.InvariantCulture)}px";
    }

    private static string? BorderWidthValue(DashboardWidgetSnapshot widget)
    {
        if (string.Equals(widget.ReadString("style.borderMode"), "none", StringComparison.OrdinalIgnoreCase))
        {
            return "0px";
        }

        return PixelValue(widget.ReadString("style.borderWidth"));
    }

    private static string? WidgetPaddingValue(DashboardWidgetSnapshot widget)
    {
        var visualization = DashboardWidgetCatalog.NormalizeMetricVisualization(
            widget.ReadString(DashboardWidgetCatalog.MetricVisualizationKey));
        if (string.Equals(visualization, DashboardMetricVisualizationIds.Value, StringComparison.Ordinal))
        {
            return PixelValue(widget.ReadString(DashboardWidgetCatalog.MetricValuePaddingKey) ?? widget.ReadString("style.padding"));
        }

        return PixelValue(widget.ReadString("style.padding"));
    }

    private static string? TitleColor(DashboardWidgetSnapshot widget)
        => widget.ReadString(DashboardWidgetCatalog.MetricValueTitleColorKey) ??
           widget.ReadString(DashboardWidgetCatalog.KpiTitleColorKey) ??
           widget.ReadString("style.titleColor") ??
           widget.ReadString("style.text");

    private static string? SubtitleColor(DashboardWidgetSnapshot widget)
        => widget.ReadString(DashboardWidgetCatalog.MetricValueSubtitleColorKey) ??
           widget.ReadString(DashboardWidgetCatalog.KpiSubtitleColorKey) ??
           widget.ReadString("style.subtitleColor") ??
           widget.ReadString("style.mutedText");

    private static string? ValueColor(DashboardWidgetSnapshot widget)
        => widget.ReadString(DashboardWidgetCatalog.MetricValueValueColorKey) ??
           widget.ReadString(DashboardWidgetCatalog.KpiValueColorKey) ??
           widget.ReadString("style.valueColor") ??
           widget.ReadString("style.text");

    private static string? BodyTextColor(DashboardWidgetSnapshot widget)
        => widget.ReadString(DashboardWidgetCatalog.MetricValueValueColorKey) ??
           widget.ReadString(DashboardWidgetCatalog.KpiValueColorKey) ??
           widget.ReadString(DashboardWidgetCatalog.MetricValueTitleColorKey) ??
           widget.ReadString(DashboardWidgetCatalog.KpiTitleColorKey) ??
           widget.ReadString("style.valueColor") ??
           widget.ReadString("style.titleColor") ??
           widget.ReadString("style.text");

    private static void AddKpiCssVariables(List<string> parts, DashboardWidgetSnapshot widget)
    {
        if (!string.Equals(widget.Type, DashboardWidgetCatalog.KpiTileType, StringComparison.Ordinal))
        {
            return;
        }

        AddCssVariable(
            parts,
            "--dashboard-kpi-title-align",
            DashboardWidgetCatalog.NormalizeKpiHorizontalAlignment(widget.ReadString(DashboardWidgetCatalog.MetricValueTitleAlignKey) ?? widget.ReadString(DashboardWidgetCatalog.KpiTitleAlignKey)),
            IsKpiHorizontalAlignment);
        AddCssVariable(
            parts,
            "--dashboard-kpi-title-items",
            KpiHorizontalAlignmentCss(widget.ReadString(DashboardWidgetCatalog.MetricValueTitleAlignKey) ?? widget.ReadString(DashboardWidgetCatalog.KpiTitleAlignKey)),
            IsKpiItemAlignment);
        AddCssVariable(
            parts,
            "--dashboard-kpi-value-align",
            DashboardWidgetCatalog.NormalizeKpiHorizontalAlignment(widget.ReadString(DashboardWidgetCatalog.MetricValueValueAlignKey) ?? widget.ReadString(DashboardWidgetCatalog.KpiValueAlignKey)),
            IsKpiHorizontalAlignment);
        AddCssVariable(
            parts,
            "--dashboard-kpi-value-items",
            KpiHorizontalAlignmentCss(widget.ReadString(DashboardWidgetCatalog.MetricValueValueAlignKey) ?? widget.ReadString(DashboardWidgetCatalog.KpiValueAlignKey)),
            IsKpiItemAlignment);
        AddCssVariable(
            parts,
            "--dashboard-kpi-value-placement",
            KpiValuePlacementCss(widget.ReadString(DashboardWidgetCatalog.MetricValueValuePlacementKey) ?? widget.ReadString(DashboardWidgetCatalog.KpiValuePlacementKey)),
            IsKpiPlacement);
    }

    private static string KpiHorizontalAlignmentCss(string? value)
        => DashboardWidgetCatalog.NormalizeKpiHorizontalAlignment(value) switch
        {
            DashboardWidgetCatalog.KpiAlignCenter => "center",
            DashboardWidgetCatalog.KpiAlignRight => "flex-end",
            _ => "flex-start"
        };

    private static string KpiValuePlacementCss(string? value)
        => DashboardWidgetCatalog.NormalizeKpiValuePlacement(value) switch
        {
            DashboardWidgetCatalog.KpiValuePlacementMiddle => "center",
            DashboardWidgetCatalog.KpiValuePlacementBottom => "flex-end",
            _ => "flex-start"
        };

    private static bool IsKpiHorizontalAlignment(string value)
        => value is DashboardWidgetCatalog.KpiAlignLeft or
            DashboardWidgetCatalog.KpiAlignCenter or
            DashboardWidgetCatalog.KpiAlignRight;

    private static bool IsKpiItemAlignment(string value)
        => value is "flex-start" or "center" or "flex-end";

    private static bool IsKpiPlacement(string value)
        => value is "flex-start" or "center" or "flex-end";

    private static bool IsSafeCssToken(string value)
        => value.Length <= 64 &&
           !value.Contains(';', StringComparison.Ordinal) &&
           !value.Contains("url", StringComparison.OrdinalIgnoreCase) &&
           !value.Contains("expression", StringComparison.OrdinalIgnoreCase);

    private static double ReadGaugeNumber(
        DashboardWidgetSnapshot widget,
        string key,
        string fallback)
        => double.TryParse(
            widget.ReadString(key) ?? fallback,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var parsed) &&
           double.IsFinite(parsed)
            ? parsed
            : double.Parse(fallback, CultureInfo.InvariantCulture);

    private static string ReadGaugeColor(
        DashboardWidgetSnapshot widget,
        string key,
        string fallback)
    {
        var value = widget.ReadString(key);
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var normalized = value.Trim().ToLowerInvariant();
        if (string.Equals(normalized, "transparent", StringComparison.Ordinal) ||
            (normalized.Length is 4 or 5 or 7 or 9 &&
             normalized[0] == '#' &&
             normalized.Skip(1).All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f')))
        {
            return normalized;
        }

        return fallback;
    }

    private static string FormatGaugeNumber(double value)
        => value.ToString(value switch
        {
            >= 1000 or <= -1000 => "0.#",
            >= 100 or <= -100 => "0.#",
            >= 10 or <= -10 => "0.##",
            _ => "0.###"
        }, CultureInfo.InvariantCulture);

    private static string ExtractTrailingDigits(string value, out string stem)
    {
        var index = value.Length;
        while (index > 0 && char.IsDigit(value[index - 1]))
        {
            index--;
        }

        stem = value[..index];
        return index == value.Length ? string.Empty : value[index..];
    }

    private static string ToReadableIdentifier(string value)
    {
        var builder = new StringBuilder(value.Length + 8);
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (index > 0 &&
                char.IsUpper(character) &&
                (char.IsLower(value[index - 1]) ||
                 char.IsDigit(value[index - 1]) ||
                 (index + 1 < value.Length && char.IsLower(value[index + 1]))))
            {
                builder.Append(' ');
            }

            builder.Append(character);
        }

        var words = builder
            .ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(CapitalizeWord);
        return string.Join(" ", words);
    }

    private static string CapitalizeWord(string word)
        => word.ToLowerInvariant() switch
        {
            "mqtt" => "MQTT",
            "qos" => "QoS",
            "json" => "JSON",
            _ => word.Length == 1
                ? char.ToUpperInvariant(word[0]).ToString()
                : char.ToUpperInvariant(word[0]) + word[1..]
        };
}

public sealed record DashboardMetricDisplayCard(string Icon, string Value, string Label, string CssClass);

public sealed record DashboardGaugeVisualState(
    string Style,
    string RangeLabel,
    string TargetLabel,
    double Progress,
    double TargetProgress);

public readonly record struct DashboardChartBucket(string Style, string Label);

public readonly record struct DashboardTopicBar(string Topic, int Count, string Style);
