using FluxMq.UI.Components.Workspace;
using FluxMq.UI.Models;
using MudBlazor;
using System.Globalization;

namespace FluxMq.UI.Services;

public static class DashboardMetricVisualizationCatalog
{
    public static IReadOnlyList<DashboardMetricVisualizationModule> CreateModules()
        =>
        [
            new(
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
                    [DashboardWidgetCatalog.KpiTitleColorKey] = DashboardWidgetCatalog.KpiDefaultTitleColor,
                    [DashboardWidgetCatalog.KpiSubtitleColorKey] = DashboardWidgetCatalog.KpiDefaultSubtitleColor,
                    [DashboardWidgetCatalog.KpiValueColorKey] = DashboardWidgetCatalog.KpiDefaultValueColor,
                    [DashboardWidgetCatalog.KpiTitleAlignKey] = DashboardWidgetCatalog.KpiAlignLeft,
                    [DashboardWidgetCatalog.KpiValueAlignKey] = DashboardWidgetCatalog.KpiAlignLeft,
                    [DashboardWidgetCatalog.KpiValuePlacementKey] = DashboardWidgetCatalog.KpiValuePlacementTop
                },
                [new("value-visual", "Value visual", CommonValueProperties())],
                typeof(DashboardMetricValueVisualizationView),
                typeof(DashboardMetricValueVisualizationView),
                "Shows one metric result as a title, value, and optional unit."),
            new(
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
                    [DashboardWidgetCatalog.KpiTitleColorKey] = DashboardWidgetCatalog.KpiDefaultTitleColor,
                    [DashboardWidgetCatalog.KpiSubtitleColorKey] = DashboardWidgetCatalog.KpiDefaultSubtitleColor,
                    [DashboardWidgetCatalog.KpiValueColorKey] = DashboardWidgetCatalog.KpiDefaultValueColor,
                    [DashboardWidgetCatalog.KpiTitleAlignKey] = DashboardWidgetCatalog.KpiAlignLeft,
                    [DashboardWidgetCatalog.KpiValueAlignKey] = DashboardWidgetCatalog.KpiAlignCenter,
                    [DashboardWidgetCatalog.KpiValuePlacementKey] = DashboardWidgetCatalog.KpiValuePlacementMiddle,
                    [DashboardWidgetCatalog.MetricDigitalStyleKey] = DashboardWidgetCatalog.MetricDigitalStylePanel,
                    [DashboardWidgetCatalog.MetricDigitalGlowKey] = DashboardWidgetCatalog.MetricDigitalGlowSoft,
                    [DashboardWidgetCatalog.MetricDigitalBackgroundColorKey] = DashboardWidgetCatalog.MetricDigitalDefaultBackgroundColor,
                    [DashboardWidgetCatalog.MetricDigitalSegmentColorKey] = DashboardWidgetCatalog.MetricDigitalDefaultSegmentColor,
                    [DashboardWidgetCatalog.MetricDigitalInactiveSegmentColorKey] = DashboardWidgetCatalog.MetricDigitalDefaultInactiveSegmentColor,
                    [DashboardWidgetCatalog.MetricDigitalLabelColorKey] = DashboardWidgetCatalog.MetricDigitalDefaultLabelColor,
                    [DashboardWidgetCatalog.MetricDigitalDigitsKey] = DashboardWidgetCatalog.MetricDigitalDefaultDigits.ToString(CultureInfo.InvariantCulture)
                },
                [
                    new(
                        "digital-visual",
                        "Digital visual",
                        [
                            .. CommonValueProperties(includeValueColor: false),
                            new(
                                DashboardWidgetCatalog.MetricDigitalStyleKey,
                                "Digit style",
                                DashboardWidgetPropertyEditorKind.Select,
                                DefaultValue: DashboardWidgetCatalog.MetricDigitalStylePanel,
                                Options: DigitalStyleOptions()),
                            new(
                                DashboardWidgetCatalog.MetricDigitalGlowKey,
                                "Glow",
                                DashboardWidgetPropertyEditorKind.Select,
                                DefaultValue: DashboardWidgetCatalog.MetricDigitalGlowSoft,
                                Options: DigitalGlowOptions()),
                            new(
                                DashboardWidgetCatalog.MetricDigitalSegmentColorKey,
                                "Segment color",
                                DashboardWidgetPropertyEditorKind.Color,
                                DefaultValue: DashboardWidgetCatalog.MetricDigitalDefaultSegmentColor),
                            new(
                                DashboardWidgetCatalog.MetricDigitalInactiveSegmentColorKey,
                                "Inactive color",
                                DashboardWidgetPropertyEditorKind.Color,
                                DefaultValue: DashboardWidgetCatalog.MetricDigitalDefaultInactiveSegmentColor),
                            new(
                                DashboardWidgetCatalog.MetricDigitalBackgroundColorKey,
                                "Display bg",
                                DashboardWidgetPropertyEditorKind.Color,
                                DefaultValue: DashboardWidgetCatalog.MetricDigitalDefaultBackgroundColor),
                            new(
                                DashboardWidgetCatalog.MetricDigitalLabelColorKey,
                                "Label color",
                                DashboardWidgetPropertyEditorKind.Color,
                                DefaultValue: DashboardWidgetCatalog.MetricDigitalDefaultLabelColor),
                            new(
                                DashboardWidgetCatalog.MetricDigitalDigitsKey,
                                "Digits",
                                DashboardWidgetPropertyEditorKind.Number,
                                DefaultValue: DashboardWidgetCatalog.MetricDigitalDefaultDigits.ToString(CultureInfo.InvariantCulture))
                        ])
                ],
                typeof(DashboardMetricDigitalVisualizationView),
                typeof(DashboardMetricDigitalVisualizationView),
                "Shows one metric result as a compact digital readout.")
        ];

    public static DashboardMetricVisualizationModule? Find(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var normalized = id.Trim();
        return CreateModules().FirstOrDefault(module =>
            string.Equals(module.Id, normalized, StringComparison.Ordinal));
    }

    private static IReadOnlyList<DashboardWidgetPropertyDefinition> CommonValueProperties(bool includeValueColor = true)
    {
        var properties = new List<DashboardWidgetPropertyDefinition>
        {
            new(
                DashboardWidgetCatalog.KpiTitleColorKey,
                "Title color",
                DashboardWidgetPropertyEditorKind.Color,
                DefaultValue: DashboardWidgetCatalog.KpiDefaultTitleColor),
            new(
                DashboardWidgetCatalog.KpiSubtitleColorKey,
                "Subtitle color",
                DashboardWidgetPropertyEditorKind.Color,
                DefaultValue: DashboardWidgetCatalog.KpiDefaultSubtitleColor)
        };
        if (includeValueColor)
        {
            properties.Add(new DashboardWidgetPropertyDefinition(
                DashboardWidgetCatalog.KpiValueColorKey,
                "Value color",
                DashboardWidgetPropertyEditorKind.Color,
                DefaultValue: DashboardWidgetCatalog.KpiDefaultValueColor));
        }

        properties.AddRange([
            new DashboardWidgetPropertyDefinition(
                DashboardWidgetCatalog.KpiTitleAlignKey,
                "Title align",
                DashboardWidgetPropertyEditorKind.Segmented,
                DefaultValue: DashboardWidgetCatalog.KpiAlignLeft),
            new DashboardWidgetPropertyDefinition(
                DashboardWidgetCatalog.KpiValueAlignKey,
                "Value align",
                DashboardWidgetPropertyEditorKind.Segmented,
                DefaultValue: DashboardWidgetCatalog.KpiAlignLeft),
            new DashboardWidgetPropertyDefinition(
                DashboardWidgetCatalog.KpiValuePlacementKey,
                "Value place",
                DashboardWidgetPropertyEditorKind.Segmented,
                DefaultValue: DashboardWidgetCatalog.KpiValuePlacementTop)
        ]);
        return properties;
    }

    private static IReadOnlyList<DashboardWidgetPropertyOption> DigitalStyleOptions()
        =>
        [
            new(DashboardWidgetCatalog.MetricDigitalStylePanel, "Panel"),
            new(DashboardWidgetCatalog.MetricDigitalStyleSegment, "Segment"),
            new(DashboardWidgetCatalog.MetricDigitalStyleTerminal, "Terminal")
        ];

    private static IReadOnlyList<DashboardWidgetPropertyOption> DigitalGlowOptions()
        =>
        [
            new(DashboardWidgetCatalog.MetricDigitalGlowOff, "Off"),
            new(DashboardWidgetCatalog.MetricDigitalGlowSoft, "Soft"),
            new(DashboardWidgetCatalog.MetricDigitalGlowStrong, "Strong")
        ];
}
