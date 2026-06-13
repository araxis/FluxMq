using FluxMq.App.Metrics;
using FluxMq.UI.Models;

namespace FluxMq.UI.Services;

/// <summary>
/// UI-side helper that turns a dashboard event snapshot into a formatted scalar for a tile. It reads the snapshot
/// aggregate that matches the metric's type id and formats it using that kind's display metadata from the catalog,
/// so a tile shows the right value, label, and unit whether it comes from a live reading or the snapshot fallback.
/// </summary>
public sealed class DashboardMetricRegistry
{
    private readonly IFluxMetricCatalog _catalog;

    public DashboardMetricRegistry(IFluxMetricCatalog? catalog = null)
        => _catalog = catalog ?? FluxMetricCatalog.CreateDefault();

    /// <summary>Maps a legacy aggregation token to a registered metric type id.</summary>
    public static string TypeForMeasure(string? measure) => MessageCountMetric.TypeId;

    /// <summary>Maps a metric type id back to the legacy aggregation token used by the dashboard projection.</summary>
    public static string MeasureForType(string? typeId) => "count";

    /// <summary>Reads the snapshot aggregate for the given metric type and formats it.</summary>
    public DashboardMetricValue Evaluate(string? typeId, DashboardEventSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return Format(typeId, SnapshotValue(typeId, snapshot));
    }

    /// <summary>Formats an already-known value (e.g. a live reading) using the metric kind's display metadata.</summary>
    public DashboardMetricValue Format(string? typeId, double value)
    {
        if (!string.IsNullOrWhiteSpace(typeId) && _catalog.Describe(typeId) is { } descriptor)
        {
            return new(descriptor.DisplayName, value, descriptor.Unit, MetricFormats.Format(value, descriptor.Format));
        }

        return new("Event count", value, "events", MetricFormats.Format(value, MetricFormats.Number));
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

    // Maps each metric kind to the snapshot aggregate it reports, so the snapshot fallback (no live reading) is
    // still type-correct: a topic.count tile shows distinct topics, an event.rate tile shows the per-second rate, etc.
    private static double SnapshotValue(string? typeId, DashboardEventSnapshot snapshot)
        => typeId switch
        {
            TopicCountMetric.TypeId or WindowedTopicCountMetric.TypeId => snapshot.UniqueTopicCount,
            EventRateMetric.TypeId => snapshot.EventsPerSecond,
            PayloadBytesMetric.TypeId => snapshot.TotalPayloadBytes,
            RetainedCountMetric.TypeId => snapshot.RetainedCount,
            _ => snapshot.RecentCount
        };
}
