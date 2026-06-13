using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;

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

    [JsonConverter(typeof(FluxMetricAdditionalFiltersJsonConverter))]
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

internal sealed class FluxMetricAdditionalFiltersJsonConverter : JsonConverter<IReadOnlyDictionary<string, string>>
{
    public override IReadOnlyDictionary<string, string> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal));
        }

        using var document = JsonDocument.ParseValue(ref reader);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Metric additional filters must be an object.");
        }

        var filters = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (string.Equals(property.Name, "attributes", StringComparison.Ordinal) &&
                property.Value.ValueKind == JsonValueKind.Object)
            {
                foreach (var attribute in property.Value.EnumerateObject())
                {
                    if (TryReadScalar(attribute.Value, out var value))
                    {
                        filters[FluxMetricCatalog.AttributeFilterKey(attribute.Name)] = value;
                    }
                }

                continue;
            }

            if (TryReadScalar(property.Value, out var filterValue))
            {
                filters[property.Name] = filterValue;
            }
        }

        return new ReadOnlyDictionary<string, string>(filters);
    }

    public override void Write(
        Utf8JsonWriter writer,
        IReadOnlyDictionary<string, string> value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var (key, filterValue) in value.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(filterValue))
            {
                writer.WriteString(key, filterValue.Trim());
            }
        }

        writer.WriteEndObject();
    }

    private static bool TryReadScalar(JsonElement element, out string value)
    {
        value = element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => string.Empty
        };

        value = value.Trim();
        return !string.IsNullOrWhiteSpace(value);
    }
}

public sealed record FluxMetricValue(
    string Label,
    double Value,
    string Unit,
    string FormattedValue);
