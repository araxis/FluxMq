using FluxMq.UI.Models;
using FluxMq.UI.Services;
using System.Globalization;

namespace FluxMq.UI.Components.Workspace;

public sealed class DashboardWidgetSettingsDraft
{
    private const string AnyValue = "";
    private readonly DashboardEventFilterCatalog _eventFilters;
    private readonly Dictionary<string, string> _filterValues = new(StringComparer.Ordinal);

    private DashboardWidgetSettingsDraft(
        DashboardWidgetSnapshot widget,
        DashboardEventFilterCatalog eventFilters)
    {
        _eventFilters = eventFilters;
        Profile = DashboardWidgetSettingsProfiles.For(widget.Type);
        Title = widget.ReadString("title") ?? Profile.Title;
        Subtitle = widget.ReadString("subtitle") ?? DefaultSubtitle(Profile.Type);
        ExcludeSystemTopics = !string.Equals(
            widget.ReadString(DashboardWidgetCatalog.ExcludeSystemTopicsKey),
            "false",
            StringComparison.OrdinalIgnoreCase);
        EventType = widget.ReadString(DashboardEventFilterCatalog.EventTypeKey) ?? AnyValue;
        Status = widget.ReadString(DashboardEventFilterCatalog.StatusKey) ?? AnyValue;
        MetricName = widget.ReadString("metric") ?? string.Empty;
        MetricVisualization = DashboardMetricVisualizationSettingsDraft.Create(widget);
        PrimaryMetric = DashboardWidgetCatalog.NormalizePrimaryMetric(
            widget.ReadString(DashboardWidgetCatalog.PrimaryMetricKey));
        GaugeStyle = DashboardEventGaugeWidgetOptions.NormalizeStyle(
            widget.ReadString(DashboardEventGaugeWidgetOptions.StyleKey));
        GaugeMin = ReadString(widget.Configuration, DashboardEventGaugeWidgetOptions.MinKey) ?? DashboardEventGaugeWidgetOptions.DefaultMin;
        GaugeMax = ReadString(widget.Configuration, DashboardEventGaugeWidgetOptions.MaxKey) ?? DashboardEventGaugeWidgetOptions.DefaultMax;
        GaugeTarget = ReadString(widget.Configuration, DashboardEventGaugeWidgetOptions.TargetKey) ?? DashboardEventGaugeWidgetOptions.DefaultTarget;
        GaugeWarning = ReadString(widget.Configuration, DashboardEventGaugeWidgetOptions.WarningKey) ?? DashboardEventGaugeWidgetOptions.DefaultWarning;
        GaugeCritical = ReadString(widget.Configuration, DashboardEventGaugeWidgetOptions.CriticalKey) ?? DashboardEventGaugeWidgetOptions.DefaultCritical;
        GaugeNormalColor = ReadString(widget.Configuration, DashboardEventGaugeWidgetOptions.NormalColorKey) ?? DashboardEventGaugeWidgetOptions.DefaultNormalColor;
        GaugeWarningColor = ReadString(widget.Configuration, DashboardEventGaugeWidgetOptions.WarningColorKey) ?? DashboardEventGaugeWidgetOptions.DefaultWarningColor;
        GaugeCriticalColor = ReadString(widget.Configuration, DashboardEventGaugeWidgetOptions.CriticalColorKey) ?? DashboardEventGaugeWidgetOptions.DefaultCriticalColor;
        ChartType = DashboardChartWidgetOptions.NormalizeType(
            widget.ReadString(DashboardChartWidgetOptions.TypeKey));
        MetricCardColumns = DashboardWidgetCatalog.NormalizeMetricCardColumns(
            widget.ReadString(DashboardWidgetCatalog.MetricCardColumnsKey));
        DisplayMetrics = [.. DashboardWidgetCatalog.NormalizeDisplayMetrics(
            widget.ReadString(DashboardWidgetCatalog.DisplayMetricsKey))];
        ApplyLatestEventVisualValues(widget.Configuration);

        foreach (var key in eventFilters.FilterKeys)
        {
            _filterValues[key] = widget.ReadString(key) ?? string.Empty;
        }

        TrimFiltersToEventType();
        ResetStatusWhenUnsupported();
    }

