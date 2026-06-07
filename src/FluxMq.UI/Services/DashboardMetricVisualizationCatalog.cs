using FluxMq.UI.Components.Workspace;
using FluxMq.UI.Models;
using MudBlazor;

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
                [
                    new(
                        "value-visual",
                        "Value visual",
                        [
                            new(
                                DashboardWidgetCatalog.KpiTitleColorKey,
                                "Title color",
                                DashboardWidgetPropertyEditorKind.Color,
                                DefaultValue: DashboardWidgetCatalog.KpiDefaultTitleColor),
                            new(
                                DashboardWidgetCatalog.KpiSubtitleColorKey,
                                "Subtitle color",
                                DashboardWidgetPropertyEditorKind.Color,
                                DefaultValue: DashboardWidgetCatalog.KpiDefaultSubtitleColor),
                            new(
                                DashboardWidgetCatalog.KpiValueColorKey,
                                "Value color",
                                DashboardWidgetPropertyEditorKind.Color,
                                DefaultValue: DashboardWidgetCatalog.KpiDefaultValueColor),
                            new(
                                DashboardWidgetCatalog.KpiTitleAlignKey,
                                "Title align",
                                DashboardWidgetPropertyEditorKind.Segmented,
                                DefaultValue: DashboardWidgetCatalog.KpiAlignLeft),
                            new(
                                DashboardWidgetCatalog.KpiValueAlignKey,
                                "Value align",
                                DashboardWidgetPropertyEditorKind.Segmented,
                                DefaultValue: DashboardWidgetCatalog.KpiAlignLeft),
                            new(
                                DashboardWidgetCatalog.KpiValuePlacementKey,
                                "Value place",
                                DashboardWidgetPropertyEditorKind.Segmented,
                                DefaultValue: DashboardWidgetCatalog.KpiValuePlacementTop)
                        ])
                ],
                typeof(DashboardMetricValueVisualizationView),
                typeof(DashboardMetricValueVisualizationView),
                "Shows one metric result as a title, value, and optional unit.")
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
}
