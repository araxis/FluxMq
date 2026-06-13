namespace FluxMq.App.Metrics;

/// <summary>The numeric value a metric produces.</summary>
public enum MetricValueKind
{
    Int,
    Double
}

/// <summary>How a single metric parameter is edited in the UI.</summary>
public enum MetricParamKind
{
    Text,
    Topic,
    Integer,
    Number,
    Toggle,
    Duration,
    Select
}

/// <summary>One choice for a <see cref="MetricParamKind.Select"/> parameter.</summary>
public sealed record MetricParamOption(string Value, string Label);

/// <summary>
/// One parameter a metric exposes. The metric declares these explicitly (no reflection): the UI renders
/// an editor from <see cref="Kind"/>, and the metric parses the stored string value back into a typed field.
/// </summary>
public sealed record MetricParamSpec(
    string Key,
    string DisplayName,
    MetricParamKind Kind,
    bool Required = false,
    string? DefaultValue = null,
    int? Min = null,
    int? Max = null,
    string? Placeholder = null,
    string? HelpText = null,
    IReadOnlyList<MetricParamOption>? Options = null);

/// <summary>
/// The full description of a metric kind: its stable id, display metadata, value kind, unit/format,
/// and the parameters it needs. The catalog lists these and the UI builds the add-metric form from them.
/// </summary>
public sealed record MetricDescriptor(
    string TypeId,
    string DisplayName,
    string Description,
    MetricValueKind ValueKind,
    string Unit,
    string Format,
    IReadOnlyList<MetricParamSpec> Parameters);

/// <summary>Terse, explicit factory helpers so a metric can declare its parameters in one readable line each.</summary>
public static class MetricParam
{
    public static MetricParamSpec Topic(string key, string displayName, bool required = false, string? placeholder = null, string? helpText = null)
        => new(key, displayName, MetricParamKind.Topic, required, Placeholder: placeholder,
            HelpText: helpText ?? "MQTT topic filter. + matches one level, # matches the rest.");

    public static MetricParamSpec Integer(string key, string displayName, int? min = null, int? max = null, string? defaultValue = null, bool required = false, string? helpText = null)
        => new(key, displayName, MetricParamKind.Integer, required, defaultValue, min, max, HelpText: helpText);

    public static MetricParamSpec Number(string key, string displayName, string? defaultValue = null, bool required = false, string? helpText = null)
        => new(key, displayName, MetricParamKind.Number, required, defaultValue, HelpText: helpText);

    public static MetricParamSpec Text(string key, string displayName, bool required = false, string? placeholder = null, string? helpText = null)
        => new(key, displayName, MetricParamKind.Text, required, Placeholder: placeholder, HelpText: helpText);

    public static MetricParamSpec Toggle(string key, string displayName, string? defaultValue = null, string? helpText = null)
        => new(key, displayName, MetricParamKind.Toggle, DefaultValue: defaultValue, HelpText: helpText);

    public static MetricParamSpec Duration(string key, string displayName, string defaultValue = "60s", string? helpText = null)
        => new(key, displayName, MetricParamKind.Duration, DefaultValue: defaultValue,
            Placeholder: "60s", HelpText: helpText ?? "Rolling window, for example 30s, 1m, or 2h.");

    public static MetricParamSpec Select(string key, string displayName, IReadOnlyList<MetricParamOption> options, string? defaultValue = null, string? helpText = null)
        => new(key, displayName, MetricParamKind.Select, DefaultValue: defaultValue, Options: options, HelpText: helpText);
}
