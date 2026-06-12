using FluxMq.UI.Components.Workspace;
using FluxMq.UI.Models;
using MudBlazor;
using System.Globalization;

namespace FluxMq.UI.Services;

public sealed class DashboardMetricValueVisualizationModuleProvider : IDashboardMetricVisualizationModuleProvider
{
    public string Id => DashboardMetricVisualizationIds.Value;

    public DashboardMetricVisualizationModule CreateModule()
        => new(
            DashboardMetricVisualizationIds.Value,
            "Value",
            Icons.Material.Filled.Pin,
            new HashSet<string>(StringComparer.Ordinal)
            {
                DashboardMetricValueKinds.Number,
                DashboardMetricValueKinds.Rate,
                DashboardMetricValueKinds.Bytes,
                DashboardMetricValueKinds.Percent,
                DashboardMetricValueKinds.Status
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [DashboardWidgetCatalog.MetricVisualizationKey] = DashboardMetricVisualizationIds.Value,
                [DashboardMetricValueVisualizationOptions.TitleKey] = DashboardMetricValueVisualizationOptions.DefaultTitle,
                [DashboardMetricValueVisualizationOptions.SubtitleKey] = DashboardMetricValueVisualizationOptions.DefaultSubtitle,
                [DashboardMetricValueVisualizationOptions.ShowTitleKey] = "true",
                [DashboardMetricValueVisualizationOptions.ShowSubtitleKey] = "true",
                [DashboardMetricValueVisualizationOptions.ShowUnitKey] = "true",
                [DashboardMetricValueVisualizationOptions.UnitTextKey] = DashboardMetricValueVisualizationOptions.DefaultUnitText,
                [DashboardMetricValueVisualizationOptions.TitleColorKey] = DashboardMetricValueVisualizationOptions.DefaultTitleColor,
                [DashboardMetricValueVisualizationOptions.SubtitleColorKey] = DashboardMetricValueVisualizationOptions.DefaultSubtitleColor,
                [DashboardMetricValueVisualizationOptions.ValueColorKey] = DashboardMetricValueVisualizationOptions.DefaultValueColor,
                [DashboardMetricValueVisualizationOptions.UnitColorKey] = DashboardMetricValueVisualizationOptions.DefaultUnitColor,
                [DashboardMetricValueVisualizationOptions.TitleAlignKey] = DashboardMetricValueVisualizationOptions.AlignLeft,
                [DashboardMetricValueVisualizationOptions.ValueAlignKey] = DashboardMetricValueVisualizationOptions.AlignLeft,
                [DashboardMetricValueVisualizationOptions.ValuePlacementKey] = DashboardMetricValueVisualizationOptions.ValuePlacementTop,
                [DashboardMetricValueVisualizationOptions.PaddingKey] = DashboardMetricValueVisualizationOptions.DefaultPadding.ToString(CultureInfo.InvariantCulture),
                [DashboardMetricValueVisualizationOptions.FitModeKey] = DashboardMetricValueVisualizationOptions.FitFill
            },
            [new("value-visual", "Value visual", Properties())],
            typeof(DashboardMetricValueVisualizationView),
            typeof(DashboardMetricValueVisualizationView),
            "Shows one metric result as a title, value, and optional unit.");

