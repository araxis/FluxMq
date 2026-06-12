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
                [DashboardWidgetCatalog.MetricValueTitleKey] = DashboardWidgetCatalog.MetricValueDefaultTitle,
                [DashboardWidgetCatalog.MetricValueSubtitleKey] = DashboardWidgetCatalog.MetricValueDefaultSubtitle,
                [DashboardWidgetCatalog.MetricValueShowTitleKey] = "true",
                [DashboardWidgetCatalog.MetricValueShowSubtitleKey] = "true",
                [DashboardWidgetCatalog.MetricValueShowUnitKey] = "true",
                [DashboardWidgetCatalog.MetricValueUnitTextKey] = DashboardWidgetCatalog.MetricValueDefaultUnitText,
                [DashboardWidgetCatalog.MetricValueTitleColorKey] = DashboardWidgetCatalog.KpiDefaultTitleColor,
                [DashboardWidgetCatalog.MetricValueSubtitleColorKey] = DashboardWidgetCatalog.KpiDefaultSubtitleColor,
                [DashboardWidgetCatalog.MetricValueValueColorKey] = DashboardWidgetCatalog.KpiDefaultValueColor,
                [DashboardWidgetCatalog.MetricValueUnitColorKey] = DashboardWidgetCatalog.KpiDefaultSubtitleColor,
                [DashboardWidgetCatalog.MetricValueTitleAlignKey] = DashboardWidgetCatalog.KpiAlignLeft,
                [DashboardWidgetCatalog.MetricValueValueAlignKey] = DashboardWidgetCatalog.KpiAlignLeft,
                [DashboardWidgetCatalog.MetricValueValuePlacementKey] = DashboardWidgetCatalog.KpiValuePlacementTop,
                [DashboardWidgetCatalog.MetricValuePaddingKey] = DashboardWidgetCatalog.MetricValueDefaultPadding.ToString(CultureInfo.InvariantCulture)
            },
            [new("value-visual", "Value visual", Properties())],
            typeof(DashboardMetricValueVisualizationView),
            typeof(DashboardMetricValueVisualizationView),
            "Shows one metric result as a title, value, and optional unit.");

    private static IReadOnlyList<DashboardWidgetPropertyDefinition> Properties()
        =>
        [
            new(DashboardWidgetCatalog.MetricValueTitleKey, "Title", DashboardWidgetPropertyEditorKind.Text, DefaultValue: DashboardWidgetCatalog.MetricValueDefaultTitle),
            new(DashboardWidgetCatalog.MetricValueShowTitleKey, "Show title", DashboardWidgetPropertyEditorKind.Toggle, DefaultValue: "true"),
            new(DashboardWidgetCatalog.MetricValueSubtitleKey, "Subtitle", DashboardWidgetPropertyEditorKind.Text, DefaultValue: DashboardWidgetCatalog.MetricValueDefaultSubtitle),
            new(DashboardWidgetCatalog.MetricValueShowSubtitleKey, "Show subtitle", DashboardWidgetPropertyEditorKind.Toggle, DefaultValue: "true"),
            new(DashboardWidgetCatalog.MetricValueShowUnitKey, "Show unit", DashboardWidgetPropertyEditorKind.Toggle, DefaultValue: "true"),
            new(DashboardWidgetCatalog.MetricValueUnitTextKey, "Unit text", DashboardWidgetPropertyEditorKind.Text, DefaultValue: DashboardWidgetCatalog.MetricValueDefaultUnitText, HelpText: "Leave empty to use the metric's natural unit."),
            new(DashboardWidgetCatalog.MetricValueTitleColorKey, "Title color", DashboardWidgetPropertyEditorKind.Color, DefaultValue: DashboardWidgetCatalog.KpiDefaultTitleColor),
            new(DashboardWidgetCatalog.MetricValueSubtitleColorKey, "Subtitle color", DashboardWidgetPropertyEditorKind.Color, DefaultValue: DashboardWidgetCatalog.KpiDefaultSubtitleColor),
            new(DashboardWidgetCatalog.MetricValueValueColorKey, "Value color", DashboardWidgetPropertyEditorKind.Color, DefaultValue: DashboardWidgetCatalog.KpiDefaultValueColor),
            new(DashboardWidgetCatalog.MetricValueUnitColorKey, "Unit color", DashboardWidgetPropertyEditorKind.Color, DefaultValue: DashboardWidgetCatalog.KpiDefaultSubtitleColor),
            new(DashboardWidgetCatalog.MetricValueTitleAlignKey, "Title align", DashboardWidgetPropertyEditorKind.Segmented, DefaultValue: DashboardWidgetCatalog.KpiAlignLeft),
            new(DashboardWidgetCatalog.MetricValueValueAlignKey, "Value align", DashboardWidgetPropertyEditorKind.Segmented, DefaultValue: DashboardWidgetCatalog.KpiAlignLeft),
            new(DashboardWidgetCatalog.MetricValueValuePlacementKey, "Value place", DashboardWidgetPropertyEditorKind.Segmented, DefaultValue: DashboardWidgetCatalog.KpiValuePlacementTop),
            new(DashboardWidgetCatalog.MetricValuePaddingKey, "Padding", DashboardWidgetPropertyEditorKind.Number, Unit: "px", DefaultValue: DashboardWidgetCatalog.MetricValueDefaultPadding.ToString(CultureInfo.InvariantCulture))
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
                [DashboardMetricDigitalVisualizationOptions.FitModeKey] = DashboardMetricDigitalVisualizationOptions.FitCompact
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
            new(DashboardMetricDigitalVisualizationOptions.FitModeKey, "Fit", DashboardWidgetPropertyEditorKind.Select, DefaultValue: DashboardMetricDigitalVisualizationOptions.FitCompact, Options: FitModeOptions())
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
