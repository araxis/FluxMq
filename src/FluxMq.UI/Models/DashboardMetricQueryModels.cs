namespace FluxMq.UI.Models;

public sealed record DashboardMetricSourceDescriptor(
    string Id,
    string DisplayName,
    string Description);

public sealed record DashboardMetricAggregationDescriptor(
    string Id,
    string DisplayName,
    string Unit);

public sealed record DashboardMetricQueryDefinition
{
    public DashboardMetricQueryDefinition(
        string Source,
        string Aggregation,
        string Window,
        string? EventType = null,
        string? TopicStartsWith = null,
        string? TopicNotStartsWith = null,
        string? Status = null,
        string? GroupBy = null,
        string Format = "number",
        IReadOnlyDictionary<string, string>? AdditionalFilters = null)
    {
        this.Source = Source;
        this.Aggregation = Aggregation;
        this.Window = Window;
        this.EventType = EventType;
        this.TopicStartsWith = TopicStartsWith;
        this.TopicNotStartsWith = TopicNotStartsWith;
        this.Status = Status;
        this.GroupBy = GroupBy;
        this.Format = Format;
        this.AdditionalFilters = AdditionalFilters ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }

    public string Source { get; init; }

    public string Aggregation { get; init; }

    public string Window { get; init; }

    public string? EventType { get; init; }

    public string? TopicStartsWith { get; init; }

    public string? TopicNotStartsWith { get; init; }

    public string? Status { get; init; }

    public string? GroupBy { get; init; }

    public string Format { get; init; }

    public IReadOnlyDictionary<string, string> AdditionalFilters { get; init; }
}

public sealed record DashboardMetricValue(
    string Label,
    double Value,
    string Unit,
    string FormattedValue);

public sealed record FluxChartSeries(
    string Name,
    IReadOnlyList<double> Values,
    IReadOnlyList<string> Labels);
