using FluxMq.UI.Components.Workspace;
using FluxMq.UI.Models;
using MudBlazor;

namespace FluxMq.UI.Services;

public sealed class DashboardChartWidgetModuleProvider : IDashboardWidgetModuleProvider
{
    public string Id => "charts";

    public IReadOnlyList<DashboardWidgetModule> CreateModules()
        =>
        [
            ChartModule(
                DashboardWidgetCatalog.LineChartType,
                "Line Chart",
                "Charts",
                "Shows one time-series metric as a line.",
                Icons.Material.Filled.StackedLineChart,
                typeof(DashboardLineChartModuleView),
                "Line chart",
                "lineChart",
                DashboardWidgetCatalog.ChartTypeLine,
                [
                    MetricGroup("metric", "Metric"),
                    WindowGroup(),
                    AxisGroup(),
                    ChartLineGroup()
                ],
                [DashboardWidgetCatalog.EventChartType]),
            ChartModule(
                DashboardWidgetCatalog.AreaChartType,
                "Area Chart",
                "Charts",
                "Shows one time-series metric as a filled area.",
                Icons.Material.Filled.AreaChart,
                typeof(DashboardAreaChartModuleView),
                "Area chart",
                "areaChart",
                DashboardWidgetCatalog.ChartTypeArea,
                [
                    MetricGroup("metric", "Metric"),
                    WindowGroup(),
                    AxisGroup(),
                    ChartFillGroup()
                ]),
            ChartModule(
                DashboardWidgetCatalog.BarChartType,
                "Bar Chart",
                "Charts",
                "Shows one bucketed metric as bars.",
                Icons.Material.Filled.BarChart,
                typeof(DashboardBarChartModuleView),
                "Bar chart",
                "barChart",
                DashboardWidgetCatalog.ChartTypeBars,
                [
                    MetricGroup("metric", "Metric"),
                    WindowGroup(),
                    AxisGroup(),
                    BarGroup()
                ]),
            DonutChartModule(
                DashboardWidgetCatalog.DonutChartType,
                "Donut Chart",
                "Charts",
                "Shows one categorical breakdown.",
                Icons.Material.Filled.DonutLarge,
                typeof(DashboardDonutChartModuleView),
                "Donut chart",
                "donutChart",
                [
                    MetricGroup("metric", "Metric"),
                    CategoryGroup()
                ])
        ];

    private static DashboardWidgetModule ChartModule(
        string type,
        string displayName,
        string category,
        string description,
        string icon,
        Type component,
        string title,
        string instanceNamePrefix,
        string chartType,
        IReadOnlyList<DashboardWidgetPropertyGroupDefinition> groups,
        IReadOnlyList<string>? compatibilityTypeIds = null)
    {
        var configuration = EventConfiguration(title);
        configuration[DashboardWidgetCatalog.PrimaryMetricKey] = DashboardWidgetCatalog.MetricMessages;
        configuration[DashboardWidgetCatalog.ChartTypeKey] = chartType;
        return new DashboardWidgetModule(
            new DashboardWidgetDescriptor(
                type,
                displayName,
                category,
                description,
                icon,
                displayName,
                DashboardWidgetRendererKind.Chart,
                DashboardWidgetEditorKind.Chart,
                ["runtimeEvents"]),
            component,
            component,
            configuration,
            groups,
            new DashboardWidgetStyleDefinition(),
            new DashboardWidgetLayoutContract(2, 1, 2, 1),
            compatibilityTypeIds,
            InstanceNamePrefix: instanceNamePrefix);
    }

    private static DashboardWidgetModule DonutChartModule(
        string type,
        string displayName,
        string category,
        string description,
        string icon,
        Type component,
        string title,
        string instanceNamePrefix,
        IReadOnlyList<DashboardWidgetPropertyGroupDefinition> groups)
        => new(
            new DashboardWidgetDescriptor(
                type,
                displayName,
                category,
                description,
                icon,
                displayName,
                DashboardWidgetRendererKind.Donut,
                DashboardWidgetEditorKind.Basic,
                ["runtimeEvents"]),
            component,
            component,
            EventConfiguration(title),
            groups,
            new DashboardWidgetStyleDefinition(),
            new DashboardWidgetLayoutContract(1, 1, 2, 1),
            InstanceNamePrefix: instanceNamePrefix);

    private static Dictionary<string, string> EventConfiguration(string title)
    {
        var configuration = new Dictionary<string, string>(DashboardEventFilterCatalog.Shared.CreateEmptyConfiguration(), StringComparer.Ordinal)
        {
            ["title"] = title,
            [DashboardWidgetCatalog.PrimaryMetricKey] = DashboardWidgetCatalog.MetricMessages
        };
        return configuration;
    }

    private static DashboardWidgetPropertyGroupDefinition MetricGroup(string id, string title)
        => new(id, title, [
            new("metric", "Metric", DashboardWidgetPropertyEditorKind.Metric, HelpText: "Named metric used by this widget."),
            new(DashboardWidgetCatalog.PrimaryMetricKey, "Value", DashboardWidgetPropertyEditorKind.Select, Options: MetricOptions())
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

    private static DashboardWidgetPropertyGroupDefinition AxisGroup()
        => new("axis", "Axis", [
            new("showGrid", "Grid", DashboardWidgetPropertyEditorKind.Toggle),
            new("showLabels", "Labels", DashboardWidgetPropertyEditorKind.Toggle)
        ]);

    private static DashboardWidgetPropertyGroupDefinition ChartLineGroup()
        => new("line", "Line", [
            new("lineColor", "Line", DashboardWidgetPropertyEditorKind.Color),
            new("showPoints", "Points", DashboardWidgetPropertyEditorKind.Toggle)
        ]);

    private static DashboardWidgetPropertyGroupDefinition ChartFillGroup()
        => new("fill", "Fill", [
            new("fillColor", "Fill", DashboardWidgetPropertyEditorKind.Color),
            new("fillOpacity", "Opacity", DashboardWidgetPropertyEditorKind.Number)
        ]);

    private static DashboardWidgetPropertyGroupDefinition BarGroup()
        => new("bars", "Bars", [
            new("barColor", "Bar", DashboardWidgetPropertyEditorKind.Color),
            new("orientation", "Orientation", DashboardWidgetPropertyEditorKind.Select, Options: [
                new("vertical", "Vertical"),
                new("horizontal", "Horizontal")
            ])
        ]);

    private static DashboardWidgetPropertyGroupDefinition CategoryGroup()
        => new("categories", "Categories", [
            new("groupBy", "Group by", DashboardWidgetPropertyEditorKind.Select),
            new("limit", "Limit", DashboardWidgetPropertyEditorKind.Number),
            new("palette", "Palette", DashboardWidgetPropertyEditorKind.Select)
        ]);

    private static IReadOnlyList<DashboardWidgetPropertyOption> MetricOptions()
        => [.. DashboardWidgetCatalog.MetricOptions.Select(static metric => new DashboardWidgetPropertyOption(metric.Id, metric.Label))];
}