    public DashboardWidgetSettingsProfile Profile { get; }

    public string Title { get; set; }

    public string Subtitle { get; set; }

    public string EventType { get; private set; }

    public string Status { get; set; }

    public string MetricName { get; set; }

    public DashboardMetricVisualizationSettingsDraft MetricVisualization { get; }

    public string MetricVisualizationId
    {
        get => MetricVisualization.VisualizationId;
        set => MetricVisualization.SetVisualization(value, applyDefaults: false);
    }

    public bool ExcludeSystemTopics { get; set; }

    public string PrimaryMetric { get; set; }

    public string GaugeStyle { get; set; }

    public string GaugeMin { get; set; }

    public string GaugeMax { get; set; }

    public string GaugeTarget { get; set; }

    public string GaugeWarning { get; set; }

    public string GaugeCritical { get; set; }

    public string GaugeNormalColor { get; set; }

    public string GaugeWarningColor { get; set; }

    public string GaugeCriticalColor { get; set; }

    public string ChartType { get; set; }

    public int MetricCardColumns { get; set; }

    public List<string> DisplayMetrics { get; }

    public string LatestHeader { get; set; } = DashboardLatestEventVisualOptions.DefaultHeader;

    public bool LatestShowHeader { get; set; } = true;

    public bool LatestShowType { get; set; } = true;

    public bool LatestShowTopic { get; set; } = true;

    public bool LatestShowStatus { get; set; } = true;

    public bool LatestShowTimestamp { get; set; } = true;

    public bool LatestShowPayload { get; set; } = true;

    public string LatestEmptyText { get; set; } = DashboardLatestEventVisualOptions.DefaultEmptyText;

    public string LatestHeaderColor { get; set; } = DashboardLatestEventVisualOptions.DefaultHeaderColor;

    public string LatestDetailColor { get; set; } = DashboardLatestEventVisualOptions.DefaultDetailColor;

    public string LatestPayloadColor { get; set; } = DashboardLatestEventVisualOptions.DefaultPayloadColor;

    public bool IsKpiTile => string.Equals(Profile.Type, DashboardWidgetCatalog.KpiTileType, StringComparison.Ordinal);

    public bool UsesMetricQueryBuilder =>
        IsKpiTile ||
        string.Equals(Profile.Type, DashboardWidgetCatalog.StatusValueType, StringComparison.Ordinal) ||
        string.Equals(Profile.Type, DashboardWidgetCatalog.EventGaugeType, StringComparison.Ordinal) ||
        string.Equals(Profile.Type, DashboardWidgetCatalog.RateTileType, StringComparison.Ordinal) ||
        string.Equals(Profile.Type, DashboardWidgetCatalog.EventCounterType, StringComparison.Ordinal) ||
        string.Equals(Profile.Type, DashboardWidgetCatalog.EventRateType, StringComparison.Ordinal);

    public DashboardEventTypeDescriptor CurrentEventType => _eventFilters.Find(EventType);

    public static DashboardWidgetSettingsDraft Create(
        DashboardWidgetSnapshot widget,
        DashboardEventFilterCatalog eventFilters)
        => new(widget, eventFilters);

