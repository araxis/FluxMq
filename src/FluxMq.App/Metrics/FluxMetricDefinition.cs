using System.Collections.ObjectModel;

namespace FluxMq.App.Metrics;

public enum MetricDefinitionMode
{
    Builder,
    Expression
}

public enum FluxMetricInstrumentKind
{
    Counter,
    Histogram,
    Gauge
}

public sealed record FluxMetricExportPolicy
{
    public static FluxMetricExportPolicy Disabled { get; } = new();

    public bool Enabled { get; init; }

    public FluxMetricInstrumentKind InstrumentKind { get; init; } = FluxMetricInstrumentKind.Counter;

    public string ExportedName { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string Unit { get; init; } = string.Empty;

    public IReadOnlyList<string> AllowedTags { get; init; } = [];
}

public sealed record FluxMetricDefinition
{
    public FluxMetricDefinition(
        string name,
        string source,
        string measure,
        string window,
        string? eventType = null,
        string? topicStartsWith = null,
        string? topicNotStartsWith = null,
        string? status = null,
        string? groupBy = null,
        string format = FluxMetricCatalog.FormatNumber,
        IReadOnlyDictionary<string, string>? additionalFilters = null,
        IReadOnlyDictionary<string, string>? labels = null,
        FluxMetricExportPolicy? exportPolicy = null,
        MetricDefinitionMode mode = MetricDefinitionMode.Builder)
    {
        Name = Normalize(name, "metric");
        Source = Normalize(source, FluxMetricCatalog.RuntimeEventsSource);
        Measure = Normalize(measure, FluxMetricCatalog.MeasureCount);
        Window = Normalize(window, "60s");
        EventType = NormalizeNullable(eventType);
        TopicStartsWith = NormalizeNullable(topicStartsWith);
        TopicNotStartsWith = NormalizeNullable(topicNotStartsWith);
        Status = NormalizeNullable(status);
        GroupBy = NormalizeNullable(groupBy);
        Format = Normalize(format, FluxMetricCatalog.FormatNumber);
        AdditionalFilters = ReadOnly(additionalFilters);
        Labels = ReadOnly(labels);
        ExportPolicy = exportPolicy ?? FluxMetricExportPolicy.Disabled;
        Mode = mode;
    }

    public string Name { get; init; }

    public string Source { get; init; }

    public string Measure { get; init; }

    public string Window { get; init; }

    public string? EventType { get; init; }

    public string? TopicStartsWith { get; init; }

    public string? TopicNotStartsWith { get; init; }

    public string? Status { get; init; }

    public string? GroupBy { get; init; }

    public string Format { get; init; }

    public IReadOnlyDictionary<string, string> AdditionalFilters { get; init; }

    public IReadOnlyDictionary<string, string> Labels { get; init; }

    public FluxMetricExportPolicy ExportPolicy { get; init; }

    public MetricDefinitionMode Mode { get; init; }

    private static string Normalize(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IReadOnlyDictionary<string, string> ReadOnly(IReadOnlyDictionary<string, string>? values)
    {
        if (values is null || values.Count == 0)
        {
            return new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal));
        }

        return new ReadOnlyDictionary<string, string>(
            values
                .Where(static pair => !string.IsNullOrWhiteSpace(pair.Value))
                .ToDictionary(
                    static pair => pair.Key,
                    static pair => pair.Value.Trim(),
                    StringComparer.Ordinal));
    }
}

public sealed record FluxMetricValue(
    string Label,
    double Value,
    string Unit,
    string FormattedValue);
