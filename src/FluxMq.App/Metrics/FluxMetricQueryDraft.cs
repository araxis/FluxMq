using System.Globalization;

namespace FluxMq.App.Metrics;

public sealed class FluxMetricQueryDraft
{
    public const string AnyValue = "";

    private readonly FluxMetricCatalog _catalog;

    private FluxMetricQueryDraft(FluxMetricCatalog catalog)
    {
        _catalog = catalog;
        CurrentEventType = catalog.FindEventType(AnyValue);
    }

    public string Source { get; set; } = FluxMetricCatalog.RuntimeEventsSource;

    public string Aggregation
    {
        get => Measure;
        set => Measure = value;
    }

    public string Measure { get; set; } = FluxMetricCatalog.MeasureCount;

    public string Window { get; set; } = "60s";

    public string EventType { get; private set; } = AnyValue;

    public string TopicStartsWith { get; set; } = AnyValue;

    public string TopicNotStartsWith { get; set; } = AnyValue;

    public string SubjectStartsWith { get; set; } = AnyValue;

    public string Status { get; set; } = AnyValue;

    public string Qos { get; set; } = AnyValue;

    public string Retain { get; set; } = AnyValue;

    public string SchemaId { get; set; } = AnyValue;

    public string GroupBy { get; set; } = AnyValue;

    public string Format { get; set; } = FluxMetricCatalog.FormatNumber;

    public FluxMetricEventTypeDescriptor CurrentEventType { get; private set; }

    public bool IsSourceCustomized { get; private set; }

    public bool IsFormatCustomized { get; private set; }

    public bool IsWindowCustomized { get; private set; }

    public bool HasValidWindow => TryNormalizeWindow(Window, out _);

    public string WindowValidationMessage => HasValidWindow
        ? string.Empty
        : "Use a positive window like 30s, 1m, or 2h.";

    public static FluxMetricQueryDraft Create(
        FluxMetricDefinition? metric,
        IReadOnlyDictionary<string, string>? legacyFilters = null,
        string metricName = "metric",
        FluxMetricCatalog? catalog = null)
    {
        catalog ??= FluxMetricCatalog.Shared;
        var draft = new FluxMetricQueryDraft(catalog);
        draft.ApplyDefinition(catalog.CreateDefaultDefinition(metricName));

        if (metric is not null)
        {
            draft.ApplyDefinition(metric);
            draft.IsSourceCustomized = true;
            draft.IsFormatCustomized = true;
            draft.IsWindowCustomized = true;
        }

        if (legacyFilters is not null)
        {
            draft.ApplyLegacyFilters(legacyFilters, fillOnlyMissing: metric is not null);
        }

        draft.NormalizeMeasureCompatibility(forceCompatibleSource: true);
        draft.ResetStatusWhenUnsupported();
        draft.TrimFiltersToEventType();
        return draft;
    }

    public void Reset(string metricName = "metric")
    {
        ApplyDefinition(_catalog.CreateDefaultDefinition(metricName));
        IsSourceCustomized = false;
        IsFormatCustomized = false;
        IsWindowCustomized = false;
        NormalizeMeasureCompatibility(forceCompatibleSource: true);
        ResetStatusWhenUnsupported();
        TrimFiltersToEventType();
    }

    public void SetAggregation(string? value)
        => SetMeasure(value);

    public void SetMeasure(string? value)
    {
        var previousMeasure = _catalog.FindMeasure(Measure);
        var nextMeasure = _catalog.FindMeasure(value);
        Measure = nextMeasure.Id;

        if (!IsSourceCustomized || !_catalog.IsSourceCompatible(Measure, Source))
        {
            Source = nextMeasure.DefaultSource;
        }

        if (!IsFormatCustomized ||
            string.Equals(Format, previousMeasure.DefaultFormat, StringComparison.Ordinal))
        {
            Format = nextMeasure.DefaultFormat;
        }

        if (string.IsNullOrWhiteSpace(EventType) &&
            !string.IsNullOrWhiteSpace(nextMeasure.DefaultEventType))
        {
            SetEventType(nextMeasure.DefaultEventType);
        }
    }

    public void SetSource(string? value)
    {
        var normalized = Normalize(value, _catalog.FindMeasure(Measure).DefaultSource);
        if (!_catalog.IsSourceCompatible(Measure, normalized))
        {
            return;
        }

        Source = normalized;
        IsSourceCustomized = true;
    }

    public void SetWindow(string? value)
    {
        Window = value ?? string.Empty;
        IsWindowCustomized = true;
    }

    public void SetFormat(string? value)
    {
        Format = _catalog.NormalizeFormat(Measure, value);
        IsFormatCustomized = true;
    }

