using FluxMq.App.Metrics;
using FluxMq.UI.Models;

namespace FluxMq.UI.Services;

public sealed class DashboardMetricRegistry
{
    public IReadOnlyList<DashboardMetricSourceDescriptor> Sources { get; } =
        [.. FluxMetricCatalog.Shared.Sources.Select(static source => new DashboardMetricSourceDescriptor(
            source.Id,
            source.Label,
            source.Description))];

    public IReadOnlyList<DashboardMetricAggregationDescriptor> Aggregations { get; } =
        [.. FluxMetricCatalog.Shared.Measures.Select(static measure => new DashboardMetricAggregationDescriptor(
            measure.Id,
            measure.Label,
            measure.Unit))];

    private readonly FluxMetricEvaluationEngine _engine = new();

    public DashboardMetricSourceDescriptor? FindSource(string? source)
        => Sources.FirstOrDefault(item => string.Equals(item.Id, source, StringComparison.Ordinal));

    public DashboardMetricAggregationDescriptor? FindAggregation(string? aggregation)
        => Aggregations.FirstOrDefault(item => string.Equals(item.Id, aggregation, StringComparison.Ordinal));

    public DashboardMetricValue Evaluate(
        DashboardMetricQueryDefinition query,
        DashboardEventSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(snapshot);

        var definition = DashboardMetricQueryMapper.ToFluxMetricDefinition("metric", query);
        var value = _engine.Evaluate(definition, DashboardMetricQueryMapper.ToFluxMetricRuntimeSnapshot(snapshot));
        return DashboardMetricQueryMapper.ToDashboardMetricValue(value);
    }

    public static DashboardMetricValue EvaluateLegacyMetric(
        string metric,
        DashboardEventSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return metric switch
        {
            DashboardWidgetCatalog.MetricCurrentRate => new(
                "Rate",
                snapshot.EventsPerSecond,
                "/s",
                FluxMetricEvaluationEngine.FormatValue(snapshot.EventsPerSecond, "/s", FluxMetricCatalog.FormatNumber)),
            DashboardWidgetCatalog.MetricRecent => new(
                "Recent",
                snapshot.RecentCount,
                "events",
                FluxMetricEvaluationEngine.FormatValue(snapshot.RecentCount, "events", FluxMetricCatalog.FormatNumber)),
            DashboardWidgetCatalog.MetricPayloadBytes => new(
                "Payload bytes",
                snapshot.TotalPayloadBytes,
                "bytes",
                FluxMetricEvaluationEngine.FormatValue(snapshot.TotalPayloadBytes, "bytes", FluxMetricCatalog.FormatBytes)),
            DashboardWidgetCatalog.MetricTopics => new(
                "Topics",
                snapshot.UniqueTopicCount,
                "topics",
                FluxMetricEvaluationEngine.FormatValue(snapshot.UniqueTopicCount, "topics", FluxMetricCatalog.FormatNumber)),
            DashboardWidgetCatalog.MetricRetained => new(
                "Retained",
                snapshot.RetainedCount,
                "messages",
                FluxMetricEvaluationEngine.FormatValue(snapshot.RetainedCount, "messages", FluxMetricCatalog.FormatNumber)),
            DashboardWidgetCatalog.MetricAveragePayload => new(
                "Average payload",
                snapshot.AveragePayloadBytes,
                "bytes",
                FluxMetricEvaluationEngine.FormatValue(snapshot.AveragePayloadBytes, "bytes", FluxMetricCatalog.FormatBytes)),
            _ => new(
                "Count",
                snapshot.Count,
                "events",
                FluxMetricEvaluationEngine.FormatValue(snapshot.Count, "events", FluxMetricCatalog.FormatNumber))
        };
    }

    public DashboardMetricQueryDefinition CreateDefaultQuery(string widgetType)
        => widgetType switch
        {
            DashboardWidgetCatalog.RateTileType or DashboardWidgetCatalog.EventRateType => new(FluxMetricCatalog.RuntimeEventsSource, FluxMetricCatalog.MeasureRate, "60s"),
            DashboardWidgetCatalog.TopicActivityType or DashboardWidgetCatalog.TopicTreeType => new(FluxMetricCatalog.TopicProjectionSource, FluxMetricCatalog.MeasureTopics, "60s", GroupBy: "topic"),
            DashboardWidgetCatalog.PayloadDistributionType => new(FluxMetricCatalog.PayloadInspectionSource, FluxMetricCatalog.MeasurePayloadBytes, "60s", GroupBy: "bucket"),
            DashboardWidgetCatalog.QosRetainBreakdownType => new(FluxMetricCatalog.MqttSnapshotsSource, FluxMetricCatalog.MeasureCount, "60s", GroupBy: "qosRetain"),
            DashboardWidgetCatalog.QosBreakdownType => new(FluxMetricCatalog.MqttSnapshotsSource, FluxMetricCatalog.MeasureCount, "60s", GroupBy: "qos"),
            DashboardWidgetCatalog.RetainBreakdownType => new(FluxMetricCatalog.MqttSnapshotsSource, FluxMetricCatalog.MeasureCount, "60s", GroupBy: "retain"),
            DashboardWidgetCatalog.StatusValueType => new(FluxMetricCatalog.RuntimeEventsSource, FluxMetricCatalog.MeasureCount, "60s"),
            _ => DashboardMetricQueryMapper.ToDashboardQuery(FluxMetricCatalog.Shared.CreateDefaultDefinition("metric"))
        };
}