    private static IReadOnlyList<DashboardWidgetPropertyDefinition> Properties()
        =>
        [
            new(DashboardMetricValueVisualizationOptions.TitleKey, "Title", DashboardWidgetPropertyEditorKind.Text, DefaultValue: DashboardMetricValueVisualizationOptions.DefaultTitle),
            new(DashboardMetricValueVisualizationOptions.ShowTitleKey, "Show title", DashboardWidgetPropertyEditorKind.Toggle, DefaultValue: "true"),
            new(DashboardMetricValueVisualizationOptions.SubtitleKey, "Subtitle", DashboardWidgetPropertyEditorKind.Text, DefaultValue: DashboardMetricValueVisualizationOptions.DefaultSubtitle),
            new(DashboardMetricValueVisualizationOptions.ShowSubtitleKey, "Show subtitle", DashboardWidgetPropertyEditorKind.Toggle, DefaultValue: "true"),
            new(DashboardMetricValueVisualizationOptions.ShowUnitKey, "Show unit", DashboardWidgetPropertyEditorKind.Toggle, DefaultValue: "true"),
            new(DashboardMetricValueVisualizationOptions.UnitTextKey, "Unit text", DashboardWidgetPropertyEditorKind.Text, DefaultValue: DashboardMetricValueVisualizationOptions.DefaultUnitText, HelpText: "Leave empty to use the metric's natural unit."),
            new(DashboardMetricValueVisualizationOptions.TitleColorKey, "Title color", DashboardWidgetPropertyEditorKind.Color, DefaultValue: DashboardMetricValueVisualizationOptions.DefaultTitleColor),
            new(DashboardMetricValueVisualizationOptions.SubtitleColorKey, "Subtitle color", DashboardWidgetPropertyEditorKind.Color, DefaultValue: DashboardMetricValueVisualizationOptions.DefaultSubtitleColor),
            new(DashboardMetricValueVisualizationOptions.ValueColorKey, "Value color", DashboardWidgetPropertyEditorKind.Color, DefaultValue: DashboardMetricValueVisualizationOptions.DefaultValueColor),
            new(DashboardMetricValueVisualizationOptions.UnitColorKey, "Unit color", DashboardWidgetPropertyEditorKind.Color, DefaultValue: DashboardMetricValueVisualizationOptions.DefaultUnitColor),
            new(DashboardMetricValueVisualizationOptions.TitleAlignKey, "Title align", DashboardWidgetPropertyEditorKind.Segmented, DefaultValue: DashboardMetricValueVisualizationOptions.AlignLeft),
            new(DashboardMetricValueVisualizationOptions.ValueAlignKey, "Value align", DashboardWidgetPropertyEditorKind.Segmented, DefaultValue: DashboardMetricValueVisualizationOptions.AlignLeft),
            new(DashboardMetricValueVisualizationOptions.ValuePlacementKey, "Value place", DashboardWidgetPropertyEditorKind.Segmented, DefaultValue: DashboardMetricValueVisualizationOptions.ValuePlacementTop),
            new(DashboardMetricValueVisualizationOptions.PaddingKey, "Padding", DashboardWidgetPropertyEditorKind.Number, Unit: "px", DefaultValue: DashboardMetricValueVisualizationOptions.DefaultPadding.ToString(CultureInfo.InvariantCulture)),
            new(DashboardMetricValueVisualizationOptions.FitModeKey, "Fit", DashboardWidgetPropertyEditorKind.Select, DefaultValue: DashboardMetricValueVisualizationOptions.FitFill, Options: ValueFitModeOptions())
        ];

    private static IReadOnlyList<DashboardWidgetPropertyOption> ValueFitModeOptions()
        =>
        [
            new(DashboardMetricValueVisualizationOptions.FitFill, "Fill"),
            new(DashboardMetricValueVisualizationOptions.FitCompact, "Compact")
        ];
}

public sealed class DashboardMetricDigitalVisualizationModuleProvider : IDashboardMetricVisualizationModuleProvider
{
    public string Id => DashboardMetricVisualizationIds.Digital;