    public void SetEventType(string? value)
    {
        EventType = Normalize(value);
        CurrentEventType = _catalog.FindEventType(EventType);
        ResetStatusWhenUnsupported();
        TrimFiltersToEventType();
    }

    public string GetFilterValue(string key)
        => string.Equals(key, FluxMetricCatalog.TopicStartsWithKey, StringComparison.Ordinal)
            ? TopicStartsWith
            : string.Equals(key, FluxMetricCatalog.TopicNotStartsWithKey, StringComparison.Ordinal)
                ? TopicNotStartsWith
                : string.Equals(key, FluxMetricCatalog.SubjectStartsWithKey, StringComparison.Ordinal)
                    ? SubjectStartsWith
                    : string.Equals(key, FluxMetricCatalog.AttributeFilterKey("qos"), StringComparison.Ordinal)
                        ? Qos
                        : string.Equals(key, FluxMetricCatalog.AttributeFilterKey("retain"), StringComparison.Ordinal)
                            ? Retain
                            : string.Equals(key, FluxMetricCatalog.AttributeFilterKey("schemaId"), StringComparison.Ordinal)
                                ? SchemaId
                                : AnyValue;

    public void SetFilterValue(string key, string? value)
    {
        if (string.Equals(key, FluxMetricCatalog.TopicStartsWithKey, StringComparison.Ordinal))
        {
            TopicStartsWith = Normalize(value);
        }
        else if (string.Equals(key, FluxMetricCatalog.TopicNotStartsWithKey, StringComparison.Ordinal))
        {
            TopicNotStartsWith = Normalize(value);
        }
        else if (string.Equals(key, FluxMetricCatalog.SubjectStartsWithKey, StringComparison.Ordinal))
        {
            SubjectStartsWith = Normalize(value);
        }
        else if (string.Equals(key, FluxMetricCatalog.AttributeFilterKey("qos"), StringComparison.Ordinal))
        {
            Qos = Normalize(value);
        }
        else if (string.Equals(key, FluxMetricCatalog.AttributeFilterKey("retain"), StringComparison.Ordinal))
        {
            Retain = Normalize(value);
        }
        else if (string.Equals(key, FluxMetricCatalog.AttributeFilterKey("schemaId"), StringComparison.Ordinal))
        {
            SchemaId = Normalize(value);
        }
    }

    public void ClearFilter(string key)
    {
        if (string.Equals(key, FluxMetricCatalog.EventTypeKey, StringComparison.Ordinal))
        {
            SetEventType(AnyValue);
        }
        else if (string.Equals(key, FluxMetricCatalog.StatusKey, StringComparison.Ordinal))
        {
            Status = AnyValue;
        }
        else
        {
            SetFilterValue(key, AnyValue);
        }
    }

    public FluxMetricDefinition BuildDefinition(string metricName = "metric")
        => new(
            metricName,
            _catalog.NormalizeSource(Measure, Source),
            _catalog.FindMeasure(Measure).Id,
            TryNormalizeWindow(Window, out var normalizedWindow) ? normalizedWindow : "60s",
            eventType: Normalize(EventType),
            topicStartsWith: Normalize(TopicStartsWith),
            topicNotStartsWith: Normalize(TopicNotStartsWith),
            status: NormalizeStatusForCurrentEvent(),
            groupBy: Normalize(GroupBy),
            format: _catalog.NormalizeFormat(Measure, Format),
            additionalFilters: BuildAdditionalFilters());

    public static bool TryNormalizeWindow(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value.Trim().ToLowerInvariant();
        var suffix = candidate[^1];
        if (suffix is not ('s' or 'm' or 'h'))
        {
            return false;
        }

        var numberText = candidate[..^1];
        if (!double.TryParse(numberText, NumberStyles.Float, CultureInfo.InvariantCulture, out var amount) ||
            amount <= 0)
        {
            return false;
        }

        var seconds = suffix switch
        {
            'h' => amount * 3600d,
            'm' => amount * 60d,
            _ => amount
        };
        if (seconds is < 1d or > 86_400d)
        {
            return false;
        }

        var amountText = amount % 1d == 0d
            ? amount.ToString("0", CultureInfo.InvariantCulture)
            : amount.ToString("0.##", CultureInfo.InvariantCulture);
        normalized = $"{amountText}{suffix}";
        return true;
    }

