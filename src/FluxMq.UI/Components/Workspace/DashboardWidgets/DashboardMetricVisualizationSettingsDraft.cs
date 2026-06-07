using FluxMq.UI.Models;
using FluxMq.UI.Services;
using System.Globalization;

namespace FluxMq.UI.Components.Workspace;

public sealed class DashboardMetricVisualizationSettingsDraft
{
    private static readonly HashSet<string> VisualConfigurationKeys = new(StringComparer.Ordinal)
    {
        "title",
        "subtitle",
        DashboardWidgetCatalog.KpiTitleColorKey,
        DashboardWidgetCatalog.KpiSubtitleColorKey,
        DashboardWidgetCatalog.KpiValueColorKey,
        DashboardWidgetCatalog.KpiTitleAlignKey,
        DashboardWidgetCatalog.KpiValueAlignKey,
        DashboardWidgetCatalog.KpiValuePlacementKey,
        DashboardWidgetCatalog.MetricVisualizationKey,
        DashboardWidgetCatalog.MetricValueTitleKey,
        DashboardWidgetCatalog.MetricValueSubtitleKey,
        DashboardWidgetCatalog.MetricValueShowTitleKey,
        DashboardWidgetCatalog.MetricValueShowSubtitleKey,
        DashboardWidgetCatalog.MetricValueTitleColorKey,
        DashboardWidgetCatalog.MetricValueSubtitleColorKey,
        DashboardWidgetCatalog.MetricValueValueColorKey,
        DashboardWidgetCatalog.MetricValueTitleAlignKey,
        DashboardWidgetCatalog.MetricValueValueAlignKey,
        DashboardWidgetCatalog.MetricValueValuePlacementKey,
        DashboardWidgetCatalog.MetricDigitalLabelKey,
        DashboardWidgetCatalog.MetricDigitalShowLabelKey,
        DashboardWidgetCatalog.MetricDigitalLabelPlacementKey,
        DashboardWidgetCatalog.MetricDigitalStyleKey,
        DashboardWidgetCatalog.MetricDigitalGlowKey,
        DashboardWidgetCatalog.MetricDigitalBackgroundColorKey,
        DashboardWidgetCatalog.MetricDigitalSegmentColorKey,
        DashboardWidgetCatalog.MetricDigitalInactiveSegmentColorKey,
        DashboardWidgetCatalog.MetricDigitalLabelColorKey,
        DashboardWidgetCatalog.MetricDigitalDigitsKey,
        DashboardWidgetCatalog.MetricDigitalBorderColorKey,
        DashboardWidgetCatalog.MetricDigitalBorderWidthKey,
        DashboardWidgetCatalog.MetricDigitalRadiusKey,
        DashboardWidgetCatalog.MetricDigitalPaddingKey,
        DashboardWidgetCatalog.MetricDigitalFitModeKey
    };

    public string VisualizationId { get; private set; } = DashboardMetricVisualizationIds.Value;

    public string ValueTitle { get; set; } = DashboardWidgetCatalog.MetricValueDefaultTitle;

    public string ValueSubtitle { get; set; } = DashboardWidgetCatalog.MetricValueDefaultSubtitle;

    public bool ValueShowTitle { get; set; } = true;

    public bool ValueShowSubtitle { get; set; } = true;

    public string ValueTitleColor { get; set; } = DashboardWidgetCatalog.KpiDefaultTitleColor;

    public string ValueSubtitleColor { get; set; } = DashboardWidgetCatalog.KpiDefaultSubtitleColor;

    public string ValueValueColor { get; set; } = DashboardWidgetCatalog.KpiDefaultValueColor;

    public string ValueTitleAlign { get; set; } = DashboardWidgetCatalog.KpiAlignLeft;

    public string ValueValueAlign { get; set; } = DashboardWidgetCatalog.KpiAlignLeft;

    public string ValueValuePlacement { get; set; } = DashboardWidgetCatalog.KpiValuePlacementTop;

    public string DigitalLabel { get; set; } = DashboardWidgetCatalog.MetricValueDefaultTitle;

    public bool DigitalShowLabel { get; set; } = true;

    public string DigitalLabelPlacement { get; set; } = DashboardWidgetCatalog.MetricDigitalLabelPlacementBottom;

    public string DigitalStyle { get; set; } = DashboardWidgetCatalog.MetricDigitalStylePanel;

    public string DigitalGlow { get; set; } = DashboardWidgetCatalog.MetricDigitalGlowSoft;

    public string DigitalBackgroundColor { get; set; } = DashboardWidgetCatalog.MetricDigitalDefaultBackgroundColor;

    public string DigitalSegmentColor { get; set; } = DashboardWidgetCatalog.MetricDigitalDefaultSegmentColor;

    public string DigitalInactiveSegmentColor { get; set; } = DashboardWidgetCatalog.MetricDigitalDefaultInactiveSegmentColor;

