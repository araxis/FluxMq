using FluxMq.UI.Components.Workspace;
using FluxMq.UI.Models;
using MudBlazor;
using System.Globalization;

namespace FluxMq.UI.Services;

public sealed class DashboardChartWidgetModuleProvider : IDashboardWidgetModuleProvider
{
    public string Id => "charts";

    public IReadOnlyList<DashboardWidgetModule> CreateModules()
        =>
        [
            LineChartModule(
                DashboardWidgetCatalog.LineChartType,
                "Line Chart",
                "Charts",
                "Shows one time-series metric as a line.",
                Icons.Material.Filled.StackedLineChart,
                typeof(DashboardLineChartModuleView),
                "Line chart",
                "lineChart",
                [
                    LineChartGroup()
                ],
                [DashboardWidgetCatalog.EventChartType]),
            AreaChartModule(
                DashboardWidgetCatalog.AreaChartType,
                "Area Chart",
                "Charts",
                "Shows one time-series metric as a filled area.",
                Icons.Material.Filled.AreaChart,
                typeof(DashboardAreaChartModuleView),
                "Area chart",
                "areaChart",
                [
                    AreaChartGroup()
                ]),
            BarChartModule(
                DashboardWidgetCatalog.BarChartType,
                "Bar Chart",
                "Charts",
                "Shows one bucketed metric as bars.",
                Icons.Material.Filled.BarChart,
                typeof(DashboardBarChartModuleView),
                "Bar chart",
                "barChart",
                [
                    BarChartGroup()
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
                    DonutChartGroup()
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
        configuration[DashboardChartWidgetOptions.TypeKey] = chartType;
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

    private static DashboardWidgetModule LineChartModule(
        string type,
        string displayName,
        string category,
        string description,
        string icon,
        Type component,
        string title,
        string instanceNamePrefix,
        IReadOnlyList<DashboardWidgetPropertyGroupDefinition> groups,
        IReadOnlyList<string>? compatibilityTypeIds = null)
    {
        var configuration = EventConfiguration(title);
        configuration.Remove(DashboardWidgetCatalog.PrimaryMetricKey);
        configuration[DashboardChartWidgetOptions.TypeKey] = DashboardChartWidgetOptions.TypeLine;
        configuration[DashboardLineChartVisualOptions.HeaderKey] = title;
        configuration[DashboardLineChartVisualOptions.ShowHeaderKey] = bool.TrueString;
        configuration[DashboardLineChartVisualOptions.ShowGridKey] = bool.TrueString;
        configuration[DashboardLineChartVisualOptions.ShowLabelsKey] = bool.TrueString;
        configuration[DashboardLineChartVisualOptions.ShowPointsKey] = bool.FalseString;
        configuration[DashboardLineChartVisualOptions.EmptyTextKey] = DashboardLineChartVisualOptions.DefaultEmptyText;
        configuration[DashboardLineChartVisualOptions.LineColorKey] = DashboardLineChartVisualOptions.DefaultLineColor;
        configuration[DashboardLineChartVisualOptions.GridColorKey] = DashboardLineChartVisualOptions.DefaultGridColor;
        configuration[DashboardLineChartVisualOptions.LabelColorKey] = DashboardLineChartVisualOptions.DefaultLabelColor;
        configuration[DashboardLineChartVisualOptions.LineWidthKey] =
            DashboardLineChartVisualOptions.DefaultLineWidth.ToString(CultureInfo.InvariantCulture);
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

    private static DashboardWidgetModule AreaChartModule(
        string type,
        string displayName,
        string category,
        string description,
        string icon,
        Type component,
        string title,
        string instanceNamePrefix,
        IReadOnlyList<DashboardWidgetPropertyGroupDefinition> groups)
    {
        var configuration = EventConfiguration(title);
        configuration.Remove(DashboardWidgetCatalog.PrimaryMetricKey);
        configuration[DashboardChartWidgetOptions.TypeKey] = DashboardChartWidgetOptions.TypeArea;
        configuration[DashboardAreaChartVisualOptions.HeaderKey] = title;
        configuration[DashboardAreaChartVisualOptions.ShowHeaderKey] = bool.TrueString;
        configuration[DashboardAreaChartVisualOptions.ShowGridKey] = bool.TrueString;
        configuration[DashboardAreaChartVisualOptions.ShowLabelsKey] = bool.TrueString;
        configuration[DashboardAreaChartVisualOptions.ShowPointsKey] = bool.FalseString;
        configuration[DashboardAreaChartVisualOptions.EmptyTextKey] = DashboardAreaChartVisualOptions.DefaultEmptyText;
        configuration[DashboardAreaChartVisualOptions.LineColorKey] = DashboardAreaChartVisualOptions.DefaultLineColor;
        configuration[DashboardAreaChartVisualOptions.FillColorKey] = DashboardAreaChartVisualOptions.DefaultFillColor;
        configuration[DashboardAreaChartVisualOptions.FillOpacityKey] =
            DashboardAreaChartVisualOptions.DefaultFillOpacity.ToString(CultureInfo.InvariantCulture);
        configuration[DashboardAreaChartVisualOptions.GridColorKey] = DashboardAreaChartVisualOptions.DefaultGridColor;
        configuration[DashboardAreaChartVisualOptions.LabelColorKey] = DashboardAreaChartVisualOptions.DefaultLabelColor;
        configuration[DashboardAreaChartVisualOptions.LineWidthKey] =
            DashboardAreaChartVisualOptions.DefaultLineWidth.ToString(CultureInfo.InvariantCulture);
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
            InstanceNamePrefix: instanceNamePrefix);
    }

    private static DashboardWidgetModule BarChartModule(
        string type,
        string displayName,
        string category,
        string description,
        string icon,
        Type component,
        string title,
        string instanceNamePrefix,
        IReadOnlyList<DashboardWidgetPropertyGroupDefinition> groups)
    {
        var configuration = EventConfiguration(title);
        configuration.Remove(DashboardWidgetCatalog.PrimaryMetricKey);
        configuration[DashboardChartWidgetOptions.TypeKey] = DashboardChartWidgetOptions.TypeBars;
        configuration[DashboardBarChartVisualOptions.HeaderKey] = title;
        configuration[DashboardBarChartVisualOptions.ShowHeaderKey] = bool.TrueString;
        configuration[DashboardBarChartVisualOptions.ShowGridKey] = bool.TrueString;
        configuration[DashboardBarChartVisualOptions.ShowLabelsKey] = bool.TrueString;
        configuration[DashboardBarChartVisualOptions.OrientationKey] = DashboardBarChartVisualOptions.OrientationVertical;
        configuration[DashboardBarChartVisualOptions.EmptyTextKey] = DashboardBarChartVisualOptions.DefaultEmptyText;
        configuration[DashboardBarChartVisualOptions.BarColorKey] = DashboardBarChartVisualOptions.DefaultBarColor;
        configuration[DashboardBarChartVisualOptions.GridColorKey] = DashboardBarChartVisualOptions.DefaultGridColor;
        configuration[DashboardBarChartVisualOptions.LabelColorKey] = DashboardBarChartVisualOptions.DefaultLabelColor;
        configuration[DashboardBarChartVisualOptions.BarRadiusKey] =
            DashboardBarChartVisualOptions.DefaultBarRadius.ToString(CultureInfo.InvariantCulture);
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
    {
        var configuration = EventConfiguration(title);
        configuration.Remove(DashboardWidgetCatalog.PrimaryMetricKey);
        configuration[DashboardChartWidgetOptions.TypeKey] = DashboardChartWidgetOptions.TypeTopics;
        configuration[DashboardDonutChartVisualOptions.HeaderKey] = title;
        configuration[DashboardDonutChartVisualOptions.ShowHeaderKey] = bool.TrueString;
        configuration[DashboardDonutChartVisualOptions.ShowLegendKey] = bool.TrueString;
        configuration[DashboardDonutChartVisualOptions.ShowTotalKey] = bool.TrueString;
        configuration[DashboardDonutChartVisualOptions.LimitKey] =
            DashboardDonutChartVisualOptions.DefaultLimit.ToString(CultureInfo.InvariantCulture);
        configuration[DashboardDonutChartVisualOptions.InnerRadiusKey] =
            DashboardDonutChartVisualOptions.DefaultInnerRadius.ToString(CultureInfo.InvariantCulture);
        configuration[DashboardDonutChartVisualOptions.EmptyTextKey] = DashboardDonutChartVisualOptions.DefaultEmptyText;
        configuration[DashboardDonutChartVisualOptions.SegmentColor1Key] = DashboardDonutChartVisualOptions.DefaultSegmentColor1;
        configuration[DashboardDonutChartVisualOptions.SegmentColor2Key] = DashboardDonutChartVisualOptions.DefaultSegmentColor2;
        configuration[DashboardDonutChartVisualOptions.SegmentColor3Key] = DashboardDonutChartVisualOptions.DefaultSegmentColor3;
        configuration[DashboardDonutChartVisualOptions.SegmentColor4Key] = DashboardDonutChartVisualOptions.DefaultSegmentColor4;
        configuration[DashboardDonutChartVisualOptions.SegmentColor5Key] = DashboardDonutChartVisualOptions.DefaultSegmentColor5;
        configuration[DashboardDonutChartVisualOptions.LabelColorKey] = DashboardDonutChartVisualOptions.DefaultLabelColor;
        configuration[DashboardDonutChartVisualOptions.MutedColorKey] = DashboardDonutChartVisualOptions.DefaultMutedColor;
        return new DashboardWidgetModule(
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
            configuration,
            groups,
            new DashboardWidgetStyleDefinition(),
            new DashboardWidgetLayoutContract(2, 1, 2, 1),
            InstanceNamePrefix: instanceNamePrefix);
    }

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

    private static DashboardWidgetPropertyGroupDefinition AxisGroup()
        => new("axis", "Axis", [
            new("showGrid", "Grid", DashboardWidgetPropertyEditorKind.Toggle),
            new("showLabels", "Labels", DashboardWidgetPropertyEditorKind.Toggle)
        ]);

    private static DashboardWidgetPropertyGroupDefinition LineChartGroup()
        => new("line-chart", "Line chart", [
            new(DashboardLineChartVisualOptions.HeaderKey, "Header", DashboardWidgetPropertyEditorKind.Text),
            new(DashboardLineChartVisualOptions.ShowHeaderKey, "Show header", DashboardWidgetPropertyEditorKind.Toggle),
            new(DashboardLineChartVisualOptions.ShowGridKey, "Grid", DashboardWidgetPropertyEditorKind.Toggle),
            new(DashboardLineChartVisualOptions.ShowLabelsKey, "Labels", DashboardWidgetPropertyEditorKind.Toggle),
            new(DashboardLineChartVisualOptions.ShowPointsKey, "Points", DashboardWidgetPropertyEditorKind.Toggle),
            new(DashboardLineChartVisualOptions.LineWidthKey, "Line width", DashboardWidgetPropertyEditorKind.Number, Unit: "px"),
            new(DashboardLineChartVisualOptions.EmptyTextKey, "Empty text", DashboardWidgetPropertyEditorKind.Text),
            new(DashboardLineChartVisualOptions.LineColorKey, "Line color", DashboardWidgetPropertyEditorKind.Color),
            new(DashboardLineChartVisualOptions.GridColorKey, "Grid color", DashboardWidgetPropertyEditorKind.Color),
            new(DashboardLineChartVisualOptions.LabelColorKey, "Label color", DashboardWidgetPropertyEditorKind.Color)
        ]);

    private static DashboardWidgetPropertyGroupDefinition AreaChartGroup()
        => new("area-chart", "Area chart", [
            new(DashboardAreaChartVisualOptions.HeaderKey, "Header", DashboardWidgetPropertyEditorKind.Text),
            new(DashboardAreaChartVisualOptions.ShowHeaderKey, "Show header", DashboardWidgetPropertyEditorKind.Toggle),
            new(DashboardAreaChartVisualOptions.ShowGridKey, "Grid", DashboardWidgetPropertyEditorKind.Toggle),
            new(DashboardAreaChartVisualOptions.ShowLabelsKey, "Labels", DashboardWidgetPropertyEditorKind.Toggle),
            new(DashboardAreaChartVisualOptions.ShowPointsKey, "Points", DashboardWidgetPropertyEditorKind.Toggle),
            new(DashboardAreaChartVisualOptions.LineWidthKey, "Line width", DashboardWidgetPropertyEditorKind.Number, Unit: "px"),
            new(DashboardAreaChartVisualOptions.FillOpacityKey, "Fill opacity", DashboardWidgetPropertyEditorKind.Number, Unit: "%"),
            new(DashboardAreaChartVisualOptions.EmptyTextKey, "Empty text", DashboardWidgetPropertyEditorKind.Text),
            new(DashboardAreaChartVisualOptions.LineColorKey, "Line color", DashboardWidgetPropertyEditorKind.Color),
            new(DashboardAreaChartVisualOptions.FillColorKey, "Fill color", DashboardWidgetPropertyEditorKind.Color),
            new(DashboardAreaChartVisualOptions.GridColorKey, "Grid color", DashboardWidgetPropertyEditorKind.Color),
            new(DashboardAreaChartVisualOptions.LabelColorKey, "Label color", DashboardWidgetPropertyEditorKind.Color)
        ]);

    private static DashboardWidgetPropertyGroupDefinition BarChartGroup()
        => new("bar-chart", "Bar chart", [
            new(DashboardBarChartVisualOptions.HeaderKey, "Header", DashboardWidgetPropertyEditorKind.Text),
            new(DashboardBarChartVisualOptions.ShowHeaderKey, "Show header", DashboardWidgetPropertyEditorKind.Toggle),
            new(DashboardBarChartVisualOptions.ShowGridKey, "Grid", DashboardWidgetPropertyEditorKind.Toggle),
            new(DashboardBarChartVisualOptions.ShowLabelsKey, "Labels", DashboardWidgetPropertyEditorKind.Toggle),
            new(DashboardBarChartVisualOptions.OrientationKey, "Orientation", DashboardWidgetPropertyEditorKind.Select, Options: [
                new(DashboardBarChartVisualOptions.OrientationVertical, "Vertical"),
                new(DashboardBarChartVisualOptions.OrientationHorizontal, "Horizontal")
            ]),
            new(DashboardBarChartVisualOptions.BarRadiusKey, "Bar radius", DashboardWidgetPropertyEditorKind.Number, Unit: "px"),
            new(DashboardBarChartVisualOptions.EmptyTextKey, "Empty text", DashboardWidgetPropertyEditorKind.Text),
            new(DashboardBarChartVisualOptions.BarColorKey, "Bar color", DashboardWidgetPropertyEditorKind.Color),
            new(DashboardBarChartVisualOptions.GridColorKey, "Grid color", DashboardWidgetPropertyEditorKind.Color),
            new(DashboardBarChartVisualOptions.LabelColorKey, "Label color", DashboardWidgetPropertyEditorKind.Color)
        ]);

    private static DashboardWidgetPropertyGroupDefinition DonutChartGroup()
        => new("donut-chart", "Donut chart", [
            new(DashboardDonutChartVisualOptions.HeaderKey, "Header", DashboardWidgetPropertyEditorKind.Text),
            new(DashboardDonutChartVisualOptions.ShowHeaderKey, "Show header", DashboardWidgetPropertyEditorKind.Toggle),
            new(DashboardDonutChartVisualOptions.ShowLegendKey, "Legend", DashboardWidgetPropertyEditorKind.Toggle),
            new(DashboardDonutChartVisualOptions.ShowTotalKey, "Center total", DashboardWidgetPropertyEditorKind.Toggle),
            new(DashboardDonutChartVisualOptions.LimitKey, "Categories", DashboardWidgetPropertyEditorKind.Number),
            new(DashboardDonutChartVisualOptions.InnerRadiusKey, "Hole", DashboardWidgetPropertyEditorKind.Number, Unit: "%"),
            new(DashboardDonutChartVisualOptions.EmptyTextKey, "Empty text", DashboardWidgetPropertyEditorKind.Text),
            new(DashboardDonutChartVisualOptions.SegmentColor1Key, "Segment 1", DashboardWidgetPropertyEditorKind.Color),
            new(DashboardDonutChartVisualOptions.SegmentColor2Key, "Segment 2", DashboardWidgetPropertyEditorKind.Color),
            new(DashboardDonutChartVisualOptions.SegmentColor3Key, "Segment 3", DashboardWidgetPropertyEditorKind.Color),
            new(DashboardDonutChartVisualOptions.SegmentColor4Key, "Segment 4", DashboardWidgetPropertyEditorKind.Color),
            new(DashboardDonutChartVisualOptions.SegmentColor5Key, "Segment 5", DashboardWidgetPropertyEditorKind.Color),
            new(DashboardDonutChartVisualOptions.LabelColorKey, "Label color", DashboardWidgetPropertyEditorKind.Color),
            new(DashboardDonutChartVisualOptions.MutedColorKey, "Muted color", DashboardWidgetPropertyEditorKind.Color)
        ]);

    private static IReadOnlyList<DashboardWidgetPropertyOption> MetricOptions()
        => [.. DashboardWidgetCatalog.MetricOptions.Select(static metric => new DashboardWidgetPropertyOption(metric.Id, metric.Label))];
}