    public void ResetToDefaultConfiguration(IReadOnlyDictionary<string, string> configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        Title = ReadString(configuration, "title") ?? Profile.Title;
        Subtitle = ReadString(configuration, "subtitle") ?? DefaultSubtitle(Profile.Type);
        ExcludeSystemTopics = !string.Equals(
            ReadString(configuration, DashboardWidgetCatalog.ExcludeSystemTopicsKey),
            "false",
            StringComparison.OrdinalIgnoreCase);
        EventType = ReadString(configuration, DashboardEventFilterCatalog.EventTypeKey) ?? AnyValue;
        Status = ReadString(configuration, DashboardEventFilterCatalog.StatusKey) ?? AnyValue;
        MetricName = ReadString(configuration, "metric") ?? MetricName;
        MetricVisualization.ApplyConfiguration(configuration);
        PrimaryMetric = DashboardWidgetCatalog.NormalizePrimaryMetric(
            ReadString(configuration, DashboardWidgetCatalog.PrimaryMetricKey));
        GaugeStyle = DashboardEventGaugeWidgetOptions.NormalizeStyle(
            ReadString(configuration, DashboardEventGaugeWidgetOptions.StyleKey));
        GaugeMin = ReadString(configuration, DashboardEventGaugeWidgetOptions.MinKey) ?? DashboardEventGaugeWidgetOptions.DefaultMin;
        GaugeMax = ReadString(configuration, DashboardEventGaugeWidgetOptions.MaxKey) ?? DashboardEventGaugeWidgetOptions.DefaultMax;
        GaugeTarget = ReadString(configuration, DashboardEventGaugeWidgetOptions.TargetKey) ?? DashboardEventGaugeWidgetOptions.DefaultTarget;
        GaugeWarning = ReadString(configuration, DashboardEventGaugeWidgetOptions.WarningKey) ?? DashboardEventGaugeWidgetOptions.DefaultWarning;
        GaugeCritical = ReadString(configuration, DashboardEventGaugeWidgetOptions.CriticalKey) ?? DashboardEventGaugeWidgetOptions.DefaultCritical;
        GaugeNormalColor = ReadString(configuration, DashboardEventGaugeWidgetOptions.NormalColorKey) ?? DashboardEventGaugeWidgetOptions.DefaultNormalColor;
        GaugeWarningColor = ReadString(configuration, DashboardEventGaugeWidgetOptions.WarningColorKey) ?? DashboardEventGaugeWidgetOptions.DefaultWarningColor;
        GaugeCriticalColor = ReadString(configuration, DashboardEventGaugeWidgetOptions.CriticalColorKey) ?? DashboardEventGaugeWidgetOptions.DefaultCriticalColor;
        ChartType = DashboardChartWidgetOptions.NormalizeType(
            ReadString(configuration, DashboardChartWidgetOptions.TypeKey));
        MetricCardColumns = DashboardWidgetCatalog.NormalizeMetricCardColumns(
            ReadString(configuration, DashboardWidgetCatalog.MetricCardColumnsKey));
        DisplayMetrics.Clear();
        DisplayMetrics.AddRange(DashboardWidgetCatalog.NormalizeDisplayMetrics(
            ReadString(configuration, DashboardWidgetCatalog.DisplayMetricsKey)));
        ApplyLatestEventVisualValues(configuration);

        foreach (var key in _eventFilters.FilterKeys)
        {
            _filterValues[key] = ReadString(configuration, key) ?? string.Empty;
        }

        TrimFiltersToEventType();
        ResetStatusWhenUnsupported();
    }

    public void SetEventType(string? value)
    {
        EventType = Normalize(value);
        TrimFiltersToEventType();
        ResetStatusWhenUnsupported();
    }

    public bool IsDisplayMetricSelected(string id)
        => DisplayMetrics.Contains(id, StringComparer.Ordinal);

    public IReadOnlyList<DashboardMetricDescriptor> AvailableDisplayMetrics
        => DashboardWidgetCatalog.MetricOptions
            .Where(option => !DisplayMetrics.Contains(option.Id, StringComparer.Ordinal))
            .ToArray();

    public bool CanRemoveDisplayMetric => DisplayMetrics.Count > 1;

    public void AddDisplayMetric(string? id)
    {
        var normalized = Normalize(id);
        if (string.IsNullOrWhiteSpace(normalized) ||
            IsDisplayMetricSelected(normalized) ||
            !DashboardWidgetCatalog.MetricOptions.Any(option => string.Equals(option.Id, normalized, StringComparison.Ordinal)))
        {
            return;
        }

        DisplayMetrics.Add(normalized);
    }

    public void RemoveDisplayMetric(string id)
    {
        if (DisplayMetrics.Count <= 1)
        {
            return;
        }

        DisplayMetrics.RemoveAll(metric => string.Equals(metric, id, StringComparison.Ordinal));
        if (!DisplayMetrics.Contains(PrimaryMetric, StringComparer.Ordinal))
        {
            PrimaryMetric = DisplayMetrics[0];
        }
    }

