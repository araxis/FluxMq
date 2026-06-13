namespace FluxMq.App.Metrics;

/// <summary>
/// An app-level metric resource: a focused metric instance selected by dashboards and pipelines.
/// The resource only carries identity plus a flat, type-owned parameter bag — there is no generic
/// measure/source/filters model and no nested <c>additionalFilters.attributes</c> shape.
/// </summary>
public sealed record FluxMetricResourceDefinition
{
    private Dictionary<string, string>? _parameters;
    private Dictionary<string, string>? _labels;

    public string Id { get; init; } = string.Empty;

    public string TypeId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = "Metric";

    public string Description { get; init; } = string.Empty;

    public Dictionary<string, string> Parameters
    {
        get => _parameters ??= new Dictionary<string, string>(StringComparer.Ordinal);
        init => _parameters = value ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }

    public Dictionary<string, string> Labels
    {
        get => _labels ??= new Dictionary<string, string>(StringComparer.Ordinal);
        init => _labels = value ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }

    public FluxMetricExportPolicy ExportPolicy { get; init; } = FluxMetricExportPolicy.Disabled;

    /// <summary>
    /// Reads a parameter value, returning <paramref name="fallback"/> when missing or blank.
    /// </summary>
    public string GetParameter(string key, string fallback = "")
        => Parameters.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : fallback;
}
