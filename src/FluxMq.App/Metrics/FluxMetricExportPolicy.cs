namespace FluxMq.App.Metrics;

public enum FluxMetricInstrumentKind
{
    Counter,
    Histogram,
    Gauge
}

/// <summary>
/// Optional OpenTelemetry export policy carried on a metric resource.
/// </summary>
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