    private void ApplyDefinition(FluxMetricDefinition metric)
    {
        Source = Normalize(metric.Source, Source);
        Measure = Normalize(metric.Measure, Measure);
        Window = Normalize(metric.Window, Window);
        GroupBy = Normalize(metric.GroupBy);
        Format = Normalize(metric.Format, Format);
        SetEventType(metric.EventType);
        TopicStartsWith = Normalize(metric.TopicStartsWith);
        TopicNotStartsWith = Normalize(metric.TopicNotStartsWith);
        SubjectStartsWith = ReadAdditionalFilter(metric, FluxMetricCatalog.SubjectStartsWithKey);
        Status = Normalize(metric.Status);
        Qos = ReadAdditionalFilter(metric, FluxMetricCatalog.AttributeFilterKey("qos"));
        Retain = ReadAdditionalFilter(metric, FluxMetricCatalog.AttributeFilterKey("retain"));
        SchemaId = ReadAdditionalFilter(metric, FluxMetricCatalog.AttributeFilterKey("schemaId"));
        NormalizeMeasureCompatibility(forceCompatibleSource: false);
    }

    private void ApplyLegacyFilters(IReadOnlyDictionary<string, string> legacyFilters, bool fillOnlyMissing)
    {
        if (ShouldApplyLegacy(EventType, fillOnlyMissing))
        {
            SetEventType(ReadString(legacyFilters, FluxMetricCatalog.EventTypeKey));
        }

        if (ShouldApplyLegacy(Status, fillOnlyMissing))
        {
            Status = Normalize(ReadString(legacyFilters, FluxMetricCatalog.StatusKey));
        }

        ApplyLegacyFilter(legacyFilters, FluxMetricCatalog.TopicStartsWithKey, fillOnlyMissing);
        ApplyLegacyFilter(legacyFilters, FluxMetricCatalog.TopicNotStartsWithKey, fillOnlyMissing);
        ApplyLegacyFilter(legacyFilters, FluxMetricCatalog.SubjectStartsWithKey, fillOnlyMissing);
        ApplyLegacyFilter(legacyFilters, FluxMetricCatalog.AttributeFilterKey("qos"), fillOnlyMissing);
        ApplyLegacyFilter(legacyFilters, FluxMetricCatalog.AttributeFilterKey("retain"), fillOnlyMissing);
        ApplyLegacyFilter(legacyFilters, FluxMetricCatalog.AttributeFilterKey("schemaId"), fillOnlyMissing);
    }

    private void ApplyLegacyFilter(IReadOnlyDictionary<string, string> legacyFilters, string key, bool fillOnlyMissing)
    {
        if (ShouldApplyLegacy(GetFilterValue(key), fillOnlyMissing))
        {
            SetFilterValue(key, ReadString(legacyFilters, key));
        }
    }

    private static bool ShouldApplyLegacy(string currentValue, bool fillOnlyMissing)
        => !fillOnlyMissing || string.IsNullOrWhiteSpace(currentValue);

    private IReadOnlyDictionary<string, string> BuildAdditionalFilters()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        AddIfPresent(result, FluxMetricCatalog.SubjectStartsWithKey, SubjectStartsWith);
        AddIfPresent(result, FluxMetricCatalog.AttributeFilterKey("qos"), Qos);
        AddIfPresent(result, FluxMetricCatalog.AttributeFilterKey("retain"), Retain);
        AddIfPresent(result, FluxMetricCatalog.AttributeFilterKey("schemaId"), SchemaId);
        return result;
    }

    private void TrimFiltersToEventType()
    {
        var activeFieldKeys = CurrentEventType.Fields.Select(static field => field.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var key in _catalog.FilterKeys.Where(key => !activeFieldKeys.Contains(key)))
        {
            SetFilterValue(key, AnyValue);
        }
    }

    private void ResetStatusWhenUnsupported()
    {
        if (!FluxMetricCatalog.ShouldExposeStatus(CurrentEventType) ||
            !CurrentEventType.StatusOptions.Any(option => string.Equals(option.Value, Status, StringComparison.Ordinal)))
        {
            Status = AnyValue;
        }
    }

    private string NormalizeStatusForCurrentEvent()
        => FluxMetricCatalog.ShouldExposeStatus(CurrentEventType)
            ? Normalize(Status)
            : AnyValue;

    private void NormalizeMeasureCompatibility(bool forceCompatibleSource)
    {
        Measure = _catalog.FindMeasure(Measure).Id;
        Format = _catalog.NormalizeFormat(Measure, Format);
        if (forceCompatibleSource || !_catalog.IsSourceCompatible(Measure, Source))
        {
            Source = _catalog.NormalizeSource(Measure, Source);
        }
    }

    private static string ReadAdditionalFilter(FluxMetricDefinition metric, string key)
        => metric.AdditionalFilters.TryGetValue(key, out var value)
            ? Normalize(value)
            : AnyValue;

    private static string? ReadString(IReadOnlyDictionary<string, string> values, string key)
        => values.TryGetValue(key, out var value) ? value : null;

    private static void AddIfPresent(Dictionary<string, string> target, string key, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            target[key] = value.Trim();
        }
    }

    private static string Normalize(string? value, string fallback = AnyValue)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}
