using FluxMq.UI.Components.Workspace;
using FluxMq.UI.Models;
using MudBlazor;

namespace FluxMq.UI.Services;

public sealed class DashboardMqttOpsWidgetModuleProvider : IDashboardWidgetModuleProvider
{
    public string Id => "mqtt-ops";

    public IReadOnlyList<DashboardWidgetModule> CreateModules()
        =>
        [
            MqttOpsModule(
                DashboardWidgetCatalog.PayloadDistributionType,
                "Payload Size Distribution",
                "MQTT Ops",
                "Shows payload size buckets.",
                Icons.Material.Filled.DataArray,
                typeof(DashboardPayloadDistributionModuleView),
                "Payload sizes",
                [
                    MetricGroup("source", "Source"),
                    BucketGroup()
                ],
                dataRequirements: ["runtimeEvents", "payload"],
                preferredColumns: 2),
            MqttOpsModule(
                DashboardWidgetCatalog.QosBreakdownType,
                "QoS Breakdown",
                "MQTT Ops",
                "Shows QoS distribution.",
                Icons.Material.Filled.PieChart,
                typeof(DashboardQosBreakdownModuleView),
                "QoS breakdown",
                [
                    MetricGroup("source", "Source"),
                    BreakdownGroup()
                ],
                dataRequirements: ["runtimeEvents", "mqttAttributes"],
                preferredColumns: 2,
                compatibilityTypeIds: [DashboardWidgetCatalog.QosRetainBreakdownType]),
            MqttOpsModule(
                DashboardWidgetCatalog.RetainBreakdownType,
                "Retain Breakdown",
                "MQTT Ops",
                "Shows retained-message distribution.",
                Icons.Material.Filled.PushPin,
                typeof(DashboardRetainBreakdownModuleView),
                "Retain breakdown",
                [
                    MetricGroup("source", "Source"),
                    BreakdownGroup()
                ],
                dataRequirements: ["runtimeEvents", "mqttAttributes"],
                preferredColumns: 2)
        ];

    private static DashboardWidgetModule MqttOpsModule(
        string type,
        string displayName,
        string category,
        string description,
        string icon,
        Type component,
        string title,
        IReadOnlyList<DashboardWidgetPropertyGroupDefinition> groups,
        IReadOnlyList<string> dataRequirements,
        int preferredColumns,
        IReadOnlyList<string>? compatibilityTypeIds = null)
        => new(
            new DashboardWidgetDescriptor(
                type,
                displayName,
                category,
                description,
                icon,
                displayName,
                RendererKind(type),
                DashboardWidgetEditorKind.Basic,
                dataRequirements),
            component,
            component,
            EventConfiguration(title),
            groups,
            new DashboardWidgetStyleDefinition(),
            new DashboardWidgetLayoutContract(1, 1, preferredColumns, 1),
            compatibilityTypeIds);

    private static DashboardWidgetRendererKind RendererKind(string type)
        => type switch
        {
            DashboardWidgetCatalog.PayloadDistributionType => DashboardWidgetRendererKind.PayloadDistribution,
            DashboardWidgetCatalog.QosBreakdownType => DashboardWidgetRendererKind.QosBreakdown,
            DashboardWidgetCatalog.RetainBreakdownType => DashboardWidgetRendererKind.RetainBreakdown,
            _ => DashboardWidgetRendererKind.Kpi
        };

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

    private static DashboardWidgetPropertyGroupDefinition BucketGroup()
        => new("buckets", "Buckets", [
            new("bucketMode", "Mode", DashboardWidgetPropertyEditorKind.Select),
            new("bucketCount", "Count", DashboardWidgetPropertyEditorKind.Number),
            new("unit", "Unit", DashboardWidgetPropertyEditorKind.Select)
        ]);

    private static DashboardWidgetPropertyGroupDefinition BreakdownGroup()
        => new("breakdown", "Breakdown", [
            new("displayMode", "Mode", DashboardWidgetPropertyEditorKind.Select),
            new("showLegend", "Legend", DashboardWidgetPropertyEditorKind.Toggle),
            new("palette", "Palette", DashboardWidgetPropertyEditorKind.Select)
        ]);

    private static IReadOnlyList<DashboardWidgetPropertyOption> MetricOptions()
        => [.. DashboardWidgetCatalog.MetricOptions.Select(static metric => new DashboardWidgetPropertyOption(metric.Id, metric.Label))];
}

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
                [
                    MetricGroup("topic-metric", "Topic metric"),
                    CategoryGroup("top-topics", "Top topics")
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
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["title"] = "Topic tree",
                    [DashboardWidgetCatalog.ExcludeSystemTopicsKey] = "true"
                },
                [TopicTreeGroup()],
                new DashboardWidgetStyleDefinition(),
                new DashboardWidgetLayoutContract(1, 2, 2, 2))
        ];

    private static DashboardWidgetModule TopicActivityModule(
        string type,
        string displayName,
        string category,
        string description,
        string icon,
        Type component,
        string title,
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
            EventConfiguration(title),
            groups,
            new DashboardWidgetStyleDefinition(),
            new DashboardWidgetLayoutContract(1, 1, preferredColumns, preferredRows));

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

    private static DashboardWidgetPropertyGroupDefinition CategoryGroup(string id = "categories", string title = "Categories")
        => new(id, title, [
            new("groupBy", "Group by", DashboardWidgetPropertyEditorKind.Select),
            new("limit", "Limit", DashboardWidgetPropertyEditorKind.Number),
            new("palette", "Palette", DashboardWidgetPropertyEditorKind.Select)
        ]);

    private static DashboardWidgetPropertyGroupDefinition TopicTreeGroup()
        => new("topic-tree", "Topic tree", [
            new("title", "Title", DashboardWidgetPropertyEditorKind.Text),
            new(DashboardWidgetCatalog.ExcludeSystemTopicsKey, "System topics", DashboardWidgetPropertyEditorKind.Toggle),
            new("depth", "Depth", DashboardWidgetPropertyEditorKind.Number),
            new("badges", "Badges", DashboardWidgetPropertyEditorKind.Select)
        ]);

    private static IReadOnlyList<DashboardWidgetPropertyOption> MetricOptions()
        => [.. DashboardWidgetCatalog.MetricOptions.Select(static metric => new DashboardWidgetPropertyOption(metric.Id, metric.Label))];
}
