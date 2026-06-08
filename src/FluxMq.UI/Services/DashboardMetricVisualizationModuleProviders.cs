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
                [DashboardWidgetCatalog.MetricDigitalLabelKey] = DashboardWidgetCatalog.MetricValueDefaultTitle,
                [DashboardWidgetCatalog.MetricDigitalShowLabelKey] = "true",
                [DashboardWidgetCatalog.MetricDigitalLabelPlacementKey] = DashboardWidgetCatalog.MetricDigitalLabelPlacementBottom,
                [DashboardWidgetCatalog.MetricDigitalStyleKey] = DashboardWidgetCatalog.MetricDigitalStylePanel,
                [DashboardWidgetCatalog.MetricDigitalGlowKey] = DashboardWidgetCatalog.MetricDigitalGlowSoft,
                [DashboardWidgetCatalog.MetricDigitalBackgroundColorKey] = DashboardWidgetCatalog.MetricDigitalDefaultBackgroundColor,
                [DashboardWidgetCatalog.MetricDigitalSegmentColorKey] = DashboardWidgetCatalog.MetricDigitalDefaultSegmentColor,
                [DashboardWidgetCatalog.MetricDigitalInactiveSegmentColorKey] = DashboardWidgetCatalog.MetricDigitalDefaultInactiveSegmentColor,
                [DashboardWidgetCatalog.MetricDigitalLabelColorKey] = DashboardWidgetCatalog.MetricDigitalDefaultLabelColor,
                [DashboardWidgetCatalog.MetricDigitalDigitsKey] = DashboardWidgetCatalog.MetricDigitalDefaultDigits.ToString(CultureInfo.InvariantCulture),
                [DashboardWidgetCatalog.MetricDigitalBorderColorKey] = DashboardWidgetCatalog.MetricDigitalDefaultBorderColor,
                [DashboardWidgetCatalog.MetricDigitalBorderWidthKey] = DashboardWidgetCatalog.MetricDigitalDefaultBorderWidth.ToString(CultureInfo.InvariantCulture),
                [DashboardWidgetCatalog.MetricDigitalRadiusKey] = DashboardWidgetCatalog.MetricDigitalDefaultRadius.ToString(CultureInfo.InvariantCulture),
                [DashboardWidgetCatalog.MetricDigitalPaddingKey] = DashboardWidgetCatalog.MetricDigitalDefaultPadding.ToString(CultureInfo.InvariantCulture),
                [DashboardWidgetCatalog.MetricDigitalFitModeKey] = DashboardWidgetCatalog.MetricDigitalFitCompact
            },
            [new("digital-visual", "Digital visual", Properties())],
            typeof(DashboardMetricDigitalVisualizationView),
            typeof(DashboardMetricDigitalVisualizationView),
            "Shows one metric result as a compact digital readout.");

    private static IReadOnlyList<DashboardWidgetPropertyDefinition> Properties()
        =>
        [
            new(DashboardWidgetCatalog.MetricDigitalLabelKey, "Label", DashboardWidgetPropertyEditorKind.Text, DefaultValue: DashboardWidgetCatalog.MetricValueDefaultTitle),
            new(DashboardWidgetCatalog.MetricDigitalShowLabelKey, "Show label", DashboardWidgetPropertyEditorKind.Toggle, DefaultValue: "true"),
            new(DashboardWidgetCatalog.MetricDigitalLabelPlacementKey, "Label place", DashboardWidgetPropertyEditorKind.Select, DefaultValue: DashboardWidgetCatalog.MetricDigitalLabelPlacementBottom, Options: LabelPlacementOptions()),
            new(DashboardWidgetCatalog.MetricDigitalStyleKey, "Digit style", DashboardWidgetPropertyEditorKind.Select, DefaultValue: DashboardWidgetCatalog.MetricDigitalStylePanel, Options: StyleOptions()),
            new(DashboardWidgetCatalog.MetricDigitalGlowKey, "Glow", DashboardWidgetPropertyEditorKind.Select, DefaultValue: DashboardWidgetCatalog.MetricDigitalGlowSoft, Options: GlowOptions()),
            new(DashboardWidgetCatalog.MetricDigitalSegmentColorKey, "Segment color", DashboardWidgetPropertyEditorKind.Color, DefaultValue: DashboardWidgetCatalog.MetricDigitalDefaultSegmentColor),
            new(DashboardWidgetCatalog.MetricDigitalInactiveSegmentColorKey, "Inactive color", DashboardWidgetPropertyEditorKind.Color, DefaultValue: DashboardWidgetCatalog.MetricDigitalDefaultInactiveSegmentColor),
            new(DashboardWidgetCatalog.MetricDigitalBackgroundColorKey, "Display bg", DashboardWidgetPropertyEditorKind.Color, DefaultValue: DashboardWidgetCatalog.MetricDigitalDefaultBackgroundColor),
            new(DashboardWidgetCatalog.MetricDigitalLabelColorKey, "Label color", DashboardWidgetPropertyEditorKind.Color, DefaultValue: DashboardWidgetCatalog.MetricDigitalDefaultLabelColor),
            new(DashboardWidgetCatalog.MetricDigitalBorderColorKey, "Border color", DashboardWidgetPropertyEditorKind.Color, DefaultValue: DashboardWidgetCatalog.MetricDigitalDefaultBorderColor),
            new(DashboardWidgetCatalog.MetricDigitalBorderWidthKey, "Border width", DashboardWidgetPropertyEditorKind.Number, Unit: "px", DefaultValue: DashboardWidgetCatalog.MetricDigitalDefaultBorderWidth.ToString(CultureInfo.InvariantCulture)),
            new(DashboardWidgetCatalog.MetricDigitalRadiusKey, "Radius", DashboardWidgetPropertyEditorKind.Number, Unit: "px", DefaultValue: DashboardWidgetCatalog.MetricDigitalDefaultRadius.ToString(CultureInfo.InvariantCulture)),
            new(DashboardWidgetCatalog.MetricDigitalPaddingKey, "Padding", DashboardWidgetPropertyEditorKind.Number, Unit: "px", DefaultValue: DashboardWidgetCatalog.MetricDigitalDefaultPadding.ToString(CultureInfo.InvariantCulture)),
            new(DashboardWidgetCatalog.MetricDigitalDigitsKey, "Digits", DashboardWidgetPropertyEditorKind.Number, DefaultValue: DashboardWidgetCatalog.MetricDigitalDefaultDigits.ToString(CultureInfo.InvariantCulture)),
            new(DashboardWidgetCatalog.MetricDigitalFitModeKey, "Fit", DashboardWidgetPropertyEditorKind.Select, DefaultValue: DashboardWidgetCatalog.MetricDigitalFitCompact, Options: FitModeOptions())
        ];

    private static IReadOnlyList<DashboardWidgetPropertyOption> LabelPlacementOptions()
        =>
        [
            new(DashboardWidgetCatalog.MetricDigitalLabelPlacementTop, "Top"),
            new(DashboardWidgetCatalog.MetricDigitalLabelPlacementBottom, "Bottom"),
            new(DashboardWidgetCatalog.MetricDigitalLabelPlacementHidden, "Hidden")
        ];

    private static IReadOnlyList<DashboardWidgetPropertyOption> StyleOptions()
        =>
        [
            new(DashboardWidgetCatalog.MetricDigitalStylePanel, "Panel"),
            new(DashboardWidgetCatalog.MetricDigitalStyleSegment, "Segment"),
            new(DashboardWidgetCatalog.MetricDigitalStyleTerminal, "Terminal")
        ];

    private static IReadOnlyList<DashboardWidgetPropertyOption> GlowOptions()
        =>
        [
            new(DashboardWidgetCatalog.MetricDigitalGlowOff, "Off"),
            new(DashboardWidgetCatalog.MetricDigitalGlowSoft, "Soft"),
            new(DashboardWidgetCatalog.MetricDigitalGlowStrong, "Strong")
        ];

    private static IReadOnlyList<DashboardWidgetPropertyOption> FitModeOptions()
        =>
        [
            new(DashboardWidgetCatalog.MetricDigitalFitCompact, "Compact"),
            new(DashboardWidgetCatalog.MetricDigitalFitFill, "Fill")
        ];
}
