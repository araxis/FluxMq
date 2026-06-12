using FluxMq.UI.Components.Workspace;
using FluxMq.UI.Models;
using MudBlazor;

namespace FluxMq.UI.Services;

public sealed class DashboardMetricWidgetModuleProvider : IDashboardWidgetModuleProvider
{
    public string Id => "metrics";

    public IReadOnlyList<DashboardWidgetModule> CreateModules()
        =>
        [
            MetricModule(
                DashboardWidgetCatalog.KpiTileType,
                "KPI Tile",
                "Metrics",
                "Shows one named operational metric with compact comparison context.",
                Icons.Material.Filled.Speed,
                typeof(DashboardKpiTileModuleView),
                "Messages",
                "messages",
                [
                    KpiMetricGroup(),
                    VisualizationGroup(),
                    FormatGroup(),
                    ThresholdGroup()
                ]),
            MetricModule(
                DashboardWidgetCatalog.StatusValueType,
                "Status Value",
                "Metrics",
                "Shows one operational status or selected metric value.",
                Icons.Material.Filled.Verified,
                typeof(DashboardStatusValueModuleView),
                "Status value",
                "recent",
                [
                    MetricGroup("status-source", "Status source"),
                    FormatGroup()
                ],
                [DashboardWidgetCatalog.StatusStripType]),
            MetricModule(
                DashboardWidgetCatalog.RateTileType,
                "Rate Tile",
                "Metrics",
                "Shows one named rate metric.",
                Icons.Material.Filled.QueryStats,
                typeof(DashboardRateTileModuleView),
                "Rate tile",
                "currentRate",
                [
                    MetricGroup("metric", "Metric"),
                    WindowGroup(),
                    FormatGroup(),
                    ThresholdGroup()
                ])
        ];

    private static DashboardWidgetModule MetricModule(
        string type,
        string displayName,
        string category,
        string description,
        string icon,
        Type component,
        string title,
        string primaryMetric,
        IReadOnlyList<DashboardWidgetPropertyGroupDefinition> groups,
        IReadOnlyList<string>? compatibilityTypeIds = null)
    {
        var configuration = BaseMetricConfiguration(title, primaryMetric);
        if (string.Equals(type, DashboardWidgetCatalog.KpiTileType, StringComparison.Ordinal))
        {
            configuration.Remove(DashboardWidgetCatalog.PrimaryMetricKey);
            configuration["subtitle"] = "Total matching events";
            foreach (var (key, value) in DashboardMetricVisualizationCatalog.Find(DashboardMetricVisualizationIds.Value)!.DefaultConfiguration)
            {
                configuration[key] = value;
            }
        }
        else if (string.Equals(type, DashboardWidgetCatalog.RateTileType, StringComparison.Ordinal) ||
                 string.Equals(type, DashboardWidgetCatalog.StatusValueType, StringComparison.Ordinal))
        {
            configuration.Remove(DashboardWidgetCatalog.PrimaryMetricKey);
        }

        return new DashboardWidgetModule(
            new DashboardWidgetDescriptor(
                type,
                displayName,
                category,
                description,
                icon,
                displayName,
                DashboardWidgetRendererKind.Kpi,
                DashboardWidgetEditorKind.MetricTile,
                ["runtimeEvents"]),
            component,
            component,
            configuration,
            groups,
            new DashboardWidgetStyleDefinition(),
            new DashboardWidgetLayoutContract(),
            compatibilityTypeIds);
    }

    private static Dictionary<string, string> BaseMetricConfiguration(string title, string primaryMetric)
        => new(StringComparer.Ordinal)
        {
            ["title"] = title,
            [DashboardWidgetCatalog.PrimaryMetricKey] = primaryMetric
        };

    private static DashboardWidgetPropertyGroupDefinition MetricGroup(string id, string title)
        => new(id, title, [
            new("metric", "Metric", DashboardWidgetPropertyEditorKind.Metric, HelpText: "Named metric used by this widget."),
            new(DashboardWidgetCatalog.PrimaryMetricKey, "Value", DashboardWidgetPropertyEditorKind.Select, Options: MetricOptions())
        ]);

    private static DashboardWidgetPropertyGroupDefinition KpiMetricGroup()
        => new("data", "Data", [
            new("metric", "Metric", DashboardWidgetPropertyEditorKind.Metric, HelpText: "Named scalar metric shown by this KPI.")
        ]);

    private static DashboardWidgetPropertyGroupDefinition VisualizationGroup()
        => new("visualization", "Visualization", [
            new(
                DashboardWidgetCatalog.MetricVisualizationKey,
                "Visualization",
                DashboardWidgetPropertyEditorKind.Select,
                HelpText: "Visual representation used for the metric value.",
                DefaultValue: DashboardMetricVisualizationIds.Value,
                Options: MetricVisualizationOptions())
        ]);

    private static DashboardWidgetPropertyGroupDefinition WindowGroup()
        => new("window", "Window", [
            new("window", "Window", DashboardWidgetPropertyEditorKind.Select, Options: [
                new("30s", "30 sec"),
                new("60s", "1 min"),
                new("300s", "5 min"),
                new("900s", "15 min")
            ])
        ]);

    private static DashboardWidgetPropertyGroupDefinition FormatGroup(string id = "format", string title = "Format")
        => new(id, title, [
            new("title", "Title", DashboardWidgetPropertyEditorKind.Text),
            new("unit", "Unit", DashboardWidgetPropertyEditorKind.Text),
            new("precision", "Decimals", DashboardWidgetPropertyEditorKind.Number)
        ]);

    private static DashboardWidgetPropertyGroupDefinition ThresholdGroup()
        => new("threshold", "Threshold", [
            new("warning", "Warning", DashboardWidgetPropertyEditorKind.Number),
            new("critical", "Critical", DashboardWidgetPropertyEditorKind.Number)
        ], StartCollapsed: true);

    private static IReadOnlyList<DashboardWidgetPropertyOption> MetricOptions()
        => [.. DashboardWidgetCatalog.MetricOptions.Select(static metric => new DashboardWidgetPropertyOption(metric.Id, metric.Label))];

    private static IReadOnlyList<DashboardWidgetPropertyOption> MetricVisualizationOptions()
        => [.. DashboardMetricVisualizationCatalog.CreateModules().Select(static visual => new DashboardWidgetPropertyOption(visual.Id, visual.DisplayName))];
}
