using FluxMq.UI.Components.Workspace;
using FluxMq.UI.Models;
using MudBlazor;
using System.Globalization;

namespace FluxMq.UI.Services;

public sealed class DashboardTopicWidgetModuleProvider : IDashboardWidgetModuleProvider
{
    public string Id => "topics";

    public IReadOnlyList<DashboardWidgetModule> CreateModules()
        =>
        [
            TopicActivityModule(
                DashboardWidgetCatalog.TopicActivityType,
                "Topic Activity",
                "Topics",
                "Shows top topic activity.",
                Icons.Material.Filled.GridOn,
                typeof(DashboardTopicActivityModuleView),
                "Topic activity",
                "topicActivity",
                [
                    TopicActivityGroup()
                ],
                dataRequirements: ["topicProjection"],
                preferredColumns: 2,
                preferredRows: 2),
            new(
                new DashboardWidgetDescriptor(
                    DashboardWidgetCatalog.TopicTreeType,
                    "Topic Tree",
                    "Topics",
                    "Shows live MQTT topics as a dashboard tree.",
                    Icons.Material.Filled.AccountTree,
                    "Topic hierarchy",
                    DashboardWidgetRendererKind.TopicTree,
                    DashboardWidgetEditorKind.TopicTree,
                    ["topicProjection"]),
                typeof(DashboardTopicTreeModuleView),
                typeof(DashboardTopicTreeModuleView),
                TopicTreeConfiguration("Topic tree"),
                [TopicTreeGroup()],
                new DashboardWidgetStyleDefinition(),
                new DashboardWidgetLayoutContract(1, 2, 2, 2),
                InstanceNamePrefix: "topicTree")
        ];