    public string DigitalLabelColor { get; set; } = DashboardWidgetCatalog.MetricDigitalDefaultLabelColor;

    public string DigitalBorderColor { get; set; } = DashboardWidgetCatalog.MetricDigitalDefaultBorderColor;

    public int DigitalBorderWidth { get; set; } = DashboardWidgetCatalog.MetricDigitalDefaultBorderWidth;

    public int DigitalRadius { get; set; } = DashboardWidgetCatalog.MetricDigitalDefaultRadius;

    public int DigitalPadding { get; set; } = DashboardWidgetCatalog.MetricDigitalDefaultPadding;

    public int DigitalDigits { get; set; } = DashboardWidgetCatalog.MetricDigitalDefaultDigits;

    public string DigitalFitMode { get; set; } = DashboardWidgetCatalog.MetricDigitalFitCompact;

    public string DisplayName =>
        DashboardMetricVisualizationCatalog.Find(VisualizationId)?.DisplayName ?? "Value";

    public static DashboardMetricVisualizationSettingsDraft Create(DashboardWidgetSnapshot widget)
    {
        var draft = new DashboardMetricVisualizationSettingsDraft();
        draft.LoadFromWidget(widget);
        return draft;
    }

    public DashboardMetricVisualizationSettingsDraft Copy()
    {
        var copy = new DashboardMetricVisualizationSettingsDraft();
        copy.LoadFromConfiguration(BuildConfiguration(), useCompatibilityFallbacks: false);
        return copy;
    }

    public void SetVisualization(string? value, bool applyDefaults)
    {
        VisualizationId = DashboardWidgetCatalog.NormalizeMetricVisualization(value);
        if (applyDefaults)
        {
            ResetSelectedVisual();
        }
    }

    public void ResetSelectedVisual()
    {
        var defaults = DashboardMetricVisualizationCatalog.Find(VisualizationId)?.DefaultConfiguration ??
            DashboardMetricVisualizationCatalog.Find(DashboardMetricVisualizationIds.Value)!.DefaultConfiguration;
        LoadFromConfiguration(defaults, useCompatibilityFallbacks: false);
    }

    public void ApplyConfiguration(IReadOnlyDictionary<string, string> configuration)
        => LoadFromConfiguration(configuration, useCompatibilityFallbacks: false);

    public IReadOnlyDictionary<string, string> BuildConfiguration()
    {
        var configuration = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [DashboardWidgetCatalog.MetricVisualizationKey] = VisualizationId
        };

        if (string.Equals(VisualizationId, DashboardMetricVisualizationIds.Digital, StringComparison.Ordinal))
        {
            configuration[DashboardWidgetCatalog.MetricDigitalLabelKey] =
                NormalizeText(DigitalLabel, DashboardWidgetCatalog.MetricValueDefaultTitle);
            configuration[DashboardWidgetCatalog.MetricDigitalShowLabelKey] = DigitalShowLabel ? "true" : "false";
            configuration[DashboardWidgetCatalog.MetricDigitalLabelPlacementKey] =
                DashboardWidgetCatalog.NormalizeMetricDigitalLabelPlacement(DigitalLabelPlacement);
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
            configuration[DashboardWidgetCatalog.MetricDigitalBorderColorKey] =
                NormalizeColor(DigitalBorderColor, DashboardWidgetCatalog.MetricDigitalDefaultBorderColor);
            configuration[DashboardWidgetCatalog.MetricDigitalBorderWidthKey] =
                Clamp(DigitalBorderWidth, 0, 8).ToString(CultureInfo.InvariantCulture);
            configuration[DashboardWidgetCatalog.MetricDigitalRadiusKey] =
                Clamp(DigitalRadius, 0, 32).ToString(CultureInfo.InvariantCulture);
            configuration[DashboardWidgetCatalog.MetricDigitalPaddingKey] =
                Clamp(DigitalPadding, 0, 32).ToString(CultureInfo.InvariantCulture);
            configuration[DashboardWidgetCatalog.MetricDigitalDigitsKey] =
                DashboardWidgetCatalog.NormalizeMetricDigitalDigits(DigitalDigits).ToString(CultureInfo.InvariantCulture);
            configuration[DashboardWidgetCatalog.MetricDigitalFitModeKey] =
                DashboardWidgetCatalog.NormalizeMetricDigitalFitMode(DigitalFitMode);
            return configuration;
        }