    public DashboardMetricVisualizationModule CreateModule()
        => new(
            DashboardMetricVisualizationIds.Digital,
            "Digital",
            Icons.Material.Filled.Dialpad,
            new HashSet<string>(StringComparer.Ordinal)
            {
                DashboardMetricValueKinds.Number,
                DashboardMetricValueKinds.Rate,
                DashboardMetricValueKinds.Bytes,
                DashboardMetricValueKinds.Percent
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [DashboardWidgetCatalog.MetricVisualizationKey] = DashboardMetricVisualizationIds.Digital,
                [DashboardMetricDigitalVisualizationOptions.LabelKey] = DashboardMetricDigitalVisualizationOptions.DefaultLabel,
                [DashboardMetricDigitalVisualizationOptions.ShowLabelKey] = "true",
                [DashboardMetricDigitalVisualizationOptions.LabelPlacementKey] = DashboardMetricDigitalVisualizationOptions.LabelPlacementBottom,
                [DashboardMetricDigitalVisualizationOptions.StyleKey] = DashboardMetricDigitalVisualizationOptions.StylePanel,
                [DashboardMetricDigitalVisualizationOptions.GlowKey] = DashboardMetricDigitalVisualizationOptions.GlowSoft,
                [DashboardMetricDigitalVisualizationOptions.BackgroundColorKey] = DashboardMetricDigitalVisualizationOptions.DefaultBackgroundColor,
                [DashboardMetricDigitalVisualizationOptions.SegmentColorKey] = DashboardMetricDigitalVisualizationOptions.DefaultSegmentColor,
                [DashboardMetricDigitalVisualizationOptions.InactiveSegmentColorKey] = DashboardMetricDigitalVisualizationOptions.DefaultInactiveSegmentColor,
                [DashboardMetricDigitalVisualizationOptions.LabelColorKey] = DashboardMetricDigitalVisualizationOptions.DefaultLabelColor,
                [DashboardMetricDigitalVisualizationOptions.DigitsKey] = DashboardMetricDigitalVisualizationOptions.DefaultDigits.ToString(CultureInfo.InvariantCulture),
                [DashboardMetricDigitalVisualizationOptions.BorderColorKey] = DashboardMetricDigitalVisualizationOptions.DefaultBorderColor,
                [DashboardMetricDigitalVisualizationOptions.BorderWidthKey] = DashboardMetricDigitalVisualizationOptions.DefaultBorderWidth.ToString(CultureInfo.InvariantCulture),
                [DashboardMetricDigitalVisualizationOptions.RadiusKey] = DashboardMetricDigitalVisualizationOptions.DefaultRadius.ToString(CultureInfo.InvariantCulture),
                [DashboardMetricDigitalVisualizationOptions.PaddingKey] = DashboardMetricDigitalVisualizationOptions.DefaultPadding.ToString(CultureInfo.InvariantCulture),
                [DashboardMetricDigitalVisualizationOptions.FitModeKey] = DashboardMetricDigitalVisualizationOptions.FitCompact,
                [DashboardMetricDigitalVisualizationOptions.AlignKey] = DashboardMetricDigitalVisualizationOptions.AlignCenter,
                [DashboardMetricDigitalVisualizationOptions.PlacementKey] = DashboardMetricDigitalVisualizationOptions.PlacementMiddle
            },
            [new("digital-visual", "Digital visual", Properties())],
            typeof(DashboardMetricDigitalVisualizationView),
            typeof(DashboardMetricDigitalVisualizationView),
            "Shows one metric result as a compact digital readout.");

    private static IReadOnlyList<DashboardWidgetPropertyDefinition> Properties()
        =>
        [
            new(DashboardMetricDigitalVisualizationOptions.LabelKey, "Label", DashboardWidgetPropertyEditorKind.Text, DefaultValue: DashboardMetricDigitalVisualizationOptions.DefaultLabel),
            new(DashboardMetricDigitalVisualizationOptions.ShowLabelKey, "Show label", DashboardWidgetPropertyEditorKind.Toggle, DefaultValue: "true"),
            new(DashboardMetricDigitalVisualizationOptions.LabelPlacementKey, "Label place", DashboardWidgetPropertyEditorKind.Select, DefaultValue: DashboardMetricDigitalVisualizationOptions.LabelPlacementBottom, Options: LabelPlacementOptions()),
            new(DashboardMetricDigitalVisualizationOptions.StyleKey, "Digit style", DashboardWidgetPropertyEditorKind.Select, DefaultValue: DashboardMetricDigitalVisualizationOptions.StylePanel, Options: StyleOptions()),
            new(DashboardMetricDigitalVisualizationOptions.GlowKey, "Glow", DashboardWidgetPropertyEditorKind.Select, DefaultValue: DashboardMetricDigitalVisualizationOptions.GlowSoft, Options: GlowOptions()),
            new(DashboardMetricDigitalVisualizationOptions.SegmentColorKey, "Segment color", DashboardWidgetPropertyEditorKind.Color, DefaultValue: DashboardMetricDigitalVisualizationOptions.DefaultSegmentColor),
            new(DashboardMetricDigitalVisualizationOptions.InactiveSegmentColorKey, "Inactive color", DashboardWidgetPropertyEditorKind.Color, DefaultValue: DashboardMetricDigitalVisualizationOptions.DefaultInactiveSegmentColor),
            new(DashboardMetricDigitalVisualizationOptions.BackgroundColorKey, "Display bg", DashboardWidgetPropertyEditorKind.Color, DefaultValue: DashboardMetricDigitalVisualizationOptions.DefaultBackgroundColor),
            new(DashboardMetricDigitalVisualizationOptions.LabelColorKey, "Label color", DashboardWidgetPropertyEditorKind.Color, DefaultValue: DashboardMetricDigitalVisualizationOptions.DefaultLabelColor),
            new(DashboardMetricDigitalVisualizationOptions.BorderColorKey, "Border color", DashboardWidgetPropertyEditorKind.Color, DefaultValue: DashboardMetricDigitalVisualizationOptions.DefaultBorderColor),
            new(DashboardMetricDigitalVisualizationOptions.BorderWidthKey, "Border width", DashboardWidgetPropertyEditorKind.Number, Unit: "px", DefaultValue: DashboardMetricDigitalVisualizationOptions.DefaultBorderWidth.ToString(CultureInfo.InvariantCulture)),
            new(DashboardMetricDigitalVisualizationOptions.RadiusKey, "Radius", DashboardWidgetPropertyEditorKind.Number, Unit: "px", DefaultValue: DashboardMetricDigitalVisualizationOptions.DefaultRadius.ToString(CultureInfo.InvariantCulture)),
            new(DashboardMetricDigitalVisualizationOptions.PaddingKey, "Padding", DashboardWidgetPropertyEditorKind.Number, Unit: "px", DefaultValue: DashboardMetricDigitalVisualizationOptions.DefaultPadding.ToString(CultureInfo.InvariantCulture)),
            new(DashboardMetricDigitalVisualizationOptions.DigitsKey, "Digits", DashboardWidgetPropertyEditorKind.Number, DefaultValue: DashboardMetricDigitalVisualizationOptions.DefaultDigits.ToString(CultureInfo.InvariantCulture)),
            new(DashboardMetricDigitalVisualizationOptions.FitModeKey, "Fit", DashboardWidgetPropertyEditorKind.Select, DefaultValue: DashboardMetricDigitalVisualizationOptions.FitCompact, Options: FitModeOptions()),
            new(DashboardMetricDigitalVisualizationOptions.AlignKey, "Readout align", DashboardWidgetPropertyEditorKind.Segmented, DefaultValue: DashboardMetricDigitalVisualizationOptions.AlignCenter),
            new(DashboardMetricDigitalVisualizationOptions.PlacementKey, "Readout place", DashboardWidgetPropertyEditorKind.Segmented, DefaultValue: DashboardMetricDigitalVisualizationOptions.PlacementMiddle)
        ];

