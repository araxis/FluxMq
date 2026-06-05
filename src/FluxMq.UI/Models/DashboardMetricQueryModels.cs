namespace FluxMq.UI.Models;

public sealed record DashboardMetricSourceDescriptor(
    string Id,
    string DisplayName,
    string Description);

public sealed record DashboardMetricAggregationDescriptor(
    string Id,
    string DisplayName,
    string Unit);

public sealed record DashboardMetricQueryDefinition(
    string Source,
    string Aggregation,
    string Window,
    string? EventType = null,
    string? TopicStartsWith = null,
    string? TopicNotStartsWith = null,
    string? Status = null,
    string? GroupBy = null,
    string Format = "number");

public sealed record DashboardMetricValue(
    string Label,
    double Value,
    string Unit,
    string FormattedValue);

public sealed record FluxChartSeries(
    string Name,
    IReadOnlyList<double> Values,
    IReadOnlyList<string> Labels);