        configuration[DashboardWidgetCatalog.MetricValueTitleKey] =
            NormalizeText(ValueTitle, DashboardWidgetCatalog.MetricValueDefaultTitle);
        configuration[DashboardWidgetCatalog.MetricValueSubtitleKey] =
            NormalizeText(ValueSubtitle, DashboardWidgetCatalog.MetricValueDefaultSubtitle);
        configuration[DashboardWidgetCatalog.MetricValueShowTitleKey] = ValueShowTitle ? "true" : "false";
        configuration[DashboardWidgetCatalog.MetricValueShowSubtitleKey] = ValueShowSubtitle ? "true" : "false";
        configuration[DashboardWidgetCatalog.MetricValueTitleColorKey] =
            NormalizeColor(ValueTitleColor, DashboardWidgetCatalog.KpiDefaultTitleColor);
        configuration[DashboardWidgetCatalog.MetricValueSubtitleColorKey] =
            NormalizeColor(ValueSubtitleColor, DashboardWidgetCatalog.KpiDefaultSubtitleColor);
        configuration[DashboardWidgetCatalog.MetricValueValueColorKey] =
            NormalizeColor(ValueValueColor, DashboardWidgetCatalog.KpiDefaultValueColor);
        configuration[DashboardWidgetCatalog.MetricValueTitleAlignKey] =
            DashboardWidgetCatalog.NormalizeKpiHorizontalAlignment(ValueTitleAlign);
        configuration[DashboardWidgetCatalog.MetricValueValueAlignKey] =
            DashboardWidgetCatalog.NormalizeKpiHorizontalAlignment(ValueValueAlign);
        configuration[DashboardWidgetCatalog.MetricValueValuePlacementKey] =
            DashboardWidgetCatalog.NormalizeKpiValuePlacement(ValueValuePlacement);
        return configuration;
    }

    public IReadOnlyDictionary<string, string> ApplyToConfiguration(IReadOnlyDictionary<string, string> configuration)
    {
        var next = configuration
            .Where(static pair => !VisualConfigurationKeys.Contains(pair.Key))
            .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);
        foreach (var (key, value) in BuildConfiguration())
        {
            next[key] = value;
        }

        return next;
    }

    public string Summary()
        => string.Equals(VisualizationId, DashboardMetricVisualizationIds.Digital, StringComparison.Ordinal)
            ? $"Digital, {DigitalDigits} digits, {Readable(DigitalStyle)}, {Readable(DigitalGlow)} glow"
            : $"Value, {Readable(ValueValuePlacement)}, title {(ValueShowTitle ? "shown" : "hidden")}, subtitle {(ValueShowSubtitle ? "shown" : "hidden")}";

    private void LoadFromWidget(DashboardWidgetSnapshot widget)
    {
        VisualizationId = DashboardWidgetCatalog.NormalizeMetricVisualization(
            widget.ReadString(DashboardWidgetCatalog.MetricVisualizationKey));
        LoadFromConfiguration(widget.Configuration, useCompatibilityFallbacks: true);
    }

    private void LoadFromConfiguration(
        IReadOnlyDictionary<string, string> configuration,
        bool useCompatibilityFallbacks)
    {
        VisualizationId = DashboardWidgetCatalog.NormalizeMetricVisualization(
            ReadString(configuration, DashboardWidgetCatalog.MetricVisualizationKey) ?? VisualizationId);

        ValueTitle = ReadString(configuration, DashboardWidgetCatalog.MetricValueTitleKey) ??
            (useCompatibilityFallbacks ? ReadString(configuration, "title") : null) ??
            DashboardWidgetCatalog.MetricValueDefaultTitle;
        ValueSubtitle = ReadString(configuration, DashboardWidgetCatalog.MetricValueSubtitleKey) ??
            (useCompatibilityFallbacks ? ReadString(configuration, "subtitle") : null) ??
            DashboardWidgetCatalog.MetricValueDefaultSubtitle;
        ValueShowTitle = ReadBool(configuration, DashboardWidgetCatalog.MetricValueShowTitleKey, true);
        ValueShowSubtitle = ReadBool(configuration, DashboardWidgetCatalog.MetricValueShowSubtitleKey, true);
        ValueTitleColor = NormalizeColor(
            ReadString(configuration, DashboardWidgetCatalog.MetricValueTitleColorKey) ??
            (useCompatibilityFallbacks ? ReadString(configuration, DashboardWidgetCatalog.KpiTitleColorKey) : null) ??
            (useCompatibilityFallbacks ? ReadString(configuration, "style.titleColor") : null),
            DashboardWidgetCatalog.KpiDefaultTitleColor);
        ValueSubtitleColor = NormalizeColor(
            ReadString(configuration, DashboardWidgetCatalog.MetricValueSubtitleColorKey) ??
            (useCompatibilityFallbacks ? ReadString(configuration, DashboardWidgetCatalog.KpiSubtitleColorKey) : null) ??
            (useCompatibilityFallbacks ? ReadString(configuration, "style.subtitleColor") : null),
            DashboardWidgetCatalog.KpiDefaultSubtitleColor);
        ValueValueColor = NormalizeColor(
            ReadString(configuration, DashboardWidgetCatalog.MetricValueValueColorKey) ??
            (useCompatibilityFallbacks ? ReadString(configuration, DashboardWidgetCatalog.KpiValueColorKey) : null) ??
            (useCompatibilityFallbacks ? ReadString(configuration, "style.valueColor") : null),
            DashboardWidgetCatalog.KpiDefaultValueColor);
        ValueTitleAlign = DashboardWidgetCatalog.NormalizeKpiHorizontalAlignment(
            ReadString(configuration, DashboardWidgetCatalog.MetricValueTitleAlignKey) ??
            (useCompatibilityFallbacks ? ReadString(configuration, DashboardWidgetCatalog.KpiTitleAlignKey) : null));
        ValueValueAlign = DashboardWidgetCatalog.NormalizeKpiHorizontalAlignment(
            ReadString(configuration, DashboardWidgetCatalog.MetricValueValueAlignKey) ??
            (useCompatibilityFallbacks ? ReadString(configuration, DashboardWidgetCatalog.KpiValueAlignKey) : null));
        ValueValuePlacement = DashboardWidgetCatalog.NormalizeKpiValuePlacement(
            ReadString(configuration, DashboardWidgetCatalog.MetricValueValuePlacementKey) ??
            (useCompatibilityFallbacks ? ReadString(configuration, DashboardWidgetCatalog.KpiValuePlacementKey) : null));

        DigitalLabel = ReadString(configuration, DashboardWidgetCatalog.MetricDigitalLabelKey) ??
            (useCompatibilityFallbacks ? ReadString(configuration, "title") : null) ??
            DashboardWidgetCatalog.MetricValueDefaultTitle;
        DigitalShowLabel = ReadBool(configuration, DashboardWidgetCatalog.MetricDigitalShowLabelKey, true);
        DigitalLabelPlacement = DashboardWidgetCatalog.NormalizeMetricDigitalLabelPlacement(
            ReadString(configuration, DashboardWidgetCatalog.MetricDigitalLabelPlacementKey));
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
        DigitalBorderColor = NormalizeColor(
            ReadString(configuration, DashboardWidgetCatalog.MetricDigitalBorderColorKey),
            DashboardWidgetCatalog.MetricDigitalDefaultBorderColor);
        DigitalBorderWidth = ReadInt(
            configuration,
            DashboardWidgetCatalog.MetricDigitalBorderWidthKey,
            DashboardWidgetCatalog.MetricDigitalDefaultBorderWidth,
            0,
            8);
        DigitalRadius = ReadInt(
            configuration,
            DashboardWidgetCatalog.MetricDigitalRadiusKey,
            DashboardWidgetCatalog.MetricDigitalDefaultRadius,
            0,
            32);
        DigitalPadding = ReadInt(
            configuration,
            DashboardWidgetCatalog.MetricDigitalPaddingKey,
            DashboardWidgetCatalog.MetricDigitalDefaultPadding,
            0,
            32);
        DigitalDigits = DashboardWidgetCatalog.NormalizeMetricDigitalDigits(
            ReadString(configuration, DashboardWidgetCatalog.MetricDigitalDigitsKey));
        DigitalFitMode = DashboardWidgetCatalog.NormalizeMetricDigitalFitMode(
            ReadString(configuration, DashboardWidgetCatalog.MetricDigitalFitModeKey));
    }

    private static string NormalizeText(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string? ReadString(IReadOnlyDictionary<string, string> configuration, string key)
        => configuration.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;

    private static bool ReadBool(
        IReadOnlyDictionary<string, string> configuration,
        string key,
        bool fallback)
        => configuration.TryGetValue(key, out var value) &&
           bool.TryParse(value, out var parsed)
            ? parsed
            : fallback;

    private static int ReadInt(
        IReadOnlyDictionary<string, string> configuration,
        string key,
        int fallback,
        int min,
        int max)
        => configuration.TryGetValue(key, out var value) &&
           int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? Clamp(parsed, min, max)
            : fallback;

    private static int Clamp(int value, int min, int max)
        => Math.Clamp(value, min, max);

    private static string NormalizeColor(string? value, string fallback)
    {
        var normalized = NormalizeText(value, string.Empty).ToLowerInvariant();
        if (string.Equals(normalized, "transparent", StringComparison.Ordinal))
        {
            return normalized;
        }

        return IsHexColor(normalized) ? normalized : fallback;
    }

    private static bool IsHexColor(string value)
        => value.Length is 4 or 5 or 7 or 9 &&
           value[0] == '#' &&
           value.Skip(1).All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static string Readable(string value)
        => string.IsNullOrWhiteSpace(value)
            ? "default"
            : string.Join(
                " ",
                value.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(static part => part.Length == 0 ? part : char.ToUpperInvariant(part[0]) + part[1..]));
}
