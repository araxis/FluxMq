namespace FluxMq.App.Metrics;

public sealed record FluxMetricQueryFilterChip(string Key, string Label);

public static class FluxMetricQuerySummary
{
    public static string Describe(FluxMetricDefinition definition, FluxMetricCatalog? catalog = null)
    {
        var sentence = DescribeSentence(definition, catalog);
        return sentence.StartsWith("Show ", StringComparison.Ordinal)
            ? sentence["Show ".Length..]
            : sentence;
    }

    public static string DescribeSentence(FluxMetricDefinition definition, FluxMetricCatalog? catalog = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        catalog ??= FluxMetricCatalog.Shared;

        var measure = catalog.FindMeasure(definition.Measure);
        var eventType = catalog.FindEventType(definition.EventType);
        var eventLabel = EventLabel(eventType);
        var window = WindowLabel(definition.Window);
        var filters = ActiveFilterChips(definition, catalog);
        var filterText = filters.Count == 0
            ? string.IsNullOrWhiteSpace(eventType.Value) ? "all events match" : "no filters"
            : string.Join(", ", filters.Select(static filter => filter.Label));

        return $"Show {measure.Label} of {eventLabel} during last {window} where {filterText}";
    }

    public static IReadOnlyList<string> ActiveFilters(FluxMetricDefinition definition, FluxMetricCatalog? catalog = null)
        => [.. ActiveFilterChips(definition, catalog).Select(static filter => filter.Label)];

    public static IReadOnlyList<FluxMetricQueryFilterChip> ActiveFilterChips(
        FluxMetricDefinition definition,
        FluxMetricCatalog? catalog = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        catalog ??= FluxMetricCatalog.Shared;

        var result = new List<FluxMetricQueryFilterChip>();
        if (!string.IsNullOrWhiteSpace(definition.TopicStartsWith))
        {
            result.Add(new(FluxMetricCatalog.TopicStartsWithKey, $"topic starts {definition.TopicStartsWith.Trim()}"));
        }

        if (!string.IsNullOrWhiteSpace(definition.TopicNotStartsWith))
        {
            result.Add(new(FluxMetricCatalog.TopicNotStartsWithKey, $"exclude {definition.TopicNotStartsWith.Trim()}"));
        }

        var eventType = catalog.FindEventType(definition.EventType);
        if (!string.IsNullOrWhiteSpace(definition.Status) &&
            FluxMetricCatalog.ShouldExposeStatus(eventType) &&
            eventType.StatusOptions.Any(option => string.Equals(option.Value, definition.Status.Trim(), StringComparison.Ordinal)) &&
            !FluxMetricCatalog.IsRedundantStatus(eventType, definition.Status))
        {
            result.Add(new(FluxMetricCatalog.StatusKey, $"status {definition.Status.Trim()}"));
        }

        foreach (var (key, value) in definition.AdditionalFilters.Where(static pair => !string.IsNullOrWhiteSpace(pair.Value)))
        {
            result.Add(new(key, FilterLabel(key, value, catalog, eventType)));
        }

        return result;
    }

    public static string WindowLabel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "1m";
        }

        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "30s" => "30s",
            "60s" => "1m",
            "300s" => "5m",
            "900s" => "15m",
            _ => normalized
        };
    }

    private static string EventLabel(FluxMetricEventTypeDescriptor descriptor)
    {
        if (string.IsNullOrWhiteSpace(descriptor.Value))
        {
            return "all runtime events";
        }

        return descriptor.Label switch
        {
            "MQTT message published" => "MQTT published messages",
            "MQTT message received" => "MQTT received messages",
            "MQTT message recorded" => "MQTT recorded messages",
            "File written" => "file writes",
            "JSON schema validated" => "schema validations",
            "Assertion evaluated" => "assertions",
            _ => descriptor.Label.ToLowerInvariant()
        };
    }

    private static string FilterLabel(
        string key,
        string value,
        FluxMetricCatalog catalog,
        FluxMetricEventTypeDescriptor eventType)
    {
        if (FluxMetricCatalog.TryGetAttributeName(key, out var attributeName))
        {
            return $"{AttributeLabel(attributeName)} {value.Trim()}";
        }

        var field = eventType.Fields
            .FirstOrDefault(field => string.Equals(field.Key, key, StringComparison.Ordinal)) ??
            catalog.EventTypes
                .SelectMany(static eventType => eventType.Fields)
                .FirstOrDefault(field => string.Equals(field.Key, key, StringComparison.Ordinal));
        return field is null
            ? $"{key} {value.Trim()}"
            : $"{field.Label.ToLowerInvariant()} {value.Trim()}";
    }

    private static string AttributeLabel(string attributeName)
        => attributeName switch
        {
            "qos" => "QoS",
            "retain" => "retain",
            "schemaId" => "schema id",
            _ => attributeName
        };
}
