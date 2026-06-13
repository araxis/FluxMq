using FluxMq.App.Metrics;
using FluxMq.UI.Models;

namespace FluxMq.UI.Services;

/// <summary>
/// UI-side bridge that maps a metric type id to the matching <see cref="DashboardEventSnapshot"/> aggregate and
/// formats it for display using the registered <see cref="IFluxMetricType{TValue}"/> metadata.
/// </summary>
public sealed class DashboardMetricRegistry
{
    private readonly IFluxMetricTypeRegistry _types;

    public DashboardMetricRegistry()
        : this(FluxMetricTypeRegistry.CreateDefault())
    {
    }

    public DashboardMetricRegistry(IFluxMetricTypeRegistry types)
    {
        ArgumentNullException.ThrowIfNull(types);
        _types = types;
    }

    public IReadOnlyList<IFluxMetricType<double>> Types => _types.NumberTypes;

    /// <summary>Maps a legacy aggregation token to the metric type id (inverse of <see cref="MeasureForType"/>).</summary>
    public static string TypeForMeasure(string? measure)
        => measure switch
        {
            "rate" => EventRateMetricType.Id,
            "topics" => UniqueTopicCountMetricType.Id,
            "payloadBytes" => PayloadBytesMetricType.Id,
            "averagePayload" => AveragePayloadMetricType.Id,
            "retained" => RetainedCountMetricType.Id,
            _ => EventCountMetricType.Id
        };

    /// <summary>Maps a metric type id back to the legacy aggregation token used by the dashboard projection.</summary>
    public static string MeasureForType(string? typeId)
        => typeId switch
        {
            EventRateMetricType.Id => "rate",
            UniqueTopicCountMetricType.Id => "topics",
            PayloadBytesMetricType.Id => "payloadBytes",
            AveragePayloadMetricType.Id => "averagePayload",
            RetainedCountMetricType.Id => "retained",
            _ => "count"
        };

    /// <summary>Reads the snapshot aggregate that the given metric type computes and formats it.</summary>
    public DashboardMetricValue Evaluate(string? typeId, DashboardEventSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return Format(typeId, SnapshotValue(typeId, snapshot));
    }

    /// <summary>Formats an already-known value (e.g. a live stream reading) using the metric type metadata.</summary>
    public DashboardMetricValue Format(string? typeId, double value)
    {
        if (!string.IsNullOrWhiteSpace(typeId) && _types.TryGetNumberType(typeId, out var type))
        {
            return new DashboardMetricValue(type.DisplayName, value, type.Unit, MetricFormats.Format(value, type.Format));
        }

        return new DashboardMetricValue("Event count", value, "events", MetricFormats.Format(value, MetricFormats.Number));
    }

    public static DashboardMetricValue EvaluateLegacyMetric(string metric, DashboardEventSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return metric switch
        {
            DashboardWidgetCatalog.MetricCurrentRate => new(
                "Event rate", snapshot.EventsPerSecond, "/s", MetricFormats.Format(snapshot.EventsPerSecond, MetricFormats.Number)),
            DashboardWidgetCatalog.MetricRecent => new(
                "Event count", snapshot.RecentCount, "events", MetricFormats.Format(snapshot.RecentCount, MetricFormats.Number)),
            DashboardWidgetCatalog.MetricPayloadBytes => new(
                "Payload bytes", snapshot.TotalPayloadBytes, "bytes", MetricFormats.Format(snapshot.TotalPayloadBytes, MetricFormats.Bytes)),
            DashboardWidgetCatalog.MetricTopics => new(
                "Unique topics", snapshot.UniqueTopicCount, "topics", MetricFormats.Format(snapshot.UniqueTopicCount, MetricFormats.Number)),
            DashboardWidgetCatalog.MetricRetained => new(
                "Retained messages", snapshot.RetainedCount, "messages", MetricFormats.Format(snapshot.RetainedCount, MetricFormats.Number)),
            DashboardWidgetCatalog.MetricAveragePayload => new(
                "Average payload", snapshot.AveragePayloadBytes, "bytes", MetricFormats.Format(snapshot.AveragePayloadBytes, MetricFormats.Bytes)),
            _ => new(
                "Event count", snapshot.Count, "events", MetricFormats.Format(snapshot.Count, MetricFormats.Number))
        };
    }

    private static double SnapshotValue(string? typeId, DashboardEventSnapshot snapshot)
        => typeId switch
        {
            EventRateMetricType.Id => snapshot.EventsPerSecond,
            UniqueTopicCountMetricType.Id => snapshot.UniqueTopicCount,
            PayloadBytesMetricType.Id => snapshot.TotalPayloadBytes,
            AveragePayloadMetricType.Id => snapshot.AveragePayloadBytes,
            RetainedCountMetricType.Id => snapshot.RetainedCount,
            _ => snapshot.RecentCount
        };
}
