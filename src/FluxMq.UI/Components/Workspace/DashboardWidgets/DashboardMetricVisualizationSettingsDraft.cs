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
        DashboardWidgetCatalog.MetricValueShowUnitKey,
        DashboardWidgetCatalog.MetricValueUnitTextKey,
        DashboardWidgetCatalog.MetricValueTitleColorKey,
        DashboardWidgetCatalog.MetricValueSubtitleColorKey,
        DashboardWidgetCatalog.MetricValueValueColorKey,
        DashboardWidgetCatalog.MetricValueUnitColorKey,
        DashboardWidgetCatalog.MetricValueTitleAlignKey,
        DashboardWidgetCatalog.MetricValueValueAlignKey,
        DashboardWidgetCatalog.MetricValueValuePlacementKey,
        DashboardWidgetCatalog.MetricValuePaddingKey,
        DashboardMetricDigitalVisualizationOptions.LabelKey,
        DashboardMetricDigitalVisualizationOptions.ShowLabelKey,
        DashboardMetricDigitalVisualizationOptions.LabelPlacementKey,
        DashboardMetricDigitalVisualizationOptions.StyleKey,
        DashboardMetricDigitalVisualizationOptions.GlowKey,
        DashboardMetricDigitalVisualizationOptions.BackgroundColorKey,
        DashboardMetricDigitalVisualizationOptions.SegmentColorKey,
        DashboardMetricDigitalVisualizationOptions.InactiveSegmentColorKey,
        DashboardMetricDigitalVisualizationOptions.LabelColorKey,
        DashboardMetricDigitalVisualizationOptions.DigitsKey,
        DashboardMetricDigitalVisualizationOptions.BorderColorKey,
        DashboardMetricDigitalVisualizationOptions.BorderWidthKey,
        DashboardMetricDigitalVisualizationOptions.RadiusKey,
        DashboardMetricDigitalVisualizationOptions.PaddingKey,
        DashboardMetricDigitalVisualizationOptions.FitModeKey
    };

    public string VisualizationId { get; private set; } = DashboardMetricVisualizationIds.Value;

    public string ValueTitle { get; set; } = DashboardWidgetCatalog.MetricValueDefaultTitle;

    public string ValueSubtitle { get; set; } = DashboardWidgetCatalog.MetricValueDefaultSubtitle;

    public bool ValueShowTitle { get; set; } = true;

    public bool ValueShowSubtitle { get; set; } = true;

    public bool ValueShowUnit { get; set; } = true;

    public string ValueUnitText { get; set; } = DashboardWidgetCatalog.MetricValueDefaultUnitText;

    public string ValueTitleColor { get; set; } = DashboardWidgetCatalog.KpiDefaultTitleColor;

    public string ValueSubtitleColor { get; set; } = DashboardWidgetCatalog.KpiDefaultSubtitleColor;

    public string ValueValueColor { get; set; } = DashboardWidgetCatalog.KpiDefaultValueColor;

    public string ValueUnitColor { get; set; } = DashboardWidgetCatalog.KpiDefaultSubtitleColor;

    public string ValueTitleAlign { get; set; } = DashboardWidgetCatalog.KpiAlignLeft;

    public string ValueValueAlign { get; set; } = DashboardWidgetCatalog.KpiAlignLeft;

    public string ValueValuePlacement { get; set; } = DashboardWidgetCatalog.KpiValuePlacementTop;

    public int ValuePadding { get; set; } = DashboardWidgetCatalog.MetricValueDefaultPadding;

    public string DigitalLabel { get; set; } = DashboardMetricDigitalVisualizationOptions.DefaultLabel;

    public bool DigitalShowLabel { get; set; } = true;

    public string DigitalLabelPlacement { get; set; } = DashboardMetricDigitalVisualizationOptions.LabelPlacementBottom;

    public string DigitalStyle { get; set; } = DashboardMetricDigitalVisualizationOptions.StylePanel;

    public string DigitalGlow { get; set; } = DashboardMetricDigitalVisualizationOptions.GlowSoft;

    public string DigitalBackgroundColor { get; set; } = DashboardMetricDigitalVisualizationOptions.DefaultBackgroundColor;

    public string DigitalSegmentColor { get; set; } = DashboardMetricDigitalVisualizationOptions.DefaultSegmentColor;

    public string DigitalInactiveSegmentColor { get; set; } = DashboardMetricDigitalVisualizationOptions.DefaultInactiveSegmentColor;

    public string DigitalLabelColor { get; set; } = DashboardMetricDigitalVisualizationOptions.DefaultLabelColor;

    public string DigitalBorderColor { get; set; } = DashboardMetricDigitalVisualizationOptions.DefaultBorderColor;

    public int DigitalBorderWidth { get; set; } = DashboardMetricDigitalVisualizationOptions.DefaultBorderWidth;

    public int DigitalRadius { get; set; } = DashboardMetricDigitalVisualizationOptions.DefaultRadius;

    public int DigitalPadding { get; set; } = DashboardMetricDigitalVisualizationOptions.DefaultPadding;

    public int DigitalDigits { get; set; } = DashboardMetricDigitalVisualizationOptions.DefaultDigits;

    public string DigitalFitMode { get; set; } = DashboardMetricDigitalVisualizationOptions.FitCompact;

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
            configuration[DashboardMetricDigitalVisualizationOptions.LabelKey] =
                NormalizeText(DigitalLabel, DashboardMetricDigitalVisualizationOptions.DefaultLabel);
            configuration[DashboardMetricDigitalVisualizationOptions.ShowLabelKey] = DigitalShowLabel ? "true" : "false";
            configuration[DashboardMetricDigitalVisualizationOptions.LabelPlacementKey] =
                DashboardMetricDigitalVisualizationOptions.NormalizeLabelPlacement(DigitalLabelPlacement);
            configuration[DashboardMetricDigitalVisualizationOptions.StyleKey] =
                DashboardMetricDigitalVisualizationOptions.NormalizeStyle(DigitalStyle);
            configuration[DashboardMetricDigitalVisualizationOptions.GlowKey] =
                DashboardMetricDigitalVisualizationOptions.NormalizeGlow(DigitalGlow);
            configuration[DashboardMetricDigitalVisualizationOptions.BackgroundColorKey] =
                NormalizeColor(DigitalBackgroundColor, DashboardMetricDigitalVisualizationOptions.DefaultBackgroundColor);
            configuration[DashboardMetricDigitalVisualizationOptions.SegmentColorKey] =
                NormalizeColor(DigitalSegmentColor, DashboardMetricDigitalVisualizationOptions.DefaultSegmentColor);
            configuration[DashboardMetricDigitalVisualizationOptions.InactiveSegmentColorKey] =
                NormalizeColor(DigitalInactiveSegmentColor, DashboardMetricDigitalVisualizationOptions.DefaultInactiveSegmentColor);
            configuration[DashboardMetricDigitalVisualizationOptions.LabelColorKey] =
                NormalizeColor(DigitalLabelColor, DashboardMetricDigitalVisualizationOptions.DefaultLabelColor);
            configuration[DashboardMetricDigitalVisualizationOptions.BorderColorKey] =
                NormalizeColor(DigitalBorderColor, DashboardMetricDigitalVisualizationOptions.DefaultBorderColor);
            configuration[DashboardMetricDigitalVisualizationOptions.BorderWidthKey] =
                Clamp(DigitalBorderWidth, 0, 8).ToString(CultureInfo.InvariantCulture);
            configuration[DashboardMetricDigitalVisualizationOptions.RadiusKey] =
                Clamp(DigitalRadius, 0, 32).ToString(CultureInfo.InvariantCulture);
            configuration[DashboardMetricDigitalVisualizationOptions.PaddingKey] =
                Clamp(DigitalPadding, 0, 32).ToString(CultureInfo.InvariantCulture);
            configuration[DashboardMetricDigitalVisualizationOptions.DigitsKey] =
                DashboardMetricDigitalVisualizationOptions.NormalizeDigits(DigitalDigits).ToString(CultureInfo.InvariantCulture);
            configuration[DashboardMetricDigitalVisualizationOptions.FitModeKey] =
                DashboardMetricDigitalVisualizationOptions.NormalizeFitMode(DigitalFitMode);
            return configuration;
        }

        configuration[DashboardWidgetCatalog.MetricValueTitleKey] =
            NormalizeText(ValueTitle, DashboardWidgetCatalog.MetricValueDefaultTitle);
        configuration[DashboardWidgetCatalog.MetricValueSubtitleKey] =
            NormalizeText(ValueSubtitle, DashboardWidgetCatalog.MetricValueDefaultSubtitle);
        configuration[DashboardWidgetCatalog.MetricValueShowTitleKey] = ValueShowTitle ? "true" : "false";
        configuration[DashboardWidgetCatalog.MetricValueShowSubtitleKey] = ValueShowSubtitle ? "true" : "false";
        configuration[DashboardWidgetCatalog.MetricValueShowUnitKey] = ValueShowUnit ? "true" : "false";
        configuration[DashboardWidgetCatalog.MetricValueUnitTextKey] = NormalizeOptionalText(ValueUnitText);
        configuration[DashboardWidgetCatalog.MetricValueTitleColorKey] =
            NormalizeColor(ValueTitleColor, DashboardWidgetCatalog.KpiDefaultTitleColor);
        configuration[DashboardWidgetCatalog.MetricValueSubtitleColorKey] =
            NormalizeColor(ValueSubtitleColor, DashboardWidgetCatalog.KpiDefaultSubtitleColor);
        configuration[DashboardWidgetCatalog.MetricValueValueColorKey] =
            NormalizeColor(ValueValueColor, DashboardWidgetCatalog.KpiDefaultValueColor);
        configuration[DashboardWidgetCatalog.MetricValueUnitColorKey] =
            NormalizeColor(ValueUnitColor, DashboardWidgetCatalog.KpiDefaultSubtitleColor);
        configuration[DashboardWidgetCatalog.MetricValueTitleAlignKey] =
            DashboardWidgetCatalog.NormalizeKpiHorizontalAlignment(ValueTitleAlign);
        configuration[DashboardWidgetCatalog.MetricValueValueAlignKey] =
            DashboardWidgetCatalog.NormalizeKpiHorizontalAlignment(ValueValueAlign);
        configuration[DashboardWidgetCatalog.MetricValueValuePlacementKey] =
            DashboardWidgetCatalog.NormalizeKpiValuePlacement(ValueValuePlacement);
        configuration[DashboardWidgetCatalog.MetricValuePaddingKey] =
            Clamp(ValuePadding, 0, 64).ToString(CultureInfo.InvariantCulture);
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
            : $"Value, {Readable(ValueValuePlacement)}, title {(ValueShowTitle ? "shown" : "hidden")}, unit {(ValueShowUnit ? "shown" : "hidden")}";

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
        ValueShowUnit = ReadBool(configuration, DashboardWidgetCatalog.MetricValueShowUnitKey, true);
        ValueUnitText = ReadString(configuration, DashboardWidgetCatalog.MetricValueUnitTextKey) ??
            DashboardWidgetCatalog.MetricValueDefaultUnitText;
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
        ValueUnitColor = NormalizeColor(
            ReadString(configuration, DashboardWidgetCatalog.MetricValueUnitColorKey),
            DashboardWidgetCatalog.KpiDefaultSubtitleColor);
        ValueTitleAlign = DashboardWidgetCatalog.NormalizeKpiHorizontalAlignment(
            ReadString(configuration, DashboardWidgetCatalog.MetricValueTitleAlignKey) ??
            (useCompatibilityFallbacks ? ReadString(configuration, DashboardWidgetCatalog.KpiTitleAlignKey) : null));
        ValueValueAlign = DashboardWidgetCatalog.NormalizeKpiHorizontalAlignment(
            ReadString(configuration, DashboardWidgetCatalog.MetricValueValueAlignKey) ??
            (useCompatibilityFallbacks ? ReadString(configuration, DashboardWidgetCatalog.KpiValueAlignKey) : null));
        ValueValuePlacement = DashboardWidgetCatalog.NormalizeKpiValuePlacement(
            ReadString(configuration, DashboardWidgetCatalog.MetricValueValuePlacementKey) ??
            (useCompatibilityFallbacks ? ReadString(configuration, DashboardWidgetCatalog.KpiValuePlacementKey) : null));
        ValuePadding = ReadInt(
            configuration,
            DashboardWidgetCatalog.MetricValuePaddingKey,
            ReadInt(
                configuration,
                "style.padding",
                DashboardWidgetCatalog.MetricValueDefaultPadding,
                0,
                64),
            0,
            64);

        DigitalLabel = ReadString(configuration, DashboardMetricDigitalVisualizationOptions.LabelKey) ??
            (useCompatibilityFallbacks ? ReadString(configuration, "title") : null) ??
            DashboardMetricDigitalVisualizationOptions.DefaultLabel;
        DigitalShowLabel = ReadBool(configuration, DashboardMetricDigitalVisualizationOptions.ShowLabelKey, true);
        DigitalLabelPlacement = DashboardMetricDigitalVisualizationOptions.NormalizeLabelPlacement(
            ReadString(configuration, DashboardMetricDigitalVisualizationOptions.LabelPlacementKey));
        DigitalStyle = DashboardMetricDigitalVisualizationOptions.NormalizeStyle(
            ReadString(configuration, DashboardMetricDigitalVisualizationOptions.StyleKey));
        DigitalGlow = DashboardMetricDigitalVisualizationOptions.NormalizeGlow(
            ReadString(configuration, DashboardMetricDigitalVisualizationOptions.GlowKey));
        DigitalBackgroundColor = NormalizeColor(
            ReadString(configuration, DashboardMetricDigitalVisualizationOptions.BackgroundColorKey),
            DashboardMetricDigitalVisualizationOptions.DefaultBackgroundColor);
        DigitalSegmentColor = NormalizeColor(
            ReadString(configuration, DashboardMetricDigitalVisualizationOptions.SegmentColorKey),
            DashboardMetricDigitalVisualizationOptions.DefaultSegmentColor);
        DigitalInactiveSegmentColor = NormalizeColor(
            ReadString(configuration, DashboardMetricDigitalVisualizationOptions.InactiveSegmentColorKey),
            DashboardMetricDigitalVisualizationOptions.DefaultInactiveSegmentColor);
        DigitalLabelColor = NormalizeColor(
            ReadString(configuration, DashboardMetricDigitalVisualizationOptions.LabelColorKey),
            DashboardMetricDigitalVisualizationOptions.DefaultLabelColor);
        DigitalBorderColor = NormalizeColor(
            ReadString(configuration, DashboardMetricDigitalVisualizationOptions.BorderColorKey),
            DashboardMetricDigitalVisualizationOptions.DefaultBorderColor);
        DigitalBorderWidth = ReadInt(
            configuration,
            DashboardMetricDigitalVisualizationOptions.BorderWidthKey,
            DashboardMetricDigitalVisualizationOptions.DefaultBorderWidth,
            0,
            8);
        DigitalRadius = ReadInt(
            configuration,
            DashboardMetricDigitalVisualizationOptions.RadiusKey,
            DashboardMetricDigitalVisualizationOptions.DefaultRadius,
            0,
            32);
        DigitalPadding = ReadInt(
            configuration,
            DashboardMetricDigitalVisualizationOptions.PaddingKey,
            DashboardMetricDigitalVisualizationOptions.DefaultPadding,
            0,
            32);
        DigitalDigits = DashboardMetricDigitalVisualizationOptions.NormalizeDigits(
            ReadString(configuration, DashboardMetricDigitalVisualizationOptions.DigitsKey));
        DigitalFitMode = DashboardMetricDigitalVisualizationOptions.NormalizeFitMode(
            ReadString(configuration, DashboardMetricDigitalVisualizationOptions.FitModeKey));
    }

    private static string NormalizeText(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string NormalizeOptionalText(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

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
