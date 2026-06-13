using FluxMq.App.Metrics;
using FluxMq.UI.Models;

namespace FluxMq.UI.Services;

/// <summary>
/// UI-side helper that turns a dashboard event snapshot into a formatted scalar for a tile. The flat metric
/// framework no longer exposes per-type display/format metadata to the dashboard layer, so this currently
/// formats with a neutral default; the dashboard value path is being reconnected to the new framework later.
/// </summary>
public sealed class DashboardMetricRegistry
{
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

    /// <summary>Formats an already-known value using a neutral default.</summary>
    public DashboardMetricValue Format(string? typeId, double value)
        => new("Event count", value, "events", MetricFormats.Format(value, MetricFormats.Number));

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

    private static double SnapshotValue(string? typeId, DashboardEventSnapshot snapshot) => snapshot.RecentCount;
}