    private static IReadOnlyList<DashboardWidgetPropertyOption> LabelPlacementOptions()
        =>
        [
            new(DashboardMetricDigitalVisualizationOptions.LabelPlacementTop, "Top"),
            new(DashboardMetricDigitalVisualizationOptions.LabelPlacementBottom, "Bottom"),
            new(DashboardMetricDigitalVisualizationOptions.LabelPlacementHidden, "Hidden")
        ];

    private static IReadOnlyList<DashboardWidgetPropertyOption> StyleOptions()
        =>
        [
            new(DashboardMetricDigitalVisualizationOptions.StylePanel, "Panel"),
            new(DashboardMetricDigitalVisualizationOptions.StyleSegment, "Segment"),
            new(DashboardMetricDigitalVisualizationOptions.StyleTerminal, "Terminal")
        ];

    private static IReadOnlyList<DashboardWidgetPropertyOption> GlowOptions()
        =>
        [
            new(DashboardMetricDigitalVisualizationOptions.GlowOff, "Off"),
            new(DashboardMetricDigitalVisualizationOptions.GlowSoft, "Soft"),
            new(DashboardMetricDigitalVisualizationOptions.GlowStrong, "Strong")
        ];

    private static IReadOnlyList<DashboardWidgetPropertyOption> FitModeOptions()
        =>
        [
            new(DashboardMetricDigitalVisualizationOptions.FitCompact, "Compact"),
            new(DashboardMetricDigitalVisualizationOptions.FitFill, "Fill")
        ];
}

public sealed class DashboardMetricGaugeVisualizationModuleProvider : IDashboardMetricVisualizationModuleProvider
{
    public string Id => DashboardMetricVisualizationIds.RadialGauge;

