using FluxMq.UI.Models;
using System.Globalization;

namespace FluxMq.UI.Services;

public sealed class DashboardMetricRegistry
{
    public IReadOnlyList<DashboardMetricSourceDescriptor> Sources { get; } =
    [
        new("runtimeEvents", "Runtime events", "Events emitted by the active app runtime."),
        new("topicProjection", "Topic projection", "Topic and message activity derived from runtime events."),
        new("mqttSnapshots", "MQTT snapshots", "Broker throughput, QoS, retain, and payload metrics."),
        new("payloadInspection", "Payload inspection", "Payload size and content inspection summaries.")
    ];

    public IReadOnlyList<DashboardMetricAggregationDescriptor> Aggregations { get; } =
    [
        new("count", "Count", "events"),
        new("rate", "Rate", "/s"),
        new("topics", "Topics", "topics"),
        new("payloadBytes", "Payload bytes", "bytes"),
        new("averagePayload", "Average payload", "bytes"),
        new("retained", "Retained", "messages")
    ];

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

        var aggregation = FindAggregation(query.Aggregation) ?? Aggregations[0];
        var value = query.Aggregation switch
        {
            "rate" => snapshot.EventsPerSecond,
            "topics" => snapshot.UniqueTopicCount,
            "payloadBytes" => snapshot.TotalPayloadBytes,
            "averagePayload" => snapshot.AveragePayloadBytes,
            "retained" => snapshot.RetainedCount,
            _ => snapshot.Count
        };

        return new DashboardMetricValue(
            aggregation.DisplayName,
            value,
            aggregation.Unit,
            FormatValue(value, aggregation.Unit, query.Format));
    }

    public DashboardMetricQueryDefinition CreateDefaultQuery(string widgetType)
        => widgetType switch
        {
            DashboardWidgetCatalog.RateTileType or DashboardWidgetCatalog.EventRateType => new("runtimeEvents", "rate", "60s"),
            DashboardWidgetCatalog.TopicActivityType or DashboardWidgetCatalog.TopicTreeType => new("topicProjection", "topics", "60s", GroupBy: "topic"),
            DashboardWidgetCatalog.PayloadDistributionType => new("payloadInspection", "payloadBytes", "60s", GroupBy: "bucket"),
            DashboardWidgetCatalog.QosRetainBreakdownType => new("mqttSnapshots", "count", "60s", GroupBy: "qosRetain"),
            _ => new("runtimeEvents", "count", "60s")
        };

    private static string FormatValue(double value, string unit, string format)
    {
        var formatted = format switch
        {
            "bytes" => FormatBytes(value),
            "percent" => $"{value:0.#}%",
            _ => value.ToString(value >= 100 ? "0" : "0.##", CultureInfo.InvariantCulture)
        };

        return string.IsNullOrWhiteSpace(unit) || format == "bytes"
            ? formatted
            : $"{formatted} {unit}";
    }

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
