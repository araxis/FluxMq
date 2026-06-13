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
        ApplyEventTableVisualValues(widget.Configuration);
        ApplyTopicActivityVisualValues(widget.Configuration);
        ApplyLineChartVisualValues(widget.Configuration);
        ApplyAreaChartVisualValues(widget.Configuration);
        ApplyBarChartVisualValues(widget.Configuration);
        ApplyTopicTreeVisualValues(widget.Configuration);

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

    public string TableHeader { get; set; } = DashboardEventTableVisualOptions.DefaultHeader;

    public bool TableShowHeader { get; set; } = true;

    public string TableRowCount { get; set; } = DashboardEventTableVisualOptions.DefaultRowCount.ToString(CultureInfo.InvariantCulture);

    public string TableDensity { get; set; } = DashboardEventTableVisualOptions.DensityCompact;

    public bool TableShowTime { get; set; } = true;

    public bool TableShowEvent { get; set; } = true;

    public bool TableShowTopic { get; set; } = true;

    public bool TableShowStatus { get; set; } = true;

    public bool TableShowPayload { get; set; } = true;

    public string TableEmptyText { get; set; } = DashboardEventTableVisualOptions.DefaultEmptyText;

    public string TableHeaderColor { get; set; } = DashboardEventTableVisualOptions.DefaultHeaderColor;

    public string TableTextColor { get; set; } = DashboardEventTableVisualOptions.DefaultTextColor;

    public string TableMutedColor { get; set; } = DashboardEventTableVisualOptions.DefaultMutedColor;

    public string TopicActivityHeader { get; set; } = DashboardTopicActivityVisualOptions.DefaultHeader;

    public bool TopicActivityShowHeader { get; set; } = true;

    public string TopicActivityLimit { get; set; } =
        DashboardTopicActivityVisualOptions.DefaultLimit.ToString(CultureInfo.InvariantCulture);

    public bool TopicActivityShowCounts { get; set; } = true;

    public string TopicActivityEmptyText { get; set; } = DashboardTopicActivityVisualOptions.DefaultEmptyText;

    public string TopicActivityHeaderColor { get; set; } = DashboardTopicActivityVisualOptions.DefaultHeaderColor;

    public string TopicActivityTextColor { get; set; } = DashboardTopicActivityVisualOptions.DefaultTextColor;

    public string TopicActivityMutedColor { get; set; } = DashboardTopicActivityVisualOptions.DefaultMutedColor;

    public string TopicActivityAccentColor { get; set; } = DashboardTopicActivityVisualOptions.DefaultAccentColor;

    public string LineChartHeader { get; set; } = DashboardLineChartVisualOptions.DefaultHeader;

    public bool LineChartShowHeader { get; set; } = true;

    public bool LineChartShowGrid { get; set; } = true;

    public bool LineChartShowLabels { get; set; } = true;

    public bool LineChartShowPoints { get; set; }

    public string LineChartLineWidth { get; set; } =
        DashboardLineChartVisualOptions.DefaultLineWidth.ToString(CultureInfo.InvariantCulture);

    public string LineChartEmptyText { get; set; } = DashboardLineChartVisualOptions.DefaultEmptyText;

    public string LineChartLineColor { get; set; } = DashboardLineChartVisualOptions.DefaultLineColor;

    public string LineChartGridColor { get; set; } = DashboardLineChartVisualOptions.DefaultGridColor;

    public string LineChartLabelColor { get; set; } = DashboardLineChartVisualOptions.DefaultLabelColor;

    public string AreaChartHeader { get; set; } = DashboardAreaChartVisualOptions.DefaultHeader;

    public bool AreaChartShowHeader { get; set; } = true;

    public bool AreaChartShowGrid { get; set; } = true;

    public bool AreaChartShowLabels { get; set; } = true;

    public bool AreaChartShowPoints { get; set; }

    public string AreaChartLineWidth { get; set; } =
        DashboardAreaChartVisualOptions.DefaultLineWidth.ToString(CultureInfo.InvariantCulture);

    public string AreaChartFillOpacity { get; set; } =
        DashboardAreaChartVisualOptions.DefaultFillOpacity.ToString(CultureInfo.InvariantCulture);

    public string AreaChartEmptyText { get; set; } = DashboardAreaChartVisualOptions.DefaultEmptyText;

    public string AreaChartLineColor { get; set; } = DashboardAreaChartVisualOptions.DefaultLineColor;

    public string AreaChartFillColor { get; set; } = DashboardAreaChartVisualOptions.DefaultFillColor;

    public string AreaChartGridColor { get; set; } = DashboardAreaChartVisualOptions.DefaultGridColor;

    public string AreaChartLabelColor { get; set; } = DashboardAreaChartVisualOptions.DefaultLabelColor;

    public string BarChartHeader { get; set; } = DashboardBarChartVisualOptions.DefaultHeader;

    public bool BarChartShowHeader { get; set; } = true;

    public bool BarChartShowGrid { get; set; } = true;

    public bool BarChartShowLabels { get; set; } = true;

    public string BarChartOrientation { get; set; } = DashboardBarChartVisualOptions.OrientationVertical;

    public string BarChartBarRadius { get; set; } =
        DashboardBarChartVisualOptions.DefaultBarRadius.ToString(CultureInfo.InvariantCulture);

    public string BarChartEmptyText { get; set; } = DashboardBarChartVisualOptions.DefaultEmptyText;

    public string BarChartBarColor { get; set; } = DashboardBarChartVisualOptions.DefaultBarColor;

    public string BarChartGridColor { get; set; } = DashboardBarChartVisualOptions.DefaultGridColor;

    public string BarChartLabelColor { get; set; } = DashboardBarChartVisualOptions.DefaultLabelColor;

    public string TopicTreeHeader { get; set; } = DashboardTopicTreeVisualOptions.DefaultHeader;

    public bool TopicTreeShowHeader { get; set; } = true;

    public bool TopicTreeShowSummary { get; set; } = true;

    public bool TopicTreeShowTopicCount { get; set; } = true;

    public bool TopicTreeShowMessageCount { get; set; } = true;

    public string TopicTreeEmptyText { get; set; } = DashboardTopicTreeVisualOptions.DefaultEmptyText;

    public string TopicTreeHeaderColor { get; set; } = DashboardTopicTreeVisualOptions.DefaultHeaderColor;

    public string TopicTreeTextColor { get; set; } = DashboardTopicTreeVisualOptions.DefaultTextColor;

    public string TopicTreeMutedColor { get; set; } = DashboardTopicTreeVisualOptions.DefaultMutedColor;

    public string TopicTreeAccentColor { get; set; } = DashboardTopicTreeVisualOptions.DefaultAccentColor;

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
        ApplyEventTableVisualValues(configuration);
        ApplyTopicActivityVisualValues(configuration);
        ApplyLineChartVisualValues(configuration);
        ApplyAreaChartVisualValues(configuration);
        ApplyBarChartVisualValues(configuration);
        ApplyTopicTreeVisualValues(configuration);

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

    public void SetEventTableVisualValue(string key, string? value)
    {
        if (!Profile.UsesEventTableVisual)
        {
            return;
        }

        switch (key)
        {
            case DashboardEventTableVisualOptions.HeaderKey:
                TableHeader = NormalizeText(value, DashboardEventTableVisualOptions.DefaultHeader);
                Title = TableHeader;
                break;
            case DashboardEventTableVisualOptions.ShowHeaderKey:
                TableShowHeader = NormalizeBoolean(value, TableShowHeader);
                break;
            case DashboardEventTableVisualOptions.RowCountKey:
                TableRowCount = DashboardEventTableVisualOptions
                    .NormalizeRowCount(value)
                    .ToString(CultureInfo.InvariantCulture);
                break;
            case DashboardEventTableVisualOptions.DensityKey:
                TableDensity = DashboardEventTableVisualOptions.NormalizeDensity(value);
                break;
            case DashboardEventTableVisualOptions.ShowTimeKey:
                TableShowTime = NormalizeBoolean(value, TableShowTime);
                break;
            case DashboardEventTableVisualOptions.ShowEventKey:
                TableShowEvent = NormalizeBoolean(value, TableShowEvent);
                break;
            case DashboardEventTableVisualOptions.ShowTopicKey:
                TableShowTopic = NormalizeBoolean(value, TableShowTopic);
                break;
            case DashboardEventTableVisualOptions.ShowStatusKey:
                TableShowStatus = NormalizeBoolean(value, TableShowStatus);
                break;
            case DashboardEventTableVisualOptions.ShowPayloadKey:
                TableShowPayload = NormalizeBoolean(value, TableShowPayload);
                break;
            case DashboardEventTableVisualOptions.EmptyTextKey:
                TableEmptyText = NormalizeText(value, DashboardEventTableVisualOptions.DefaultEmptyText);
                break;
            case DashboardEventTableVisualOptions.HeaderColorKey:
                TableHeaderColor = Normalize(value);
                break;
            case DashboardEventTableVisualOptions.TextColorKey:
                TableTextColor = Normalize(value);
                break;
            case DashboardEventTableVisualOptions.MutedColorKey:
                TableMutedColor = Normalize(value);
                break;
        }
    }

    public void SetTopicTreeVisualValue(string key, string? value)
    {
        if (!Profile.UsesTopicTreeVisual)
        {
            return;
        }

        switch (key)
        {
            case DashboardTopicTreeVisualOptions.HeaderKey:
                TopicTreeHeader = NormalizeText(value, DashboardTopicTreeVisualOptions.DefaultHeader);
                Title = TopicTreeHeader;
                break;
            case DashboardTopicTreeVisualOptions.ShowHeaderKey:
                TopicTreeShowHeader = NormalizeBoolean(value, TopicTreeShowHeader);
                break;
            case DashboardTopicTreeVisualOptions.ShowSummaryKey:
                TopicTreeShowSummary = NormalizeBoolean(value, TopicTreeShowSummary);
                break;
            case DashboardTopicTreeVisualOptions.ShowTopicCountKey:
                TopicTreeShowTopicCount = NormalizeBoolean(value, TopicTreeShowTopicCount);
                break;
            case DashboardTopicTreeVisualOptions.ShowMessageCountKey:
                TopicTreeShowMessageCount = NormalizeBoolean(value, TopicTreeShowMessageCount);
                break;
            case DashboardTopicTreeVisualOptions.ExcludeSystemTopicsKey:
                ExcludeSystemTopics = NormalizeBoolean(value, ExcludeSystemTopics);
                break;
            case DashboardTopicTreeVisualOptions.EmptyTextKey:
                TopicTreeEmptyText = NormalizeText(value, DashboardTopicTreeVisualOptions.DefaultEmptyText);
                break;
            case DashboardTopicTreeVisualOptions.HeaderColorKey:
                TopicTreeHeaderColor = Normalize(value);
                break;
            case DashboardTopicTreeVisualOptions.TextColorKey:
                TopicTreeTextColor = Normalize(value);
                break;
            case DashboardTopicTreeVisualOptions.MutedColorKey:
                TopicTreeMutedColor = Normalize(value);
                break;
            case DashboardTopicTreeVisualOptions.AccentColorKey:
                TopicTreeAccentColor = Normalize(value);
                break;
        }
    }

    public void SetTopicActivityVisualValue(string key, string? value)
    {
        if (!Profile.UsesTopicActivityVisual)
        {
            return;
        }

        switch (key)
        {
            case DashboardTopicActivityVisualOptions.HeaderKey:
                TopicActivityHeader = NormalizeText(value, DashboardTopicActivityVisualOptions.DefaultHeader);
                Title = TopicActivityHeader;
                break;
            case DashboardTopicActivityVisualOptions.ShowHeaderKey:
                TopicActivityShowHeader = NormalizeBoolean(value, TopicActivityShowHeader);
                break;
            case DashboardTopicActivityVisualOptions.LimitKey:
                TopicActivityLimit = DashboardTopicActivityVisualOptions
                    .NormalizeLimit(value)
                    .ToString(CultureInfo.InvariantCulture);
                break;
            case DashboardTopicActivityVisualOptions.ShowCountsKey:
                TopicActivityShowCounts = NormalizeBoolean(value, TopicActivityShowCounts);
                break;
            case DashboardTopicActivityVisualOptions.EmptyTextKey:
                TopicActivityEmptyText = NormalizeText(value, DashboardTopicActivityVisualOptions.DefaultEmptyText);
                break;
            case DashboardTopicActivityVisualOptions.HeaderColorKey:
                TopicActivityHeaderColor = Normalize(value);
                break;
            case DashboardTopicActivityVisualOptions.TextColorKey:
                TopicActivityTextColor = Normalize(value);
                break;
            case DashboardTopicActivityVisualOptions.MutedColorKey:
                TopicActivityMutedColor = Normalize(value);
                break;
            case DashboardTopicActivityVisualOptions.AccentColorKey:
                TopicActivityAccentColor = Normalize(value);
                break;
        }
    }

    public void SetLineChartVisualValue(string key, string? value)
    {
        if (!Profile.UsesLineChartVisual)
        {
            return;
        }

        switch (key)
        {
            case DashboardLineChartVisualOptions.HeaderKey:
                LineChartHeader = NormalizeText(value, DashboardLineChartVisualOptions.DefaultHeader);
                Title = LineChartHeader;
                break;
            case DashboardLineChartVisualOptions.ShowHeaderKey:
                LineChartShowHeader = NormalizeBoolean(value, LineChartShowHeader);
                break;
            case DashboardLineChartVisualOptions.ShowGridKey:
                LineChartShowGrid = NormalizeBoolean(value, LineChartShowGrid);
                break;
            case DashboardLineChartVisualOptions.ShowLabelsKey:
                LineChartShowLabels = NormalizeBoolean(value, LineChartShowLabels);
                break;
            case DashboardLineChartVisualOptions.ShowPointsKey:
                LineChartShowPoints = NormalizeBoolean(value, LineChartShowPoints);
                break;
            case DashboardLineChartVisualOptions.LineWidthKey:
                LineChartLineWidth = DashboardLineChartVisualOptions
                    .NormalizeLineWidth(value)
                    .ToString(CultureInfo.InvariantCulture);
                break;
            case DashboardLineChartVisualOptions.EmptyTextKey:
                LineChartEmptyText = NormalizeText(value, DashboardLineChartVisualOptions.DefaultEmptyText);
                break;
            case DashboardLineChartVisualOptions.LineColorKey:
                LineChartLineColor = Normalize(value);
                break;
            case DashboardLineChartVisualOptions.GridColorKey:
                LineChartGridColor = Normalize(value);
                break;
            case DashboardLineChartVisualOptions.LabelColorKey:
                LineChartLabelColor = Normalize(value);
                break;
        }
    }

    public void SetAreaChartVisualValue(string key, string? value)
    {
        if (!Profile.UsesAreaChartVisual)
        {
            return;
        }

        switch (key)
        {
            case DashboardAreaChartVisualOptions.HeaderKey:
                AreaChartHeader = NormalizeText(value, DashboardAreaChartVisualOptions.DefaultHeader);
                Title = AreaChartHeader;
                break;
            case DashboardAreaChartVisualOptions.ShowHeaderKey:
                AreaChartShowHeader = NormalizeBoolean(value, AreaChartShowHeader);
                break;
            case DashboardAreaChartVisualOptions.ShowGridKey:
                AreaChartShowGrid = NormalizeBoolean(value, AreaChartShowGrid);
                break;
            case DashboardAreaChartVisualOptions.ShowLabelsKey:
                AreaChartShowLabels = NormalizeBoolean(value, AreaChartShowLabels);
                break;
            case DashboardAreaChartVisualOptions.ShowPointsKey:
                AreaChartShowPoints = NormalizeBoolean(value, AreaChartShowPoints);
                break;
            case DashboardAreaChartVisualOptions.LineWidthKey:
                AreaChartLineWidth = DashboardAreaChartVisualOptions
                    .NormalizeLineWidth(value)
                    .ToString(CultureInfo.InvariantCulture);
                break;
            case DashboardAreaChartVisualOptions.FillOpacityKey:
                AreaChartFillOpacity = DashboardAreaChartVisualOptions
                    .NormalizeFillOpacity(value)
                    .ToString(CultureInfo.InvariantCulture);
                break;
            case DashboardAreaChartVisualOptions.EmptyTextKey:
                AreaChartEmptyText = NormalizeText(value, DashboardAreaChartVisualOptions.DefaultEmptyText);
                break;
            case DashboardAreaChartVisualOptions.LineColorKey:
                AreaChartLineColor = Normalize(value);
                break;
            case DashboardAreaChartVisualOptions.FillColorKey:
                AreaChartFillColor = Normalize(value);
                break;
            case DashboardAreaChartVisualOptions.GridColorKey:
                AreaChartGridColor = Normalize(value);
                break;
            case DashboardAreaChartVisualOptions.LabelColorKey:
                AreaChartLabelColor = Normalize(value);
                break;
        }
    }

    public void SetBarChartVisualValue(string key, string? value)
    {
        if (!Profile.UsesBarChartVisual)
        {
            return;
        }

        switch (key)
        {
            case DashboardBarChartVisualOptions.HeaderKey:
                BarChartHeader = NormalizeText(value, DashboardBarChartVisualOptions.DefaultHeader);
                Title = BarChartHeader;
                break;
            case DashboardBarChartVisualOptions.ShowHeaderKey:
                BarChartShowHeader = NormalizeBoolean(value, BarChartShowHeader);
                break;
            case DashboardBarChartVisualOptions.ShowGridKey:
                BarChartShowGrid = NormalizeBoolean(value, BarChartShowGrid);
                break;
            case DashboardBarChartVisualOptions.ShowLabelsKey:
                BarChartShowLabels = NormalizeBoolean(value, BarChartShowLabels);
                break;
            case DashboardBarChartVisualOptions.OrientationKey:
                BarChartOrientation = DashboardBarChartVisualOptions.NormalizeOrientation(value);
                break;
            case DashboardBarChartVisualOptions.BarRadiusKey:
                BarChartBarRadius = DashboardBarChartVisualOptions
                    .NormalizeBarRadius(value)
                    .ToString(CultureInfo.InvariantCulture);
                break;
            case DashboardBarChartVisualOptions.EmptyTextKey:
                BarChartEmptyText = NormalizeText(value, DashboardBarChartVisualOptions.DefaultEmptyText);
                break;
            case DashboardBarChartVisualOptions.BarColorKey:
                BarChartBarColor = Normalize(value);
                break;
            case DashboardBarChartVisualOptions.GridColorKey:
                BarChartGridColor = Normalize(value);
                break;
            case DashboardBarChartVisualOptions.LabelColorKey:
                BarChartLabelColor = Normalize(value);
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
            ApplyTopicTreeVisualConfiguration(topicConfiguration);
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
        ApplyEventTableVisualConfiguration(configuration);
        ApplyTopicActivityVisualConfiguration(configuration);
        ApplyLineChartVisualConfiguration(configuration);
        ApplyAreaChartVisualConfiguration(configuration);
        ApplyBarChartVisualConfiguration(configuration);
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

    private void ApplyEventTableVisualValues(IReadOnlyDictionary<string, string> configuration)
    {
        if (!Profile.UsesEventTableVisual)
        {
            return;
        }

        TableHeader = ReadString(configuration, DashboardEventTableVisualOptions.HeaderKey) ??
            ReadString(configuration, "title") ??
            DashboardEventTableVisualOptions.DefaultHeader;
        Title = TableHeader;
        TableShowHeader = ReadBoolean(configuration, DashboardEventTableVisualOptions.ShowHeaderKey, true);
        TableRowCount = DashboardEventTableVisualOptions
            .NormalizeRowCount(
                ReadString(configuration, DashboardEventTableVisualOptions.RowCountKey) ??
                ReadString(configuration, DashboardEventTableVisualOptions.LegacyRowCountKey))
            .ToString(CultureInfo.InvariantCulture);
        TableDensity = DashboardEventTableVisualOptions.NormalizeDensity(
            ReadString(configuration, DashboardEventTableVisualOptions.DensityKey) ??
            ReadString(configuration, DashboardEventTableVisualOptions.LegacyDensityKey));
        TableShowTime = ReadBoolean(configuration, DashboardEventTableVisualOptions.ShowTimeKey, true);
        TableShowEvent = ReadBoolean(configuration, DashboardEventTableVisualOptions.ShowEventKey, true);
        TableShowTopic = ReadBoolean(configuration, DashboardEventTableVisualOptions.ShowTopicKey, true);
        TableShowStatus = ReadBoolean(configuration, DashboardEventTableVisualOptions.ShowStatusKey, true);
        TableShowPayload = ReadBoolean(
            configuration,
            DashboardEventTableVisualOptions.ShowPayloadKey,
            ReadBoolean(configuration, DashboardEventTableVisualOptions.LegacyPayloadPreviewKey, true));
        TableEmptyText = ReadString(configuration, DashboardEventTableVisualOptions.EmptyTextKey) ??
            DashboardEventTableVisualOptions.DefaultEmptyText;
        TableHeaderColor = ReadString(configuration, DashboardEventTableVisualOptions.HeaderColorKey) ??
            DashboardEventTableVisualOptions.DefaultHeaderColor;
        TableTextColor = ReadString(configuration, DashboardEventTableVisualOptions.TextColorKey) ??
            DashboardEventTableVisualOptions.DefaultTextColor;
        TableMutedColor = ReadString(configuration, DashboardEventTableVisualOptions.MutedColorKey) ??
            DashboardEventTableVisualOptions.DefaultMutedColor;
    }

    private void ApplyEventTableVisualConfiguration(Dictionary<string, string> configuration)
    {
        if (!Profile.UsesEventTableVisual)
        {
            return;
        }

        var header = string.IsNullOrWhiteSpace(TableHeader)
            ? DashboardEventTableVisualOptions.DefaultHeader
            : TableHeader.Trim();
        configuration["title"] = header;
        configuration[DashboardEventTableVisualOptions.HeaderKey] = header;
        configuration[DashboardEventTableVisualOptions.ShowHeaderKey] = TableShowHeader ? bool.TrueString : bool.FalseString;
        configuration[DashboardEventTableVisualOptions.RowCountKey] =
            DashboardEventTableVisualOptions.NormalizeRowCount(TableRowCount).ToString(CultureInfo.InvariantCulture);
        configuration[DashboardEventTableVisualOptions.DensityKey] = DashboardEventTableVisualOptions.NormalizeDensity(TableDensity);
        configuration[DashboardEventTableVisualOptions.ShowTimeKey] = TableShowTime ? bool.TrueString : bool.FalseString;
        configuration[DashboardEventTableVisualOptions.ShowEventKey] = TableShowEvent ? bool.TrueString : bool.FalseString;
        configuration[DashboardEventTableVisualOptions.ShowTopicKey] = TableShowTopic ? bool.TrueString : bool.FalseString;
        configuration[DashboardEventTableVisualOptions.ShowStatusKey] = TableShowStatus ? bool.TrueString : bool.FalseString;
        configuration[DashboardEventTableVisualOptions.ShowPayloadKey] = TableShowPayload ? bool.TrueString : bool.FalseString;
        configuration[DashboardEventTableVisualOptions.EmptyTextKey] = string.IsNullOrWhiteSpace(TableEmptyText)
            ? DashboardEventTableVisualOptions.DefaultEmptyText
            : TableEmptyText.Trim();
        configuration[DashboardEventTableVisualOptions.HeaderColorKey] = NormalizeColor(
            TableHeaderColor,
            DashboardEventTableVisualOptions.DefaultHeaderColor);
        configuration[DashboardEventTableVisualOptions.TextColorKey] = NormalizeColor(
            TableTextColor,
            DashboardEventTableVisualOptions.DefaultTextColor);
        configuration[DashboardEventTableVisualOptions.MutedColorKey] = NormalizeColor(
            TableMutedColor,
            DashboardEventTableVisualOptions.DefaultMutedColor);
    }

    private void ApplyTopicActivityVisualValues(IReadOnlyDictionary<string, string> configuration)
    {
        if (!Profile.UsesTopicActivityVisual)
        {
            return;
        }

        TopicActivityHeader = ReadString(configuration, DashboardTopicActivityVisualOptions.HeaderKey) ??
            ReadString(configuration, "title") ??
            DashboardTopicActivityVisualOptions.DefaultHeader;
        Title = TopicActivityHeader;
        TopicActivityShowHeader = ReadBoolean(configuration, DashboardTopicActivityVisualOptions.ShowHeaderKey, true);
        TopicActivityLimit = DashboardTopicActivityVisualOptions
            .NormalizeLimit(ReadString(configuration, DashboardTopicActivityVisualOptions.LimitKey) ?? ReadString(configuration, "limit"))
            .ToString(CultureInfo.InvariantCulture);
        TopicActivityShowCounts = ReadBoolean(configuration, DashboardTopicActivityVisualOptions.ShowCountsKey, true);
        TopicActivityEmptyText = ReadString(configuration, DashboardTopicActivityVisualOptions.EmptyTextKey) ??
            DashboardTopicActivityVisualOptions.DefaultEmptyText;
        TopicActivityHeaderColor = ReadString(configuration, DashboardTopicActivityVisualOptions.HeaderColorKey) ??
            DashboardTopicActivityVisualOptions.DefaultHeaderColor;
        TopicActivityTextColor = ReadString(configuration, DashboardTopicActivityVisualOptions.TextColorKey) ??
            DashboardTopicActivityVisualOptions.DefaultTextColor;
        TopicActivityMutedColor = ReadString(configuration, DashboardTopicActivityVisualOptions.MutedColorKey) ??
            DashboardTopicActivityVisualOptions.DefaultMutedColor;
        TopicActivityAccentColor = ReadString(configuration, DashboardTopicActivityVisualOptions.AccentColorKey) ??
            DashboardTopicActivityVisualOptions.DefaultAccentColor;
    }

    private void ApplyTopicActivityVisualConfiguration(Dictionary<string, string> configuration)
    {
        if (!Profile.UsesTopicActivityVisual)
        {
            return;
        }

        var header = string.IsNullOrWhiteSpace(TopicActivityHeader)
            ? DashboardTopicActivityVisualOptions.DefaultHeader
            : TopicActivityHeader.Trim();
        configuration["title"] = header;
        configuration[DashboardTopicActivityVisualOptions.HeaderKey] = header;
        configuration[DashboardTopicActivityVisualOptions.ShowHeaderKey] = TopicActivityShowHeader ? bool.TrueString : bool.FalseString;
        configuration[DashboardTopicActivityVisualOptions.LimitKey] =
            DashboardTopicActivityVisualOptions.NormalizeLimit(TopicActivityLimit).ToString(CultureInfo.InvariantCulture);
        configuration[DashboardTopicActivityVisualOptions.ShowCountsKey] = TopicActivityShowCounts ? bool.TrueString : bool.FalseString;
        configuration[DashboardTopicActivityVisualOptions.EmptyTextKey] = string.IsNullOrWhiteSpace(TopicActivityEmptyText)
            ? DashboardTopicActivityVisualOptions.DefaultEmptyText
            : TopicActivityEmptyText.Trim();
        configuration[DashboardTopicActivityVisualOptions.HeaderColorKey] = NormalizeColor(
            TopicActivityHeaderColor,
            DashboardTopicActivityVisualOptions.DefaultHeaderColor);
        configuration[DashboardTopicActivityVisualOptions.TextColorKey] = NormalizeColor(
            TopicActivityTextColor,
            DashboardTopicActivityVisualOptions.DefaultTextColor);
        configuration[DashboardTopicActivityVisualOptions.MutedColorKey] = NormalizeColor(
            TopicActivityMutedColor,
            DashboardTopicActivityVisualOptions.DefaultMutedColor);
        configuration[DashboardTopicActivityVisualOptions.AccentColorKey] = NormalizeColor(
            TopicActivityAccentColor,
            DashboardTopicActivityVisualOptions.DefaultAccentColor);
    }

    private void ApplyTopicTreeVisualValues(IReadOnlyDictionary<string, string> configuration)
    {
        if (!Profile.UsesTopicTreeVisual)
        {
            return;
        }

        TopicTreeHeader = ReadString(configuration, DashboardTopicTreeVisualOptions.HeaderKey) ??
            ReadString(configuration, "title") ??
            DashboardTopicTreeVisualOptions.DefaultHeader;
        Title = TopicTreeHeader;
        TopicTreeShowHeader = ReadBoolean(configuration, DashboardTopicTreeVisualOptions.ShowHeaderKey, true);
        TopicTreeShowSummary = ReadBoolean(configuration, DashboardTopicTreeVisualOptions.ShowSummaryKey, true);
        TopicTreeShowTopicCount = ReadBoolean(configuration, DashboardTopicTreeVisualOptions.ShowTopicCountKey, true);
        TopicTreeShowMessageCount = ReadBoolean(configuration, DashboardTopicTreeVisualOptions.ShowMessageCountKey, true);
        ExcludeSystemTopics = ReadBoolean(configuration, DashboardTopicTreeVisualOptions.ExcludeSystemTopicsKey, true);
        TopicTreeEmptyText = ReadString(configuration, DashboardTopicTreeVisualOptions.EmptyTextKey) ??
            DashboardTopicTreeVisualOptions.DefaultEmptyText;
        TopicTreeHeaderColor = ReadString(configuration, DashboardTopicTreeVisualOptions.HeaderColorKey) ??
            DashboardTopicTreeVisualOptions.DefaultHeaderColor;
        TopicTreeTextColor = ReadString(configuration, DashboardTopicTreeVisualOptions.TextColorKey) ??
            DashboardTopicTreeVisualOptions.DefaultTextColor;
        TopicTreeMutedColor = ReadString(configuration, DashboardTopicTreeVisualOptions.MutedColorKey) ??
            DashboardTopicTreeVisualOptions.DefaultMutedColor;
        TopicTreeAccentColor = ReadString(configuration, DashboardTopicTreeVisualOptions.AccentColorKey) ??
            DashboardTopicTreeVisualOptions.DefaultAccentColor;
    }

    private void ApplyTopicTreeVisualConfiguration(Dictionary<string, string> configuration)
    {
        if (!Profile.UsesTopicTreeVisual)
        {
            return;
        }

        var header = string.IsNullOrWhiteSpace(TopicTreeHeader)
            ? DashboardTopicTreeVisualOptions.DefaultHeader
            : TopicTreeHeader.Trim();
        configuration["title"] = header;
        configuration[DashboardTopicTreeVisualOptions.HeaderKey] = header;
        configuration[DashboardTopicTreeVisualOptions.ShowHeaderKey] = TopicTreeShowHeader ? bool.TrueString : bool.FalseString;
        configuration[DashboardTopicTreeVisualOptions.ShowSummaryKey] = TopicTreeShowSummary ? bool.TrueString : bool.FalseString;
        configuration[DashboardTopicTreeVisualOptions.ShowTopicCountKey] = TopicTreeShowTopicCount ? bool.TrueString : bool.FalseString;
        configuration[DashboardTopicTreeVisualOptions.ShowMessageCountKey] = TopicTreeShowMessageCount ? bool.TrueString : bool.FalseString;
        configuration[DashboardTopicTreeVisualOptions.ExcludeSystemTopicsKey] = ExcludeSystemTopics ? bool.TrueString : bool.FalseString;
        configuration[DashboardTopicTreeVisualOptions.EmptyTextKey] = string.IsNullOrWhiteSpace(TopicTreeEmptyText)
            ? DashboardTopicTreeVisualOptions.DefaultEmptyText
            : TopicTreeEmptyText.Trim();
        configuration[DashboardTopicTreeVisualOptions.HeaderColorKey] = NormalizeColor(
            TopicTreeHeaderColor,
            DashboardTopicTreeVisualOptions.DefaultHeaderColor);
        configuration[DashboardTopicTreeVisualOptions.TextColorKey] = NormalizeColor(
            TopicTreeTextColor,
            DashboardTopicTreeVisualOptions.DefaultTextColor);
        configuration[DashboardTopicTreeVisualOptions.MutedColorKey] = NormalizeColor(
            TopicTreeMutedColor,
            DashboardTopicTreeVisualOptions.DefaultMutedColor);
        configuration[DashboardTopicTreeVisualOptions.AccentColorKey] = NormalizeColor(
            TopicTreeAccentColor,
            DashboardTopicTreeVisualOptions.DefaultAccentColor);
    }

    private void ApplyLineChartVisualValues(IReadOnlyDictionary<string, string> configuration)
    {
        if (!Profile.UsesLineChartVisual)
        {
            return;
        }

        LineChartHeader = ReadString(configuration, DashboardLineChartVisualOptions.HeaderKey) ??
            ReadString(configuration, "title") ??
            DashboardLineChartVisualOptions.DefaultHeader;
        Title = LineChartHeader;
        LineChartShowHeader = ReadBoolean(configuration, DashboardLineChartVisualOptions.ShowHeaderKey, true);
        LineChartShowGrid = ReadBoolean(
            configuration,
            DashboardLineChartVisualOptions.ShowGridKey,
            ReadBoolean(configuration, DashboardLineChartVisualOptions.LegacyShowGridKey, true));
        LineChartShowLabels = ReadBoolean(
            configuration,
            DashboardLineChartVisualOptions.ShowLabelsKey,
            ReadBoolean(configuration, DashboardLineChartVisualOptions.LegacyShowLabelsKey, true));
        LineChartShowPoints = ReadBoolean(
            configuration,
            DashboardLineChartVisualOptions.ShowPointsKey,
            ReadBoolean(configuration, DashboardLineChartVisualOptions.LegacyShowPointsKey, false));
        LineChartLineWidth = DashboardLineChartVisualOptions
            .NormalizeLineWidth(ReadString(configuration, DashboardLineChartVisualOptions.LineWidthKey))
            .ToString(CultureInfo.InvariantCulture);
        LineChartEmptyText = ReadString(configuration, DashboardLineChartVisualOptions.EmptyTextKey) ??
            DashboardLineChartVisualOptions.DefaultEmptyText;
        LineChartLineColor = ReadString(configuration, DashboardLineChartVisualOptions.LineColorKey) ??
            ReadString(configuration, DashboardLineChartVisualOptions.LegacyLineColorKey) ??
            DashboardLineChartVisualOptions.DefaultLineColor;
        LineChartGridColor = ReadString(configuration, DashboardLineChartVisualOptions.GridColorKey) ??
            DashboardLineChartVisualOptions.DefaultGridColor;
        LineChartLabelColor = ReadString(configuration, DashboardLineChartVisualOptions.LabelColorKey) ??
            DashboardLineChartVisualOptions.DefaultLabelColor;
    }

    private void ApplyLineChartVisualConfiguration(Dictionary<string, string> configuration)
    {
        if (!Profile.UsesLineChartVisual)
        {
            return;
        }

        var header = string.IsNullOrWhiteSpace(LineChartHeader)
            ? DashboardLineChartVisualOptions.DefaultHeader
            : LineChartHeader.Trim();
        configuration["title"] = header;
        configuration[DashboardChartWidgetOptions.TypeKey] = DashboardChartWidgetOptions.TypeLine;
        configuration[DashboardLineChartVisualOptions.HeaderKey] = header;
        configuration[DashboardLineChartVisualOptions.ShowHeaderKey] = LineChartShowHeader ? bool.TrueString : bool.FalseString;
        configuration[DashboardLineChartVisualOptions.ShowGridKey] = LineChartShowGrid ? bool.TrueString : bool.FalseString;
        configuration[DashboardLineChartVisualOptions.ShowLabelsKey] = LineChartShowLabels ? bool.TrueString : bool.FalseString;
        configuration[DashboardLineChartVisualOptions.ShowPointsKey] = LineChartShowPoints ? bool.TrueString : bool.FalseString;
        configuration[DashboardLineChartVisualOptions.LineWidthKey] =
            DashboardLineChartVisualOptions.NormalizeLineWidth(LineChartLineWidth).ToString(CultureInfo.InvariantCulture);
        configuration[DashboardLineChartVisualOptions.EmptyTextKey] = string.IsNullOrWhiteSpace(LineChartEmptyText)
            ? DashboardLineChartVisualOptions.DefaultEmptyText
            : LineChartEmptyText.Trim();
        configuration[DashboardLineChartVisualOptions.LineColorKey] = NormalizeColor(
            LineChartLineColor,
            DashboardLineChartVisualOptions.DefaultLineColor);
        configuration[DashboardLineChartVisualOptions.GridColorKey] = NormalizeColor(
            LineChartGridColor,
            DashboardLineChartVisualOptions.DefaultGridColor);
        configuration[DashboardLineChartVisualOptions.LabelColorKey] = NormalizeColor(
            LineChartLabelColor,
            DashboardLineChartVisualOptions.DefaultLabelColor);
    }

    private void ApplyAreaChartVisualValues(IReadOnlyDictionary<string, string> configuration)
    {
        if (!Profile.UsesAreaChartVisual)
        {
            return;
        }

        AreaChartHeader = ReadString(configuration, DashboardAreaChartVisualOptions.HeaderKey) ??
            ReadString(configuration, "title") ??
            DashboardAreaChartVisualOptions.DefaultHeader;
        Title = AreaChartHeader;
        AreaChartShowHeader = ReadBoolean(configuration, DashboardAreaChartVisualOptions.ShowHeaderKey, true);
        AreaChartShowGrid = ReadBoolean(
            configuration,
            DashboardAreaChartVisualOptions.ShowGridKey,
            ReadBoolean(configuration, DashboardAreaChartVisualOptions.LegacyShowGridKey, true));
        AreaChartShowLabels = ReadBoolean(
            configuration,
            DashboardAreaChartVisualOptions.ShowLabelsKey,
            ReadBoolean(configuration, DashboardAreaChartVisualOptions.LegacyShowLabelsKey, true));
        AreaChartShowPoints = ReadBoolean(
            configuration,
            DashboardAreaChartVisualOptions.ShowPointsKey,
            ReadBoolean(configuration, DashboardAreaChartVisualOptions.LegacyShowPointsKey, false));
        AreaChartLineWidth = DashboardAreaChartVisualOptions
            .NormalizeLineWidth(ReadString(configuration, DashboardAreaChartVisualOptions.LineWidthKey))
            .ToString(CultureInfo.InvariantCulture);
        AreaChartFillOpacity = DashboardAreaChartVisualOptions
            .NormalizeFillOpacity(
                ReadString(configuration, DashboardAreaChartVisualOptions.FillOpacityKey) ??
                ReadString(configuration, DashboardAreaChartVisualOptions.LegacyFillOpacityKey))
            .ToString(CultureInfo.InvariantCulture);
        AreaChartEmptyText = ReadString(configuration, DashboardAreaChartVisualOptions.EmptyTextKey) ??
            DashboardAreaChartVisualOptions.DefaultEmptyText;
        AreaChartLineColor = ReadString(configuration, DashboardAreaChartVisualOptions.LineColorKey) ??
            ReadString(configuration, DashboardAreaChartVisualOptions.LegacyLineColorKey) ??
            DashboardAreaChartVisualOptions.DefaultLineColor;
        AreaChartFillColor = ReadString(configuration, DashboardAreaChartVisualOptions.FillColorKey) ??
            ReadString(configuration, DashboardAreaChartVisualOptions.LegacyFillColorKey) ??
            DashboardAreaChartVisualOptions.DefaultFillColor;
        AreaChartGridColor = ReadString(configuration, DashboardAreaChartVisualOptions.GridColorKey) ??
            DashboardAreaChartVisualOptions.DefaultGridColor;
        AreaChartLabelColor = ReadString(configuration, DashboardAreaChartVisualOptions.LabelColorKey) ??
            DashboardAreaChartVisualOptions.DefaultLabelColor;
    }

    private void ApplyAreaChartVisualConfiguration(Dictionary<string, string> configuration)
    {
        if (!Profile.UsesAreaChartVisual)
        {
            return;
        }

        var header = string.IsNullOrWhiteSpace(AreaChartHeader)
            ? DashboardAreaChartVisualOptions.DefaultHeader
            : AreaChartHeader.Trim();
        configuration["title"] = header;
        configuration[DashboardChartWidgetOptions.TypeKey] = DashboardChartWidgetOptions.TypeArea;
        configuration[DashboardAreaChartVisualOptions.HeaderKey] = header;
        configuration[DashboardAreaChartVisualOptions.ShowHeaderKey] = AreaChartShowHeader ? bool.TrueString : bool.FalseString;
        configuration[DashboardAreaChartVisualOptions.ShowGridKey] = AreaChartShowGrid ? bool.TrueString : bool.FalseString;
        configuration[DashboardAreaChartVisualOptions.ShowLabelsKey] = AreaChartShowLabels ? bool.TrueString : bool.FalseString;
        configuration[DashboardAreaChartVisualOptions.ShowPointsKey] = AreaChartShowPoints ? bool.TrueString : bool.FalseString;
        configuration[DashboardAreaChartVisualOptions.LineWidthKey] =
            DashboardAreaChartVisualOptions.NormalizeLineWidth(AreaChartLineWidth).ToString(CultureInfo.InvariantCulture);
        configuration[DashboardAreaChartVisualOptions.FillOpacityKey] =
            DashboardAreaChartVisualOptions.NormalizeFillOpacity(AreaChartFillOpacity).ToString(CultureInfo.InvariantCulture);
        configuration[DashboardAreaChartVisualOptions.EmptyTextKey] = string.IsNullOrWhiteSpace(AreaChartEmptyText)
            ? DashboardAreaChartVisualOptions.DefaultEmptyText
            : AreaChartEmptyText.Trim();
        configuration[DashboardAreaChartVisualOptions.LineColorKey] = NormalizeColor(
            AreaChartLineColor,
            DashboardAreaChartVisualOptions.DefaultLineColor);
        configuration[DashboardAreaChartVisualOptions.FillColorKey] = NormalizeColor(
            AreaChartFillColor,
            DashboardAreaChartVisualOptions.DefaultFillColor);
        configuration[DashboardAreaChartVisualOptions.GridColorKey] = NormalizeColor(
            AreaChartGridColor,
            DashboardAreaChartVisualOptions.DefaultGridColor);
        configuration[DashboardAreaChartVisualOptions.LabelColorKey] = NormalizeColor(
            AreaChartLabelColor,
            DashboardAreaChartVisualOptions.DefaultLabelColor);
    }

    private void ApplyBarChartVisualValues(IReadOnlyDictionary<string, string> configuration)
    {
        if (!Profile.UsesBarChartVisual)
        {
            return;
        }

        BarChartHeader = ReadString(configuration, DashboardBarChartVisualOptions.HeaderKey) ??
            ReadString(configuration, "title") ??
            DashboardBarChartVisualOptions.DefaultHeader;
        Title = BarChartHeader;
        BarChartShowHeader = ReadBoolean(configuration, DashboardBarChartVisualOptions.ShowHeaderKey, true);
        BarChartShowGrid = ReadBoolean(
            configuration,
            DashboardBarChartVisualOptions.ShowGridKey,
            ReadBoolean(configuration, DashboardBarChartVisualOptions.LegacyShowGridKey, true));
        BarChartShowLabels = ReadBoolean(
            configuration,
            DashboardBarChartVisualOptions.ShowLabelsKey,
            ReadBoolean(configuration, DashboardBarChartVisualOptions.LegacyShowLabelsKey, true));
        BarChartOrientation = DashboardBarChartVisualOptions.NormalizeOrientation(
            ReadString(configuration, DashboardBarChartVisualOptions.OrientationKey) ??
            ReadString(configuration, DashboardBarChartVisualOptions.LegacyOrientationKey));
        BarChartBarRadius = DashboardBarChartVisualOptions
            .NormalizeBarRadius(ReadString(configuration, DashboardBarChartVisualOptions.BarRadiusKey))
            .ToString(CultureInfo.InvariantCulture);
        BarChartEmptyText = ReadString(configuration, DashboardBarChartVisualOptions.EmptyTextKey) ??
            DashboardBarChartVisualOptions.DefaultEmptyText;
        BarChartBarColor = ReadString(configuration, DashboardBarChartVisualOptions.BarColorKey) ??
            ReadString(configuration, DashboardBarChartVisualOptions.LegacyBarColorKey) ??
            DashboardBarChartVisualOptions.DefaultBarColor;
        BarChartGridColor = ReadString(configuration, DashboardBarChartVisualOptions.GridColorKey) ??
            DashboardBarChartVisualOptions.DefaultGridColor;
        BarChartLabelColor = ReadString(configuration, DashboardBarChartVisualOptions.LabelColorKey) ??
            DashboardBarChartVisualOptions.DefaultLabelColor;
    }

    private void ApplyBarChartVisualConfiguration(Dictionary<string, string> configuration)
    {
        if (!Profile.UsesBarChartVisual)
        {
            return;
        }

        var header = string.IsNullOrWhiteSpace(BarChartHeader)
            ? DashboardBarChartVisualOptions.DefaultHeader
            : BarChartHeader.Trim();
        configuration["title"] = header;
        configuration[DashboardChartWidgetOptions.TypeKey] = DashboardChartWidgetOptions.TypeBars;
        configuration.Remove(DashboardWidgetCatalog.PrimaryMetricKey);
        configuration[DashboardBarChartVisualOptions.HeaderKey] = header;
        configuration[DashboardBarChartVisualOptions.ShowHeaderKey] = BarChartShowHeader ? bool.TrueString : bool.FalseString;
        configuration[DashboardBarChartVisualOptions.ShowGridKey] = BarChartShowGrid ? bool.TrueString : bool.FalseString;
        configuration[DashboardBarChartVisualOptions.ShowLabelsKey] = BarChartShowLabels ? bool.TrueString : bool.FalseString;
        configuration[DashboardBarChartVisualOptions.OrientationKey] =
            DashboardBarChartVisualOptions.NormalizeOrientation(BarChartOrientation);
        configuration[DashboardBarChartVisualOptions.BarRadiusKey] =
            DashboardBarChartVisualOptions.NormalizeBarRadius(BarChartBarRadius).ToString(CultureInfo.InvariantCulture);
        configuration[DashboardBarChartVisualOptions.EmptyTextKey] = string.IsNullOrWhiteSpace(BarChartEmptyText)
            ? DashboardBarChartVisualOptions.DefaultEmptyText
            : BarChartEmptyText.Trim();
        configuration[DashboardBarChartVisualOptions.BarColorKey] = NormalizeColor(
            BarChartBarColor,
            DashboardBarChartVisualOptions.DefaultBarColor);
        configuration[DashboardBarChartVisualOptions.GridColorKey] = NormalizeColor(
            BarChartGridColor,
            DashboardBarChartVisualOptions.DefaultGridColor);
        configuration[DashboardBarChartVisualOptions.LabelColorKey] = NormalizeColor(
            BarChartLabelColor,
            DashboardBarChartVisualOptions.DefaultLabelColor);
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