    public DashboardMetricVisualizationModule CreateModule()
        => new(
            DashboardMetricVisualizationIds.RadialGauge,
            "Gauge",
            Icons.Material.Filled.Speed,
            new HashSet<string>(StringComparer.Ordinal)
            {
                DashboardMetricValueKinds.Number,
                DashboardMetricValueKinds.Rate,
                DashboardMetricValueKinds.Bytes,
                DashboardMetricValueKinds.Percent
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [DashboardWidgetCatalog.MetricVisualizationKey] = DashboardMetricVisualizationIds.RadialGauge,
                [DashboardMetricGaugeVisualizationOptions.ShapeKey] = DashboardMetricGaugeVisualizationOptions.ShapeRing,
                [DashboardMetricGaugeVisualizationOptions.LabelKey] = DashboardMetricGaugeVisualizationOptions.DefaultLabel,
                [DashboardMetricGaugeVisualizationOptions.ShowLabelKey] = "true",
                [DashboardMetricGaugeVisualizationOptions.MinKey] = DashboardMetricGaugeVisualizationOptions.DefaultMin,
                [DashboardMetricGaugeVisualizationOptions.MaxKey] = DashboardMetricGaugeVisualizationOptions.DefaultMax,
                [DashboardMetricGaugeVisualizationOptions.TargetKey] = DashboardMetricGaugeVisualizationOptions.DefaultTarget,
                [DashboardMetricGaugeVisualizationOptions.WarningKey] = DashboardMetricGaugeVisualizationOptions.DefaultWarning,
                [DashboardMetricGaugeVisualizationOptions.CriticalKey] = DashboardMetricGaugeVisualizationOptions.DefaultCritical,
                [DashboardMetricGaugeVisualizationOptions.NormalColorKey] = DashboardMetricGaugeVisualizationOptions.DefaultNormalColor,
                [DashboardMetricGaugeVisualizationOptions.WarningColorKey] = DashboardMetricGaugeVisualizationOptions.DefaultWarningColor,
                [DashboardMetricGaugeVisualizationOptions.CriticalColorKey] = DashboardMetricGaugeVisualizationOptions.DefaultCriticalColor
            },
            [new("gauge-visual", "Gauge visual", Properties())],
            typeof(DashboardMetricGaugeVisualizationView),
            typeof(DashboardMetricGaugeVisualizationView),
            "Shows one metric result as a threshold gauge.");

    private static IReadOnlyList<DashboardWidgetPropertyDefinition> Properties()
        =>
        [
            new(DashboardMetricGaugeVisualizationOptions.LabelKey, "Label", DashboardWidgetPropertyEditorKind.Text, DefaultValue: DashboardMetricGaugeVisualizationOptions.DefaultLabel),
            new(DashboardMetricGaugeVisualizationOptions.ShowLabelKey, "Show label", DashboardWidgetPropertyEditorKind.Toggle, DefaultValue: "true"),
            new(DashboardMetricGaugeVisualizationOptions.ShapeKey, "Shape", DashboardWidgetPropertyEditorKind.Select, DefaultValue: DashboardMetricGaugeVisualizationOptions.ShapeRing, Options: ShapeOptions()),
            new(DashboardMetricGaugeVisualizationOptions.MinKey, "Min", DashboardWidgetPropertyEditorKind.Number, DefaultValue: DashboardMetricGaugeVisualizationOptions.DefaultMin),
            new(DashboardMetricGaugeVisualizationOptions.MaxKey, "Max", DashboardWidgetPropertyEditorKind.Number, DefaultValue: DashboardMetricGaugeVisualizationOptions.DefaultMax),
            new(DashboardMetricGaugeVisualizationOptions.TargetKey, "Target", DashboardWidgetPropertyEditorKind.Number, DefaultValue: DashboardMetricGaugeVisualizationOptions.DefaultTarget),
            new(DashboardMetricGaugeVisualizationOptions.WarningKey, "Warning", DashboardWidgetPropertyEditorKind.Number, DefaultValue: DashboardMetricGaugeVisualizationOptions.DefaultWarning),
            new(DashboardMetricGaugeVisualizationOptions.CriticalKey, "Critical", DashboardWidgetPropertyEditorKind.Number, DefaultValue: DashboardMetricGaugeVisualizationOptions.DefaultCritical),
            new(DashboardMetricGaugeVisualizationOptions.NormalColorKey, "Normal color", DashboardWidgetPropertyEditorKind.Color, DefaultValue: DashboardMetricGaugeVisualizationOptions.DefaultNormalColor),
            new(DashboardMetricGaugeVisualizationOptions.WarningColorKey, "Warning color", DashboardWidgetPropertyEditorKind.Color, DefaultValue: DashboardMetricGaugeVisualizationOptions.DefaultWarningColor),
            new(DashboardMetricGaugeVisualizationOptions.CriticalColorKey, "Critical color", DashboardWidgetPropertyEditorKind.Color, DefaultValue: DashboardMetricGaugeVisualizationOptions.DefaultCriticalColor)
        ];

    private static IReadOnlyList<DashboardWidgetPropertyOption> ShapeOptions()
        =>
        [
            new(DashboardMetricGaugeVisualizationOptions.ShapeRing, "Ring"),
            new(DashboardMetricGaugeVisualizationOptions.ShapeMeter, "Meter")
        ];
}
