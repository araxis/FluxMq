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
        MetricVisualizationId = DashboardWidgetCatalog.NormalizeMetricVisualization(
            widget.ReadString(DashboardWidgetCatalog.MetricVisualizationKey));
        PrimaryMetric = DashboardWidgetCatalog.NormalizePrimaryMetric(
            widget.ReadString(DashboardWidgetCatalog.PrimaryMetricKey));
        TitleColor = NormalizeColor(
            widget.ReadString(DashboardWidgetCatalog.KpiTitleColorKey) ?? widget.ReadString("style.titleColor"),
            DashboardWidgetCatalog.KpiDefaultTitleColor);
        SubtitleColor = NormalizeColor(
            widget.ReadString(DashboardWidgetCatalog.KpiSubtitleColorKey) ?? widget.ReadString("style.subtitleColor"),
            DashboardWidgetCatalog.KpiDefaultSubtitleColor);
        ValueColor = NormalizeColor(
            widget.ReadString(DashboardWidgetCatalog.KpiValueColorKey) ?? widget.ReadString("style.valueColor"),
            DashboardWidgetCatalog.KpiDefaultValueColor);
        TitleAlign = DashboardWidgetCatalog.NormalizeKpiHorizontalAlignment(
            widget.ReadString(DashboardWidgetCatalog.KpiTitleAlignKey));
        ValueAlign = DashboardWidgetCatalog.NormalizeKpiHorizontalAlignment(
            widget.ReadString(DashboardWidgetCatalog.KpiValueAlignKey));
        ValuePlacement = DashboardWidgetCatalog.NormalizeKpiValuePlacement(
            widget.ReadString(DashboardWidgetCatalog.KpiValuePlacementKey));
        DigitalStyle = DashboardWidgetCatalog.NormalizeMetricDigitalStyle(
            widget.ReadString(DashboardWidgetCatalog.MetricDigitalStyleKey));
        DigitalGlow = DashboardWidgetCatalog.NormalizeMetricDigitalGlow(
            widget.ReadString(DashboardWidgetCatalog.MetricDigitalGlowKey));
        DigitalBackgroundColor = NormalizeColor(
            widget.ReadString(DashboardWidgetCatalog.MetricDigitalBackgroundColorKey),
            DashboardWidgetCatalog.MetricDigitalDefaultBackgroundColor);
        DigitalSegmentColor = NormalizeColor(
            widget.ReadString(DashboardWidgetCatalog.MetricDigitalSegmentColorKey),
            DashboardWidgetCatalog.MetricDigitalDefaultSegmentColor);
        DigitalInactiveSegmentColor = NormalizeColor(
            widget.ReadString(DashboardWidgetCatalog.MetricDigitalInactiveSegmentColorKey),
            DashboardWidgetCatalog.MetricDigitalDefaultInactiveSegmentColor);
        DigitalLabelColor = NormalizeColor(
            widget.ReadString(DashboardWidgetCatalog.MetricDigitalLabelColorKey),
            DashboardWidgetCatalog.MetricDigitalDefaultLabelColor);
        DigitalDigits = DashboardWidgetCatalog.NormalizeMetricDigitalDigits(
            widget.ReadString(DashboardWidgetCatalog.MetricDigitalDigitsKey));
        GaugeStyle = DashboardWidgetCatalog.NormalizeGaugeStyle(
            widget.ReadString(DashboardWidgetCatalog.GaugeStyleKey));
        ChartType = DashboardWidgetCatalog.NormalizeChartType(
            widget.ReadString(DashboardWidgetCatalog.ChartTypeKey));
        MetricCardColumns = DashboardWidgetCatalog.NormalizeMetricCardColumns(
            widget.ReadString(DashboardWidgetCatalog.MetricCardColumnsKey));
        DisplayMetrics = [.. DashboardWidgetCatalog.NormalizeDisplayMetrics(
            widget.ReadString(DashboardWidgetCatalog.DisplayMetricsKey))];

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

    public string MetricVisualizationId { get; set; }

    public bool ExcludeSystemTopics { get; set; }

    public string PrimaryMetric { get; set; }

    public string TitleColor { get; set; }

    public string SubtitleColor { get; set; }

    public string ValueColor { get; set; }

    public string TitleAlign { get; set; }

    public string ValueAlign { get; set; }

    public string ValuePlacement { get; set; }

    public string DigitalStyle { get; set; }

    public string DigitalGlow { get; set; }

    public string DigitalBackgroundColor { get; set; }

    public string DigitalSegmentColor { get; set; }

    public string DigitalInactiveSegmentColor { get; set; }

    public string DigitalLabelColor { get; set; }

    public int DigitalDigits { get; set; }

    public string GaugeStyle { get; set; }

    public string ChartType { get; set; }

    public int MetricCardColumns { get; set; }

    public List<string> DisplayMetrics { get; }

    public bool IsKpiTile => string.Equals(Profile.Type, DashboardWidgetCatalog.KpiTileType, StringComparison.Ordinal);

    public bool UsesMetricQueryBuilder =>
        IsKpiTile ||
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
        MetricVisualizationId = DashboardWidgetCatalog.NormalizeMetricVisualization(
            ReadString(configuration, DashboardWidgetCatalog.MetricVisualizationKey));
        PrimaryMetric = DashboardWidgetCatalog.NormalizePrimaryMetric(
            ReadString(configuration, DashboardWidgetCatalog.PrimaryMetricKey));
        TitleColor = NormalizeColor(
            ReadString(configuration, DashboardWidgetCatalog.KpiTitleColorKey),
            DashboardWidgetCatalog.KpiDefaultTitleColor);
        SubtitleColor = NormalizeColor(
            ReadString(configuration, DashboardWidgetCatalog.KpiSubtitleColorKey),
            DashboardWidgetCatalog.KpiDefaultSubtitleColor);
        ValueColor = NormalizeColor(
            ReadString(configuration, DashboardWidgetCatalog.KpiValueColorKey),
            DashboardWidgetCatalog.KpiDefaultValueColor);
        TitleAlign = DashboardWidgetCatalog.NormalizeKpiHorizontalAlignment(
            ReadString(configuration, DashboardWidgetCatalog.KpiTitleAlignKey));
        ValueAlign = DashboardWidgetCatalog.NormalizeKpiHorizontalAlignment(
            ReadString(configuration, DashboardWidgetCatalog.KpiValueAlignKey));
        ValuePlacement = DashboardWidgetCatalog.NormalizeKpiValuePlacement(
            ReadString(configuration, DashboardWidgetCatalog.KpiValuePlacementKey));
        DigitalStyle = DashboardWidgetCatalog.NormalizeMetricDigitalStyle(
            ReadString(configuration, DashboardWidgetCatalog.MetricDigitalStyleKey));
        DigitalGlow = DashboardWidgetCatalog.NormalizeMetricDigitalGlow(
            ReadString(configuration, DashboardWidgetCatalog.MetricDigitalGlowKey));
        DigitalBackgroundColor = NormalizeColor(
            ReadString(configuration, DashboardWidgetCatalog.MetricDigitalBackgroundColorKey),
            DashboardWidgetCatalog.MetricDigitalDefaultBackgroundColor);
        DigitalSegmentColor = NormalizeColor(
            ReadString(configuration, DashboardWidgetCatalog.MetricDigitalSegmentColorKey),
            DashboardWidgetCatalog.MetricDigitalDefaultSegmentColor);
        DigitalInactiveSegmentColor = NormalizeColor(
            ReadString(configuration, DashboardWidgetCatalog.MetricDigitalInactiveSegmentColorKey),
            DashboardWidgetCatalog.MetricDigitalDefaultInactiveSegmentColor);
        DigitalLabelColor = NormalizeColor(
            ReadString(configuration, DashboardWidgetCatalog.MetricDigitalLabelColorKey),
            DashboardWidgetCatalog.MetricDigitalDefaultLabelColor);
        DigitalDigits = DashboardWidgetCatalog.NormalizeMetricDigitalDigits(
            ReadString(configuration, DashboardWidgetCatalog.MetricDigitalDigitsKey));
        GaugeStyle = DashboardWidgetCatalog.NormalizeGaugeStyle(
            ReadString(configuration, DashboardWidgetCatalog.GaugeStyleKey));
        ChartType = DashboardWidgetCatalog.NormalizeChartType(
            ReadString(configuration, DashboardWidgetCatalog.ChartTypeKey));
        MetricCardColumns = DashboardWidgetCatalog.NormalizeMetricCardColumns(
            ReadString(configuration, DashboardWidgetCatalog.MetricCardColumnsKey));
        DisplayMetrics.Clear();
        DisplayMetrics.AddRange(DashboardWidgetCatalog.NormalizeDisplayMetrics(
            ReadString(configuration, DashboardWidgetCatalog.DisplayMetricsKey)));

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
            var metricQueryConfiguration = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["title"] = title
            };
            if (Profile.UsesSubtitle)
            {
                metricQueryConfiguration["subtitle"] = subtitle;
            }

            ApplyKpiConfiguration(metricQueryConfiguration);
            ApplyMetricVisualization(metricQueryConfiguration);
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
        ApplyKpiConfiguration(configuration);
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
            configuration[DashboardWidgetCatalog.GaugeStyleKey] =
                DashboardWidgetCatalog.NormalizeGaugeStyle(GaugeStyle);
        }

        if (Profile.UsesChartType)
        {
            configuration[DashboardWidgetCatalog.ChartTypeKey] =
                DashboardWidgetCatalog.NormalizeChartType(ChartType);
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

    private void ApplyKpiConfiguration(Dictionary<string, string> configuration)
    {
        if (!IsKpiTile)
        {
            return;
        }

        configuration[DashboardWidgetCatalog.KpiTitleColorKey] =
            NormalizeColor(TitleColor, DashboardWidgetCatalog.KpiDefaultTitleColor);
        configuration[DashboardWidgetCatalog.KpiSubtitleColorKey] =
            NormalizeColor(SubtitleColor, DashboardWidgetCatalog.KpiDefaultSubtitleColor);
        configuration[DashboardWidgetCatalog.KpiValueColorKey] =
            NormalizeColor(ValueColor, DashboardWidgetCatalog.KpiDefaultValueColor);
        configuration[DashboardWidgetCatalog.KpiTitleAlignKey] =
            DashboardWidgetCatalog.NormalizeKpiHorizontalAlignment(TitleAlign);
        configuration[DashboardWidgetCatalog.KpiValueAlignKey] =
            DashboardWidgetCatalog.NormalizeKpiHorizontalAlignment(ValueAlign);
        configuration[DashboardWidgetCatalog.KpiValuePlacementKey] =
            DashboardWidgetCatalog.NormalizeKpiValuePlacement(ValuePlacement);
        if (string.Equals(MetricVisualizationId, DashboardMetricVisualizationIds.Digital, StringComparison.Ordinal))
        {
            configuration[DashboardWidgetCatalog.MetricDigitalStyleKey] =
                DashboardWidgetCatalog.NormalizeMetricDigitalStyle(DigitalStyle);
            configuration[DashboardWidgetCatalog.MetricDigitalGlowKey] =
                DashboardWidgetCatalog.NormalizeMetricDigitalGlow(DigitalGlow);
            configuration[DashboardWidgetCatalog.MetricDigitalBackgroundColorKey] =
                NormalizeColor(DigitalBackgroundColor, DashboardWidgetCatalog.MetricDigitalDefaultBackgroundColor);
            configuration[DashboardWidgetCatalog.MetricDigitalSegmentColorKey] =
                NormalizeColor(DigitalSegmentColor, DashboardWidgetCatalog.MetricDigitalDefaultSegmentColor);
            configuration[DashboardWidgetCatalog.MetricDigitalInactiveSegmentColorKey] =
                NormalizeColor(DigitalInactiveSegmentColor, DashboardWidgetCatalog.MetricDigitalDefaultInactiveSegmentColor);
            configuration[DashboardWidgetCatalog.MetricDigitalLabelColorKey] =
                NormalizeColor(DigitalLabelColor, DashboardWidgetCatalog.MetricDigitalDefaultLabelColor);
            configuration[DashboardWidgetCatalog.MetricDigitalDigitsKey] =
                DashboardWidgetCatalog.NormalizeMetricDigitalDigits(DigitalDigits)
                    .ToString(CultureInfo.InvariantCulture);
        }
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

    private static string? ReadString(IReadOnlyDictionary<string, string> configuration, string key)
        => configuration.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;

    private static string NormalizeColor(string? value, string fallback)
    {
        var normalized = Normalize(value);
        return IsHexColor(normalized) ? normalized.ToLowerInvariant() : fallback;
    }

    private static bool IsHexColor(string value)
        => value.Length == 7 &&
           value[0] == '#' &&
           value.Skip(1).All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');

    private static string DefaultSubtitle(string type)
        => string.Equals(type, DashboardWidgetCatalog.KpiTileType, StringComparison.Ordinal)
            ? "Total matching events"
            : string.Empty;
}