    private static DashboardWidgetModule TopicActivityModule(
        string type,
        string displayName,
        string category,
        string description,
        string icon,
        Type component,
        string title,
        string instanceNamePrefix,
        IReadOnlyList<DashboardWidgetPropertyGroupDefinition> groups,
        IReadOnlyList<string> dataRequirements,
        int preferredColumns,
        int preferredRows)
        => new(
            new DashboardWidgetDescriptor(
                type,
                displayName,
                category,
                description,
                icon,
                displayName,
                DashboardWidgetRendererKind.TopicActivity,
                DashboardWidgetEditorKind.Basic,
                dataRequirements),
            component,
            component,
            TopicActivityConfiguration(title),
            groups,
            new DashboardWidgetStyleDefinition(),
            new DashboardWidgetLayoutContract(1, 1, preferredColumns, preferredRows),
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

    private static Dictionary<string, string> TopicActivityConfiguration(string header)
    {
        var configuration = EventConfiguration(header);
        configuration[DashboardTopicActivityVisualOptions.HeaderKey] = header;
        configuration[DashboardTopicActivityVisualOptions.ShowHeaderKey] = "true";
        configuration[DashboardTopicActivityVisualOptions.LimitKey] =
            DashboardTopicActivityVisualOptions.DefaultLimit.ToString(CultureInfo.InvariantCulture);
        configuration[DashboardTopicActivityVisualOptions.ShowCountsKey] = "true";
        configuration[DashboardTopicActivityVisualOptions.EmptyTextKey] = DashboardTopicActivityVisualOptions.DefaultEmptyText;
        configuration[DashboardTopicActivityVisualOptions.HeaderColorKey] = DashboardTopicActivityVisualOptions.DefaultHeaderColor;
        configuration[DashboardTopicActivityVisualOptions.TextColorKey] = DashboardTopicActivityVisualOptions.DefaultTextColor;
        configuration[DashboardTopicActivityVisualOptions.MutedColorKey] = DashboardTopicActivityVisualOptions.DefaultMutedColor;
        configuration[DashboardTopicActivityVisualOptions.AccentColorKey] = DashboardTopicActivityVisualOptions.DefaultAccentColor;
        return configuration;
    }

    private static Dictionary<string, string> TopicTreeConfiguration(string header)
        => new(StringComparer.Ordinal)
        {
            ["title"] = header,
            [DashboardTopicTreeVisualOptions.HeaderKey] = header,
            [DashboardTopicTreeVisualOptions.ShowHeaderKey] = "true",
            [DashboardTopicTreeVisualOptions.ShowSummaryKey] = "true",
            [DashboardTopicTreeVisualOptions.ShowTopicCountKey] = "true",
            [DashboardTopicTreeVisualOptions.ShowMessageCountKey] = "true",
            [DashboardTopicTreeVisualOptions.ExcludeSystemTopicsKey] = "true",
            [DashboardTopicTreeVisualOptions.EmptyTextKey] = DashboardTopicTreeVisualOptions.DefaultEmptyText,
            [DashboardTopicTreeVisualOptions.HeaderColorKey] = DashboardTopicTreeVisualOptions.DefaultHeaderColor,
            [DashboardTopicTreeVisualOptions.TextColorKey] = DashboardTopicTreeVisualOptions.DefaultTextColor,
            [DashboardTopicTreeVisualOptions.MutedColorKey] = DashboardTopicTreeVisualOptions.DefaultMutedColor,
            [DashboardTopicTreeVisualOptions.AccentColorKey] = DashboardTopicTreeVisualOptions.DefaultAccentColor
        };

    private static DashboardWidgetPropertyGroupDefinition TopicActivityGroup()
        => new("topic-activity", "Topic activity", [
            new(DashboardTopicActivityVisualOptions.HeaderKey, "Header", DashboardWidgetPropertyEditorKind.Text),
            new(DashboardTopicActivityVisualOptions.ShowHeaderKey, "Show header", DashboardWidgetPropertyEditorKind.Toggle),
            new(DashboardTopicActivityVisualOptions.LimitKey, "Topics", DashboardWidgetPropertyEditorKind.Number),
            new(DashboardTopicActivityVisualOptions.ShowCountsKey, "Counts", DashboardWidgetPropertyEditorKind.Toggle),
            new(DashboardTopicActivityVisualOptions.EmptyTextKey, "Empty text", DashboardWidgetPropertyEditorKind.Text),
            new(DashboardTopicActivityVisualOptions.HeaderColorKey, "Header color", DashboardWidgetPropertyEditorKind.Color),
            new(DashboardTopicActivityVisualOptions.TextColorKey, "Text color", DashboardWidgetPropertyEditorKind.Color),
            new(DashboardTopicActivityVisualOptions.MutedColorKey, "Muted color", DashboardWidgetPropertyEditorKind.Color),
            new(DashboardTopicActivityVisualOptions.AccentColorKey, "Accent color", DashboardWidgetPropertyEditorKind.Color)
        ]);

    private static DashboardWidgetPropertyGroupDefinition TopicTreeGroup()
        => new("topic-tree", "Topic tree", [
            new(DashboardTopicTreeVisualOptions.HeaderKey, "Header", DashboardWidgetPropertyEditorKind.Text),
            new(DashboardTopicTreeVisualOptions.ShowHeaderKey, "Show header", DashboardWidgetPropertyEditorKind.Toggle),
            new(DashboardTopicTreeVisualOptions.ShowSummaryKey, "Summary", DashboardWidgetPropertyEditorKind.Toggle),
            new(DashboardTopicTreeVisualOptions.ShowTopicCountKey, "Topic count", DashboardWidgetPropertyEditorKind.Toggle),
            new(DashboardTopicTreeVisualOptions.ShowMessageCountKey, "Message count", DashboardWidgetPropertyEditorKind.Toggle),
            new(DashboardTopicTreeVisualOptions.ExcludeSystemTopicsKey, "System topics", DashboardWidgetPropertyEditorKind.Toggle),
            new(DashboardTopicTreeVisualOptions.EmptyTextKey, "Empty text", DashboardWidgetPropertyEditorKind.Text),
            new(DashboardTopicTreeVisualOptions.HeaderColorKey, "Header color", DashboardWidgetPropertyEditorKind.Color),
            new(DashboardTopicTreeVisualOptions.TextColorKey, "Text color", DashboardWidgetPropertyEditorKind.Color),
            new(DashboardTopicTreeVisualOptions.MutedColorKey, "Muted color", DashboardWidgetPropertyEditorKind.Color),
            new(DashboardTopicTreeVisualOptions.AccentColorKey, "Accent color", DashboardWidgetPropertyEditorKind.Color)
        ]);
}
