namespace FluxMq.App.Metrics;

/// <summary>
/// A selectable value/label pair for a metric parameter rendered as a dropdown.
/// </summary>
public sealed record FluxMetricParameterOption(string Value, string Label);

/// <summary>
/// Describes a single editable parameter that a metric type owns. The Metrics page and dashboard metric
/// selector render one row per descriptor; the type interprets the configured values when it builds a source.
/// </summary>
public sealed record FluxMetricParameterDescriptor
{
    private IReadOnlyList<FluxMetricParameterOption>? _options;

    public required string Key { get; init; }

    public required string Label { get; init; }

    public string Kind { get; init; } = FluxMetricParameterKinds.Text;

    public string DefaultValue { get; init; } = string.Empty;

    public bool Required { get; init; }

    public string HelperText { get; init; } = string.Empty;

    public string? Placeholder { get; init; }

    public IReadOnlyList<FluxMetricParameterOption> Options
    {
        get => _options ??= [];
        init => _options = value ?? [];
    }
}