    public void MoveDisplayMetric(string id, int offset)
    {
        if (offset == 0)
        {
            return;
        }

        var index = DisplayMetrics.FindIndex(metric => string.Equals(metric, id, StringComparison.Ordinal));
        if (index < 0)
        {
            return;
        }

        var next = Math.Clamp(index + offset, 0, DisplayMetrics.Count - 1);
        if (next == index)
        {
            return;
        }

        var item = DisplayMetrics[index];
        DisplayMetrics.RemoveAt(index);
        DisplayMetrics.Insert(next, item);
    }

    public void SetPrimaryMetric(string? id)
    {
        var normalized = DashboardWidgetCatalog.NormalizePrimaryMetric(id);
        PrimaryMetric = normalized;
        if (!DisplayMetrics.Contains(normalized, StringComparer.Ordinal))
        {
            DisplayMetrics.Insert(0, normalized);
        }
    }

    public void ToggleDisplayMetric(string id, bool selected)
    {
        if (selected)
        {
            AddDisplayMetric(id);

            return;
        }

        RemoveDisplayMetric(id);
    }

    public string GetFilterValue(string key)
        => _filterValues.TryGetValue(key, out var value) ? value : string.Empty;

    public void SetFilterValue(string key, string? value)
        => _filterValues[key] = Normalize(value);

    public void SetLatestEventVisualValue(string key, string? value)
    {
        if (!Profile.UsesLatestEventVisual)
        {
            return;
        }

        switch (key)
        {
            case DashboardLatestEventVisualOptions.HeaderKey:
                LatestHeader = NormalizeText(value, DashboardLatestEventVisualOptions.DefaultHeader);
                Title = LatestHeader;
                break;
            case DashboardLatestEventVisualOptions.ShowHeaderKey:
                LatestShowHeader = NormalizeBoolean(value, LatestShowHeader);
                break;
            case DashboardLatestEventVisualOptions.ShowTypeKey:
                LatestShowType = NormalizeBoolean(value, LatestShowType);
                break;
            case DashboardLatestEventVisualOptions.ShowTopicKey:
                LatestShowTopic = NormalizeBoolean(value, LatestShowTopic);
                break;
            case DashboardLatestEventVisualOptions.ShowStatusKey:
                LatestShowStatus = NormalizeBoolean(value, LatestShowStatus);
                break;
            case DashboardLatestEventVisualOptions.ShowTimestampKey:
                LatestShowTimestamp = NormalizeBoolean(value, LatestShowTimestamp);
                break;
            case DashboardLatestEventVisualOptions.ShowPayloadKey:
                LatestShowPayload = NormalizeBoolean(value, LatestShowPayload);
                break;
            case DashboardLatestEventVisualOptions.EmptyTextKey:
                LatestEmptyText = NormalizeText(value, DashboardLatestEventVisualOptions.DefaultEmptyText);
                break;
            case DashboardLatestEventVisualOptions.HeaderColorKey:
                LatestHeaderColor = Normalize(value);
                break;
            case DashboardLatestEventVisualOptions.DetailColorKey:
                LatestDetailColor = Normalize(value);
                break;
            case DashboardLatestEventVisualOptions.PayloadColorKey:
                LatestPayloadColor = Normalize(value);
                break;
        }
    }

    public IReadOnlyDictionary<string, string> BuildConfiguration()
    {
        var title = string.IsNullOrWhiteSpace(Title) ? Profile.Title : Title.Trim();
        var subtitle = string.IsNullOrWhiteSpace(Subtitle) ? DefaultSubtitle(Profile.Type) : Subtitle.Trim();
        if (Profile.IsTopicTreeWidget)
        {
            var topicConfiguration = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["title"] = title,
                [DashboardWidgetCatalog.ExcludeSystemTopicsKey] = ExcludeSystemTopics ? "true" : "false"
            };
            ApplyMetricName(topicConfiguration);
            return topicConfiguration;
        }

