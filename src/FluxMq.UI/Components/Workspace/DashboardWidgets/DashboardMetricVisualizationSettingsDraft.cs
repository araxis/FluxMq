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
        DashboardMetricValueVisualizationOptions.TitleKey,
        DashboardMetricValueVisualizationOptions.SubtitleKey,
        DashboardMetricValueVisualizationOptions.ShowTitleKey,
        DashboardMetricValueVisualizationOptions.ShowSubtitleKey,
        DashboardMetricValueVisualizationOptions.ShowUnitKey,
        DashboardMetricValueVisualizationOptions.UnitTextKey,
        DashboardMetricValueVisualizationOptions.TitleColorKey,
        DashboardMetricValueVisualizationOptions.SubtitleColorKey,
        DashboardMetricValueVisualizationOptions.ValueColorKey,
        DashboardMetricValueVisualizationOptions.UnitColorKey,
        DashboardMetricValueVisualizationOptions.TitleAlignKey,
        DashboardMetricValueVisualizationOptions.ValueAlignKey,
        DashboardMetricValueVisualizationOptions.ValuePlacementKey,
        DashboardMetricValueVisualizationOptions.PaddingKey,
        DashboardMetricValueVisualizationOptions.FitModeKey,
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
        DashboardMetricDigitalVisualizationOptions.FitModeKey,
        DashboardMetricDigitalVisualizationOptions.AlignKey,
        DashboardMetricDigitalVisualizationOptions.PlacementKey,
        DashboardMetricGaugeVisualizationOptions.ShapeKey,
        DashboardMetricGaugeVisualizationOptions.LabelKey,
        DashboardMetricGaugeVisualizationOptions.ShowLabelKey,
        DashboardMetricGaugeVisualizationOptions.MinKey,
        DashboardMetricGaugeVisualizationOptions.MaxKey,
        DashboardMetricGaugeVisualizationOptions.TargetKey,
        DashboardMetricGaugeVisualizationOptions.WarningKey,
        DashboardMetricGaugeVisualizationOptions.CriticalKey,
        DashboardMetricGaugeVisualizationOptions.NormalColorKey,
        DashboardMetricGaugeVisualizationOptions.WarningColorKey,
        DashboardMetricGaugeVisualizationOptions.CriticalColorKey,
        DashboardEventGaugeWidgetOptions.StyleKey,
        DashboardEventGaugeWidgetOptions.MinKey,
        DashboardEventGaugeWidgetOptions.MaxKey,
        DashboardEventGaugeWidgetOptions.TargetKey,
        DashboardEventGaugeWidgetOptions.WarningKey,
        DashboardEventGaugeWidgetOptions.CriticalKey,
        DashboardEventGaugeWidgetOptions.NormalColorKey,
        DashboardEventGaugeWidgetOptions.WarningColorKey,
        DashboardEventGaugeWidgetOptions.CriticalColorKey
    };

    public string VisualizationId { get; private set; } = DashboardMetricVisualizationIds.Value;

    public string ValueTitle { get; set; } = DashboardMetricValueVisualizationOptions.DefaultTitle;

    public string ValueSubtitle { get; set; } = DashboardMetricValueVisualizationOptions.DefaultSubtitle;

    public bool ValueShowTitle { get; set; } = true;

    public bool ValueShowSubtitle { get; set; } = true;

    public bool ValueShowUnit { get; set; } = true;

    public string ValueUnitText { get; set; } = DashboardMetricValueVisualizationOptions.DefaultUnitText;

    public string ValueTitleColor { get; set; } = DashboardMetricValueVisualizationOptions.DefaultTitleColor;

    public string ValueSubtitleColor { get; set; } = DashboardMetricValueVisualizationOptions.DefaultSubtitleColor;

    public string ValueValueColor { get; set; } = DashboardMetricValueVisualizationOptions.DefaultValueColor;

    public string ValueUnitColor { get; set; } = DashboardMetricValueVisualizationOptions.DefaultUnitColor;

    public string ValueTitleAlign { get; set; } = DashboardMetricValueVisualizationOptions.AlignLeft;

    public string ValueValueAlign { get; set; } = DashboardMetricValueVisualizationOptions.AlignLeft;

    public string ValueValuePlacement { get; set; } = DashboardMetricValueVisualizationOptions.ValuePlacementTop;

    public int ValuePadding { get; set; } = DashboardMetricValueVisualizationOptions.DefaultPadding;

    public string ValueFitMode { get; set; } = DashboardMetricValueVisualizationOptions.FitFill;

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

    public string DigitalAlign { get; set; } = DashboardMetricDigitalVisualizationOptions.AlignCenter;

    public string DigitalPlacement { get; set; } = DashboardMetricDigitalVisualizationOptions.PlacementMiddle;

    public string GaugeShape { get; set; } = DashboardMetricGaugeVisualizationOptions.ShapeRing;

    public string GaugeLabel { get; set; } = DashboardMetricGaugeVisualizationOptions.DefaultLabel;

    public bool GaugeShowLabel { get; set; } = true;

    public string GaugeMin { get; set; } = DashboardMetricGaugeVisualizationOptions.DefaultMin;

    public string GaugeMax { get; set; } = DashboardMetricGaugeVisualizationOptions.DefaultMax;

    public string GaugeTarget { get; set; } = DashboardMetricGaugeVisualizationOptions.DefaultTarget;

    public string GaugeWarning { get; set; } = DashboardMetricGaugeVisualizationOptions.DefaultWarning;

    public string GaugeCritical { get; set; } = DashboardMetricGaugeVisualizationOptions.DefaultCritical;

    public string GaugeNormalColor { get; set; } = DashboardMetricGaugeVisualizationOptions.DefaultNormalColor;

    public string GaugeWarningColor { get; set; } = DashboardMetricGaugeVisualizationOptions.DefaultWarningColor;

    public string GaugeCriticalColor { get; set; } = DashboardMetricGaugeVisualizationOptions.DefaultCriticalColor;

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
            configuration[DashboardMetricDigitalVisualizationOptions.AlignKey] =
                DashboardMetricDigitalVisualizationOptions.NormalizeAlignment(DigitalAlign);
            configuration[DashboardMetricDigitalVisualizationOptions.PlacementKey] =
                DashboardMetricDigitalVisualizationOptions.NormalizePlacement(DigitalPlacement);
            return configuration;
        }

        if (string.Equals(VisualizationId, DashboardMetricVisualizationIds.RadialGauge, StringComparison.Ordinal))
        {
            configuration[DashboardMetricGaugeVisualizationOptions.ShapeKey] =
                DashboardMetricGaugeVisualizationOptions.NormalizeShape(GaugeShape);
            configuration[DashboardMetricGaugeVisualizationOptions.LabelKey] =
                NormalizeText(GaugeLabel, DashboardMetricGaugeVisualizationOptions.DefaultLabel);
            configuration[DashboardMetricGaugeVisualizationOptions.ShowLabelKey] = GaugeShowLabel ? "true" : "false";
            configuration[DashboardMetricGaugeVisualizationOptions.MinKey] =
                NormalizeNumber(GaugeMin, DashboardMetricGaugeVisualizationOptions.DefaultMin);
            configuration[DashboardMetricGaugeVisualizationOptions.MaxKey] =
                NormalizeNumber(GaugeMax, DashboardMetricGaugeVisualizationOptions.DefaultMax);
            configuration[DashboardMetricGaugeVisualizationOptions.TargetKey] =
                NormalizeNumber(GaugeTarget, DashboardMetricGaugeVisualizationOptions.DefaultTarget);
            configuration[DashboardMetricGaugeVisualizationOptions.WarningKey] =
                NormalizeNumber(GaugeWarning, DashboardMetricGaugeVisualizationOptions.DefaultWarning);
            configuration[DashboardMetricGaugeVisualizationOptions.CriticalKey] =
                NormalizeNumber(GaugeCritical, DashboardMetricGaugeVisualizationOptions.DefaultCritical);
            configuration[DashboardMetricGaugeVisualizationOptions.NormalColorKey] =
                NormalizeColor(GaugeNormalColor, DashboardMetricGaugeVisualizationOptions.DefaultNormalColor);
            configuration[DashboardMetricGaugeVisualizationOptions.WarningColorKey] =
                NormalizeColor(GaugeWarningColor, DashboardMetricGaugeVisualizationOptions.DefaultWarningColor);
            configuration[DashboardMetricGaugeVisualizationOptions.CriticalColorKey] =
                NormalizeColor(GaugeCriticalColor, DashboardMetricGaugeVisualizationOptions.DefaultCriticalColor);
            return configuration;
        }

        configuration[DashboardMetricValueVisualizationOptions.TitleKey] =
            NormalizeText(ValueTitle, DashboardMetricValueVisualizationOptions.DefaultTitle);
        configuration[DashboardMetricValueVisualizationOptions.SubtitleKey] =
            NormalizeText(ValueSubtitle, DashboardMetricValueVisualizationOptions.DefaultSubtitle);
        configuration[DashboardMetricValueVisualizationOptions.ShowTitleKey] = ValueShowTitle ? "true" : "false";
        configuration[DashboardMetricValueVisualizationOptions.ShowSubtitleKey] = ValueShowSubtitle ? "true" : "false";
        configuration[DashboardMetricValueVisualizationOptions.ShowUnitKey] = ValueShowUnit ? "true" : "false";
        configuration[DashboardMetricValueVisualizationOptions.UnitTextKey] = NormalizeOptionalText(ValueUnitText);
        configuration[DashboardMetricValueVisualizationOptions.TitleColorKey] =
            NormalizeColor(ValueTitleColor, DashboardMetricValueVisualizationOptions.DefaultTitleColor);
        configuration[DashboardMetricValueVisualizationOptions.SubtitleColorKey] =
            NormalizeColor(ValueSubtitleColor, DashboardMetricValueVisualizationOptions.DefaultSubtitleColor);
        configuration[DashboardMetricValueVisualizationOptions.ValueColorKey] =
            NormalizeColor(ValueValueColor, DashboardMetricValueVisualizationOptions.DefaultValueColor);
        configuration[DashboardMetricValueVisualizationOptions.UnitColorKey] =
            NormalizeColor(ValueUnitColor, DashboardMetricValueVisualizationOptions.DefaultUnitColor);
        configuration[DashboardMetricValueVisualizationOptions.TitleAlignKey] =
            DashboardMetricValueVisualizationOptions.NormalizeHorizontalAlignment(ValueTitleAlign);
        configuration[DashboardMetricValueVisualizationOptions.ValueAlignKey] =
            DashboardMetricValueVisualizationOptions.NormalizeHorizontalAlignment(ValueValueAlign);
        configuration[DashboardMetricValueVisualizationOptions.ValuePlacementKey] =
            DashboardMetricValueVisualizationOptions.NormalizeValuePlacement(ValueValuePlacement);
        configuration[DashboardMetricValueVisualizationOptions.PaddingKey] =
            Clamp(ValuePadding, 0, 64).ToString(CultureInfo.InvariantCulture);
        configuration[DashboardMetricValueVisualizationOptions.FitModeKey] =
            DashboardMetricValueVisualizationOptions.NormalizeFitMode(ValueFitMode);
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
        => VisualizationId switch
        {
            DashboardMetricVisualizationIds.Digital => $"Digital, {DigitalDigits} digits, {Readable(DigitalFitMode)}, {Readable(DigitalPlacement)}",
            DashboardMetricVisualizationIds.RadialGauge => $"Gauge, {Readable(GaugeShape)}, {GaugeMin}-{GaugeMax}, target {GaugeTarget}",
            _ => $"Value, {Readable(ValueFitMode)}, {Readable(ValueValuePlacement)}, unit {(ValueShowUnit ? "shown" : "hidden")}"
        };

    private void LoadFromWidget(DashboardWidgetSnapshot widget)
    {
        VisualizationId = DashboardWidgetCatalog.NormalizeMetricVisualization(
            widget.ReadString(DashboardWidgetCatalog.MetricVisualizationKey) ??
            DefaultVisualizationForWidget(widget.Type));
        LoadFromConfiguration(widget.Configuration, useCompatibilityFallbacks: true);
    }

    private static string DefaultVisualizationForWidget(string type)
        => string.Equals(type, DashboardWidgetCatalog.EventGaugeType, StringComparison.Ordinal)
            ? DashboardMetricVisualizationIds.RadialGauge
            : DashboardMetricVisualizationIds.Value;

    private void LoadFromConfiguration(
        IReadOnlyDictionary<string, string> configuration,
        bool useCompatibilityFallbacks)
    {
        VisualizationId = DashboardWidgetCatalog.NormalizeMetricVisualization(
            ReadString(configuration, DashboardWidgetCatalog.MetricVisualizationKey) ?? VisualizationId);

        ValueTitle = ReadString(configuration, DashboardMetricValueVisualizationOptions.TitleKey) ??
            (useCompatibilityFallbacks ? ReadString(configuration, "title") : null) ??
            DashboardMetricValueVisualizationOptions.DefaultTitle;
        ValueSubtitle = ReadString(configuration, DashboardMetricValueVisualizationOptions.SubtitleKey) ??
            (useCompatibilityFallbacks ? ReadString(configuration, "subtitle") : null) ??
            DashboardMetricValueVisualizationOptions.DefaultSubtitle;
        ValueShowTitle = ReadBool(configuration, DashboardMetricValueVisualizationOptions.ShowTitleKey, true);
        ValueShowSubtitle = ReadBool(configuration, DashboardMetricValueVisualizationOptions.ShowSubtitleKey, true);
        ValueShowUnit = ReadBool(configuration, DashboardMetricValueVisualizationOptions.ShowUnitKey, true);
        ValueUnitText = ReadString(configuration, DashboardMetricValueVisualizationOptions.UnitTextKey) ??
            DashboardMetricValueVisualizationOptions.DefaultUnitText;
        ValueTitleColor = NormalizeColor(
            ReadString(configuration, DashboardMetricValueVisualizationOptions.TitleColorKey) ??
            (useCompatibilityFallbacks ? ReadString(configuration, DashboardWidgetCatalog.KpiTitleColorKey) : null) ??
            (useCompatibilityFallbacks ? ReadString(configuration, "style.titleColor") : null),
            DashboardMetricValueVisualizationOptions.DefaultTitleColor);
        ValueSubtitleColor = NormalizeColor(
            ReadString(configuration, DashboardMetricValueVisualizationOptions.SubtitleColorKey) ??
            (useCompatibilityFallbacks ? ReadString(configuration, DashboardWidgetCatalog.KpiSubtitleColorKey) : null) ??
            (useCompatibilityFallbacks ? ReadString(configuration, "style.subtitleColor") : null),
            DashboardMetricValueVisualizationOptions.DefaultUnitColor);
        ValueValueColor = NormalizeColor(
            ReadString(configuration, DashboardMetricValueVisualizationOptions.ValueColorKey) ??
            (useCompatibilityFallbacks ? ReadString(configuration, DashboardWidgetCatalog.KpiValueColorKey) : null) ??
            (useCompatibilityFallbacks ? ReadString(configuration, "style.valueColor") : null),
            DashboardMetricValueVisualizationOptions.DefaultValueColor);
        ValueUnitColor = NormalizeColor(
            ReadString(configuration, DashboardMetricValueVisualizationOptions.UnitColorKey),
            DashboardMetricValueVisualizationOptions.DefaultSubtitleColor);
        ValueTitleAlign = DashboardMetricValueVisualizationOptions.NormalizeHorizontalAlignment(
            ReadString(configuration, DashboardMetricValueVisualizationOptions.TitleAlignKey) ??
            (useCompatibilityFallbacks ? ReadString(configuration, DashboardWidgetCatalog.KpiTitleAlignKey) : null));
        ValueValueAlign = DashboardMetricValueVisualizationOptions.NormalizeHorizontalAlignment(
            ReadString(configuration, DashboardMetricValueVisualizationOptions.ValueAlignKey) ??
            (useCompatibilityFallbacks ? ReadString(configuration, DashboardWidgetCatalog.KpiValueAlignKey) : null));
        ValueValuePlacement = DashboardMetricValueVisualizationOptions.NormalizeValuePlacement(
            ReadString(configuration, DashboardMetricValueVisualizationOptions.ValuePlacementKey) ??
            (useCompatibilityFallbacks ? ReadString(configuration, DashboardWidgetCatalog.KpiValuePlacementKey) : null));
        ValuePadding = ReadInt(
            configuration,
            DashboardMetricValueVisualizationOptions.PaddingKey,
            ReadInt(
                configuration,
                "style.padding",
                DashboardMetricValueVisualizationOptions.DefaultPadding,
                0,
                64),
            0,
            64);
        ValueFitMode = DashboardMetricValueVisualizationOptions.NormalizeFitMode(
            ReadString(configuration, DashboardMetricValueVisualizationOptions.FitModeKey));

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
        DigitalAlign = DashboardMetricDigitalVisualizationOptions.NormalizeAlignment(
            ReadString(configuration, DashboardMetricDigitalVisualizationOptions.AlignKey));
        DigitalPlacement = DashboardMetricDigitalVisualizationOptions.NormalizePlacement(
            ReadString(configuration, DashboardMetricDigitalVisualizationOptions.PlacementKey));

        GaugeShape = DashboardMetricGaugeVisualizationOptions.NormalizeShape(
            ReadString(configuration, DashboardMetricGaugeVisualizationOptions.ShapeKey) ??
            (useCompatibilityFallbacks ? ReadString(configuration, DashboardEventGaugeWidgetOptions.StyleKey) : null));
        GaugeLabel = ReadString(configuration, DashboardMetricGaugeVisualizationOptions.LabelKey) ??
            (useCompatibilityFallbacks ? ReadString(configuration, "title") : null) ??
            DashboardMetricGaugeVisualizationOptions.DefaultLabel;
        GaugeShowLabel = ReadBool(configuration, DashboardMetricGaugeVisualizationOptions.ShowLabelKey, true);
        GaugeMin = NormalizeNumber(
            ReadString(configuration, DashboardMetricGaugeVisualizationOptions.MinKey) ??
            (useCompatibilityFallbacks ? ReadString(configuration, DashboardEventGaugeWidgetOptions.MinKey) : null),
            DashboardMetricGaugeVisualizationOptions.DefaultMin);
        GaugeMax = NormalizeNumber(
            ReadString(configuration, DashboardMetricGaugeVisualizationOptions.MaxKey) ??
            (useCompatibilityFallbacks ? ReadString(configuration, DashboardEventGaugeWidgetOptions.MaxKey) : null),
            DashboardMetricGaugeVisualizationOptions.DefaultMax);
        GaugeTarget = NormalizeNumber(
            ReadString(configuration, DashboardMetricGaugeVisualizationOptions.TargetKey) ??
            (useCompatibilityFallbacks ? ReadString(configuration, DashboardEventGaugeWidgetOptions.TargetKey) : null),
            DashboardMetricGaugeVisualizationOptions.DefaultTarget);
        GaugeWarning = NormalizeNumber(
            ReadString(configuration, DashboardMetricGaugeVisualizationOptions.WarningKey) ??
            (useCompatibilityFallbacks ? ReadString(configuration, DashboardEventGaugeWidgetOptions.WarningKey) : null),
            DashboardMetricGaugeVisualizationOptions.DefaultWarning);
        GaugeCritical = NormalizeNumber(
            ReadString(configuration, DashboardMetricGaugeVisualizationOptions.CriticalKey) ??
            (useCompatibilityFallbacks ? ReadString(configuration, DashboardEventGaugeWidgetOptions.CriticalKey) : null),
            DashboardMetricGaugeVisualizationOptions.DefaultCritical);
        GaugeNormalColor = NormalizeColor(
            ReadString(configuration, DashboardMetricGaugeVisualizationOptions.NormalColorKey) ??
            (useCompatibilityFallbacks ? ReadString(configuration, DashboardEventGaugeWidgetOptions.NormalColorKey) : null),
            DashboardMetricGaugeVisualizationOptions.DefaultNormalColor);
        GaugeWarningColor = NormalizeColor(
            ReadString(configuration, DashboardMetricGaugeVisualizationOptions.WarningColorKey) ??
            (useCompatibilityFallbacks ? ReadString(configuration, DashboardEventGaugeWidgetOptions.WarningColorKey) : null),
            DashboardMetricGaugeVisualizationOptions.DefaultWarningColor);
        GaugeCriticalColor = NormalizeColor(
            ReadString(configuration, DashboardMetricGaugeVisualizationOptions.CriticalColorKey) ??
            (useCompatibilityFallbacks ? ReadString(configuration, DashboardEventGaugeWidgetOptions.CriticalColorKey) : null),
            DashboardMetricGaugeVisualizationOptions.DefaultCriticalColor);
    }

    private static string NormalizeText(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string NormalizeOptionalText(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string NormalizeNumber(string? value, string fallback)
        => double.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var parsed) &&
           double.IsFinite(parsed)
            ? parsed.ToString("0.###", CultureInfo.InvariantCulture)
            : fallback;

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
