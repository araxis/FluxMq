using FluxMq.App.Metrics;
using FluxMq.UI.Models;

namespace FluxMq.UI.Services;

public static class DashboardMetricQueryMapper
{
    public static FluxMetricDefinition ToFluxMetricDefinition(
        string name,
        DashboardMetricQueryDefinition query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return new FluxMetricDefinition(
            name,
            query.Source,
            query.Aggregation,
            query.Window,
            eventType: query.EventType,
            topicStartsWith: query.TopicStartsWith,
            topicNotStartsWith: query.TopicNotStartsWith,
            status: query.Status,
            groupBy: query.GroupBy,
            format: query.Format,
            additionalFilters: query.AdditionalFilters);
    }

    public static FluxMetricDefinition ToFluxMetricDefinition(DashboardMetricSnapshot metric)
    {
        ArgumentNullException.ThrowIfNull(metric);

        return new FluxMetricDefinition(
            metric.Name,
            metric.Source,
            metric.Aggregation,
            metric.Window,
            eventType: metric.ReadFilter(FluxMetricCatalog.EventTypeKey),
            topicStartsWith: metric.ReadFilter(FluxMetricCatalog.TopicStartsWithKey),
            topicNotStartsWith: metric.ReadFilter(FluxMetricCatalog.TopicNotStartsWithKey),
            status: metric.ReadFilter(FluxMetricCatalog.StatusKey),
            groupBy: metric.GroupBy,
            format: metric.ReadFormat("unit") ?? FluxMetricCatalog.FormatNumber,
            additionalFilters: ReadAdditionalFilters(metric));
    }

    public static DashboardMetricQueryDefinition ToDashboardQuery(FluxMetricDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return new DashboardMetricQueryDefinition(
            definition.Source,
            definition.Measure,
            definition.Window,
            EventType: definition.EventType,
            TopicStartsWith: definition.TopicStartsWith,
            TopicNotStartsWith: definition.TopicNotStartsWith,
            Status: definition.Status,
            GroupBy: definition.GroupBy,
            Format: definition.Format,
            AdditionalFilters: definition.AdditionalFilters);
    }

    public static DashboardMetricSnapshot ToDashboardMetricSnapshot(
        string metricName,
        DashboardMetricQueryDefinition query)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(metricName);
        ArgumentNullException.ThrowIfNull(query);

        var filters = new Dictionary<string, string>(StringComparer.Ordinal);
        AddIfPresent(filters, FluxMetricCatalog.EventTypeKey, query.EventType);
        AddIfPresent(filters, FluxMetricCatalog.TopicStartsWithKey, query.TopicStartsWith);
        AddIfPresent(filters, FluxMetricCatalog.TopicNotStartsWithKey, query.TopicNotStartsWith);
        AddIfPresent(filters, FluxMetricCatalog.StatusKey, query.Status);
        foreach (var (key, value) in query.AdditionalFilters)
        {
            AddIfPresent(filters, key, value);
        }

        return new DashboardMetricSnapshot(
            metricName,
            query.Source,
            query.Aggregation,
            query.Window,
            query.GroupBy,
            filters,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["unit"] = query.Format
            });
    }

    public static DashboardMetricValue ToDashboardMetricValue(FluxMetricValue value)
        => new(value.Label, value.Value, value.Unit, value.FormattedValue);

    public static FluxMetricRuntimeSnapshot ToFluxMetricRuntimeSnapshot(DashboardEventSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new FluxMetricRuntimeSnapshot(
            snapshot.Count,
            snapshot.LatestEvent,
            snapshot.RecentCount,
            snapshot.RateWindow,
            snapshot.EventsPerSecond,
            snapshot.Events,
            [.. snapshot.TopicCounts.Select(static topic => new FluxMetricTopicCount(topic.Topic, topic.Count))],
            snapshot.TotalPayloadBytes,
            snapshot.UniqueTopicCount,
            snapshot.RetainedCount);
    }

    public static DashboardEventSnapshot ToDashboardEventSnapshot(FluxMetricRuntimeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new DashboardEventSnapshot(
            snapshot.Count,
            snapshot.LatestEvent,
            snapshot.RecentCount,
            snapshot.RateWindow,
            snapshot.EventsPerSecond,
            snapshot.Events,
            [],
            [.. snapshot.TopicCounts.Select(static topic => new DashboardTopicMetric(topic.Topic, topic.Count))],
            snapshot.TotalPayloadBytes,
            snapshot.UniqueTopicCount,
            snapshot.RetainedCount);
    }

    private static IReadOnlyDictionary<string, string> ReadAdditionalFilters(DashboardMetricSnapshot metric)
    {
        var filters = new Dictionary<string, string>(StringComparer.Ordinal);
        AddIfPresent(filters, FluxMetricCatalog.SubjectStartsWithKey, metric.ReadFilter(FluxMetricCatalog.SubjectStartsWithKey));
        AddIfPresent(filters, FluxMetricCatalog.AttributeFilterKey("qos"), metric.ReadFilter(FluxMetricCatalog.AttributeFilterKey("qos")));
        AddIfPresent(filters, FluxMetricCatalog.AttributeFilterKey("retain"), metric.ReadFilter(FluxMetricCatalog.AttributeFilterKey("retain")));
        AddIfPresent(filters, FluxMetricCatalog.AttributeFilterKey("schemaId"), metric.ReadFilter(FluxMetricCatalog.AttributeFilterKey("schemaId")));
        return filters;
    }

    private static void AddIfPresent(Dictionary<string, string> target, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            target[key] = value.Trim();
        }
    }
}
