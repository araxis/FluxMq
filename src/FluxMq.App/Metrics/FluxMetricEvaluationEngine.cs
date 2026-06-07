using FluxFlow.Engine.Components;
using System.Globalization;

namespace FluxMq.App.Metrics;

public sealed record FluxMetricTopicCount(string Topic, int Count);

public sealed record FluxMetricRuntimeSnapshot(
    int Count,
    FlowEvent? LatestEvent,
    int RecentCount,
    TimeSpan RateWindow,
    double EventsPerSecond,
    IReadOnlyList<FlowEvent> Events,
    IReadOnlyList<FluxMetricTopicCount> TopicCounts,
    long TotalPayloadBytes,
    int UniqueTopicCount,
    int RetainedCount)
{
    public double AveragePayloadBytes => Count == 0 ? 0 : TotalPayloadBytes / (double)Count;
}

public sealed class FluxMetricEvaluationEngine
{
    private readonly FluxMetricCatalog _catalog;

    public FluxMetricEvaluationEngine(FluxMetricCatalog? catalog = null)
    {
        _catalog = catalog ?? FluxMetricCatalog.Shared;
    }

    public FluxMetricValue Evaluate(
        FluxMetricDefinition definition,
        FluxMetricRuntimeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(snapshot);

        var measure = _catalog.FindMeasure(definition.Measure);
        var value = measure.Id switch
        {
            FluxMetricCatalog.MeasureRate => snapshot.EventsPerSecond,
            FluxMetricCatalog.MeasureTopics => snapshot.UniqueTopicCount,
            FluxMetricCatalog.MeasurePayloadBytes => snapshot.TotalPayloadBytes,
            FluxMetricCatalog.MeasureAveragePayload => snapshot.AveragePayloadBytes,
            FluxMetricCatalog.MeasureRetained => snapshot.RetainedCount,
            _ => snapshot.RecentCount
        };

        return new FluxMetricValue(
            measure.Label,
            value,
            measure.Unit,
            FormatValue(value, measure.Unit, definition.Format));
    }

    public FluxMetricRuntimeSnapshot CreateSnapshot(
        FluxMetricDefinition definition,
        IReadOnlyList<FlowEvent> events,
        DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(events);

        var rateWindow = ParseWindow(definition.Window);
        var threshold = (now ?? DateTimeOffset.UtcNow).Subtract(rateWindow);
        var matching = events
            .Where(flowEvent => _catalog.Matches(definition, flowEvent))
            .ToArray();
        var recentCount = matching.Count(flowEvent => flowEvent.Timestamp >= threshold);
        var topicCounts = BuildTopicCounts(matching);
        return new FluxMetricRuntimeSnapshot(
            matching.Length,
            matching.LastOrDefault(),
            recentCount,
            rateWindow,
            recentCount / rateWindow.TotalSeconds,
            matching.Reverse().Take(12).ToArray(),
            topicCounts,
            matching.Sum(static flowEvent => (long)(flowEvent.PayloadBytes ?? 0)),
            topicCounts.Count,
            matching.Count(IsRetained));
    }

    public static TimeSpan ParseWindow(string? value)
    {
        if (!FluxMetricQueryDraft.TryNormalizeWindow(value, out var normalized))
        {
            normalized = "60s";
        }

        var amount = double.Parse(normalized[..^1], CultureInfo.InvariantCulture);
        return normalized[^1] switch
        {
            'h' => TimeSpan.FromHours(amount),
            'm' => TimeSpan.FromMinutes(amount),
            _ => TimeSpan.FromSeconds(amount)
        };
    }

    public static string FormatValue(double value, string unit, string format)
    {
        var normalizedFormat = string.Equals(format, FluxMetricCatalog.FormatAuto, StringComparison.Ordinal)
            ? unit switch
            {
                "bytes" => FluxMetricCatalog.FormatBytes,
                "percent" => FluxMetricCatalog.FormatPercent,
                _ => FluxMetricCatalog.FormatNumber
            }
            : format;

        return normalizedFormat switch
        {
            FluxMetricCatalog.FormatBytes => FormatBytes(value),
            FluxMetricCatalog.FormatPercent => $"{value:0.#}%",
            _ => value.ToString(value >= 100 ? "0" : "0.##", CultureInfo.InvariantCulture)
        };
    }

    private static IReadOnlyList<FluxMetricTopicCount> BuildTopicCounts(IReadOnlyList<FlowEvent> events)
        => events
            .Where(static flowEvent => !string.IsNullOrWhiteSpace(flowEvent.Channel))
            .GroupBy(static flowEvent => flowEvent.Channel!, StringComparer.Ordinal)
            .Select(static group => new FluxMetricTopicCount(group.Key, group.Count()))
            .OrderByDescending(static metric => metric.Count)
            .ThenBy(static metric => metric.Topic, StringComparer.Ordinal)
            .ToArray();

    private static bool IsRetained(FlowEvent flowEvent)
        => bool.TryParse(flowEvent.GetAttribute("retain"), out var retained) && retained;

    private static string FormatBytes(double value)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        var unitIndex = 0;
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value:0.#} {units[unitIndex]}";
    }
}