        if (!Profile.IsEventWidget)
        {
            var basicConfiguration = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["title"] = title
            };
            ApplyMetricName(basicConfiguration);
            return basicConfiguration;
        }

        if (UsesMetricQueryBuilder)
        {
            var metricQueryConfiguration = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!IsKpiTile)
            {
                metricQueryConfiguration["title"] = title;
            }

            if (!IsKpiTile && Profile.UsesSubtitle)
            {
                metricQueryConfiguration["subtitle"] = subtitle;
            }

            ApplyMetricVisualizationConfiguration(metricQueryConfiguration);
            ApplyMetricVisualization(metricQueryConfiguration);
            ApplyGaugeConfiguration(metricQueryConfiguration);
            ApplyMetricName(metricQueryConfiguration);
            return metricQueryConfiguration;
        }

        var eventType = CurrentEventType;
        var status = eventType.StatusOptions.Any(option => string.Equals(option.Value, Status, StringComparison.Ordinal))
            ? Normalize(Status)
            : string.Empty;
        var configuration = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["title"] = title,
            [DashboardEventFilterCatalog.EventTypeKey] = Normalize(EventType),
            [DashboardEventFilterCatalog.StatusKey] = status
        };
        if (Profile.UsesSubtitle)
        {
            configuration["subtitle"] = subtitle;
        }

        var activeFieldKeys = eventType.Fields.Select(static field => field.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var key in _eventFilters.FilterKeys)
        {
            configuration[key] = activeFieldKeys.Contains(key)
                ? Normalize(GetFilterValue(key))
                : string.Empty;
        }

        ApplyVisualConfiguration(configuration);
        ApplyLatestEventVisualConfiguration(configuration);
        ApplyMetricVisualizationConfiguration(configuration);
        ApplyMetricVisualization(configuration);
        ApplyMetricName(configuration);
        return configuration;
    }

    private void ApplyMetricName(Dictionary<string, string> configuration)
    {
        if (!string.IsNullOrWhiteSpace(MetricName))
        {
            configuration["metric"] = MetricName.Trim();
        }
    }

    private void ApplyVisualConfiguration(Dictionary<string, string> configuration)
    {
        if (!Profile.UsesVisualMetrics)
        {
            return;
        }

        configuration[DashboardWidgetCatalog.PrimaryMetricKey] =
            DashboardWidgetCatalog.NormalizePrimaryMetric(PrimaryMetric);
        if (Profile.SupportsMetricSlots)
        {
            configuration[DashboardWidgetCatalog.DisplayMetricsKey] =
                DashboardWidgetCatalog.BuildDisplayMetrics(DisplayMetrics);
            configuration[DashboardWidgetCatalog.MetricCardColumnsKey] =
                DashboardWidgetCatalog.NormalizeMetricCardColumns(MetricCardColumns)
                    .ToString(CultureInfo.InvariantCulture);
        }

        if (Profile.UsesGaugeStyle)
        {
            configuration[DashboardEventGaugeWidgetOptions.StyleKey] =
                DashboardEventGaugeWidgetOptions.NormalizeStyle(GaugeStyle);
        }

        if (Profile.UsesChartType)
        {
            configuration[DashboardChartWidgetOptions.TypeKey] =
                DashboardChartWidgetOptions.NormalizeType(ChartType);
        }
    }

    private void ApplyMetricVisualization(Dictionary<string, string> configuration)
    {
        if (!Profile.UsesMetricVisualization)
        {
            return;
        }

        configuration[DashboardWidgetCatalog.MetricVisualizationKey] =
            DashboardWidgetCatalog.NormalizeMetricVisualization(MetricVisualizationId);
    }

    private void ApplyGaugeConfiguration(Dictionary<string, string> configuration)
    {
        if (Profile.UsesGaugeStyle)
        {
            configuration[DashboardEventGaugeWidgetOptions.StyleKey] =
                DashboardEventGaugeWidgetOptions.NormalizeStyle(GaugeStyle);
            configuration[DashboardEventGaugeWidgetOptions.MinKey] =
                NormalizeNumber(GaugeMin, DashboardEventGaugeWidgetOptions.DefaultMin);
            configuration[DashboardEventGaugeWidgetOptions.MaxKey] =
                NormalizeNumber(GaugeMax, DashboardEventGaugeWidgetOptions.DefaultMax);
            configuration[DashboardEventGaugeWidgetOptions.TargetKey] =
                NormalizeNumber(GaugeTarget, DashboardEventGaugeWidgetOptions.DefaultTarget);
            configuration[DashboardEventGaugeWidgetOptions.WarningKey] =
                NormalizeNumber(GaugeWarning, DashboardEventGaugeWidgetOptions.DefaultWarning);
            configuration[DashboardEventGaugeWidgetOptions.CriticalKey] =
                NormalizeNumber(GaugeCritical, DashboardEventGaugeWidgetOptions.DefaultCritical);
            configuration[DashboardEventGaugeWidgetOptions.NormalColorKey] =
                NormalizeColor(GaugeNormalColor, DashboardEventGaugeWidgetOptions.DefaultNormalColor);
            configuration[DashboardEventGaugeWidgetOptions.WarningColorKey] =
                NormalizeColor(GaugeWarningColor, DashboardEventGaugeWidgetOptions.DefaultWarningColor);
            configuration[DashboardEventGaugeWidgetOptions.CriticalColorKey] =
                NormalizeColor(GaugeCriticalColor, DashboardEventGaugeWidgetOptions.DefaultCriticalColor);
        }
    }

    private void ApplyMetricVisualizationConfiguration(Dictionary<string, string> configuration)
    {
        if (!Profile.UsesMetricVisualization)
        {
            return;
        }

        foreach (var (key, value) in MetricVisualization.BuildConfiguration())
        {
            configuration[key] = value;
        }
    }

    private void ApplyLatestEventVisualValues(IReadOnlyDictionary<string, string> configuration)
    {
        if (!Profile.UsesLatestEventVisual)
        {
            return;
        }

        LatestHeader = ReadString(configuration, DashboardLatestEventVisualOptions.HeaderKey) ??
            ReadString(configuration, "title") ??
            DashboardLatestEventVisualOptions.DefaultHeader;
        Title = LatestHeader;
        LatestShowHeader = ReadBoolean(configuration, DashboardLatestEventVisualOptions.ShowHeaderKey, true);
        LatestShowType = ReadBoolean(configuration, DashboardLatestEventVisualOptions.ShowTypeKey, true);
        LatestShowTopic = ReadBoolean(
            configuration,
            DashboardLatestEventVisualOptions.ShowTopicKey,
            ReadBoolean(configuration, DashboardLatestEventVisualOptions.LegacyShowTopicKey, true));
        LatestShowStatus = ReadBoolean(
            configuration,
            DashboardLatestEventVisualOptions.ShowStatusKey,
            ReadBoolean(configuration, DashboardLatestEventVisualOptions.LegacyShowStatusKey, true));
        LatestShowTimestamp = ReadBoolean(
            configuration,
            DashboardLatestEventVisualOptions.ShowTimestampKey,
            !string.Equals(
                ReadString(configuration, DashboardLatestEventVisualOptions.LegacyTimestampFormatKey),
                "hidden",
                StringComparison.OrdinalIgnoreCase));
        LatestShowPayload = ReadBoolean(
            configuration,
            DashboardLatestEventVisualOptions.ShowPayloadKey,
            ReadBoolean(configuration, DashboardLatestEventVisualOptions.LegacyShowPayloadKey, true));
        LatestEmptyText = ReadString(configuration, DashboardLatestEventVisualOptions.EmptyTextKey) ??
            DashboardLatestEventVisualOptions.DefaultEmptyText;
        LatestHeaderColor = ReadString(configuration, DashboardLatestEventVisualOptions.HeaderColorKey) ??
            DashboardLatestEventVisualOptions.DefaultHeaderColor;
        LatestDetailColor = ReadString(configuration, DashboardLatestEventVisualOptions.DetailColorKey) ??
            DashboardLatestEventVisualOptions.DefaultDetailColor;
        LatestPayloadColor = ReadString(configuration, DashboardLatestEventVisualOptions.PayloadColorKey) ??
            DashboardLatestEventVisualOptions.DefaultPayloadColor;
    }

    private void ApplyLatestEventVisualConfiguration(Dictionary<string, string> configuration)
    {
        if (!Profile.UsesLatestEventVisual)
        {
            return;
        }

        var header = string.IsNullOrWhiteSpace(LatestHeader)
            ? DashboardLatestEventVisualOptions.DefaultHeader
            : LatestHeader.Trim();
        configuration["title"] = header;
        configuration[DashboardLatestEventVisualOptions.HeaderKey] = header;
        configuration[DashboardLatestEventVisualOptions.ShowHeaderKey] = LatestShowHeader ? bool.TrueString : bool.FalseString;
        configuration[DashboardLatestEventVisualOptions.ShowTypeKey] = LatestShowType ? bool.TrueString : bool.FalseString;
        configuration[DashboardLatestEventVisualOptions.ShowTopicKey] = LatestShowTopic ? bool.TrueString : bool.FalseString;
        configuration[DashboardLatestEventVisualOptions.ShowStatusKey] = LatestShowStatus ? bool.TrueString : bool.FalseString;
        configuration[DashboardLatestEventVisualOptions.ShowTimestampKey] = LatestShowTimestamp ? bool.TrueString : bool.FalseString;
        configuration[DashboardLatestEventVisualOptions.ShowPayloadKey] = LatestShowPayload ? bool.TrueString : bool.FalseString;
        configuration[DashboardLatestEventVisualOptions.EmptyTextKey] = string.IsNullOrWhiteSpace(LatestEmptyText)
            ? DashboardLatestEventVisualOptions.DefaultEmptyText
            : LatestEmptyText.Trim();
        configuration[DashboardLatestEventVisualOptions.HeaderColorKey] = NormalizeColor(
            LatestHeaderColor,
            DashboardLatestEventVisualOptions.DefaultHeaderColor);
        configuration[DashboardLatestEventVisualOptions.DetailColorKey] = NormalizeColor(
            LatestDetailColor,
            DashboardLatestEventVisualOptions.DefaultDetailColor);
        configuration[DashboardLatestEventVisualOptions.PayloadColorKey] = NormalizeColor(
            LatestPayloadColor,
            DashboardLatestEventVisualOptions.DefaultPayloadColor);
    }

    private void TrimFiltersToEventType()
    {
        var activeFieldKeys = CurrentEventType.Fields.Select(static field => field.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var key in _eventFilters.FilterKeys.Where(key => !activeFieldKeys.Contains(key)))
        {
            _filterValues[key] = string.Empty;
        }
    }

    private void ResetStatusWhenUnsupported()
    {
        if (!CurrentEventType.StatusOptions.Any(option => string.Equals(option.Value, Status, StringComparison.Ordinal)))
        {
            Status = AnyValue;
        }
    }

    private static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string NormalizeText(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static bool NormalizeBoolean(string? value, bool fallback)
        => bool.TryParse(value, out var parsed) ? parsed : fallback;

    private static string NormalizeNumber(string? value, string fallback)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) &&
           double.IsFinite(parsed)
            ? parsed.ToString("0.###", CultureInfo.InvariantCulture)
            : fallback;

    private static string NormalizeColor(string? value, string fallback)
    {
        var normalized = Normalize(value).ToLowerInvariant();
        if (string.Equals(normalized, "transparent", StringComparison.Ordinal))
        {
            return normalized;
        }

        return normalized.Length is 4 or 5 or 7 or 9 &&
               normalized[0] == '#' &&
               normalized.Skip(1).All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f')
            ? normalized
            : fallback;
    }

    private static string? ReadString(IReadOnlyDictionary<string, string> configuration, string key)
        => configuration.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;

    private static bool ReadBoolean(IReadOnlyDictionary<string, string> configuration, string key, bool fallback)
        => configuration.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed)
            ? parsed
            : fallback;

    private static string DefaultSubtitle(string type)
        => string.Equals(type, DashboardWidgetCatalog.KpiTileType, StringComparison.Ordinal)
            ? "Total matching events"
            : string.Empty;
}
