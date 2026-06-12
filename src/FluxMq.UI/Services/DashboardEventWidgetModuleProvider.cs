using FluxMq.UI.Components.Workspace;
using FluxMq.UI.Models;
using MudBlazor;

namespace FluxMq.UI.Services;

public sealed class DashboardEventWidgetModuleProvider : IDashboardWidgetModuleProvider
{
    public string Id => "events";

    public IReadOnlyList<DashboardWidgetModule> CreateModules()
    {
        var eventFilter = EventFilterProperties();
        return
        [
            EventModule(
                DashboardWidgetCatalog.EventCounterType,
                "Event Counter",
                "Events",
                "Counts matching runtime events.",
                Icons.Material.Filled.Numbers,
                typeof(DashboardEventCounterModuleView),
                "Events",
                "eventCounter",
                [
                    MetricQueryGroup("counter-source", "Counter source"),
                    FormatGroup("count-format", "Count display")
                ]),
            EventModule(
                DashboardWidgetCatalog.LatestEventType,
                "Latest Event",
                "Events",
                "Shows the latest matching runtime event.",
                Icons.Material.Filled.Bolt,
                typeof(DashboardLatestEventModuleView),
                "Latest event",
                "latestEvent",
                [eventFilter, FieldGroup("fields", "Fields")],
                preferredColumns: 2,
                preferredRows: 1),
            EventModule(
                DashboardWidgetCatalog.EventRateType,
                "Event Rate",
                "Events",
                "Shows the current runtime event rate.",
                Icons.Material.Filled.Speed,
                typeof(DashboardEventRateModuleView),
                "Event rate",
                "eventRate",
                [
                    MetricQueryGroup("rate-source", "Rate source"),
                    FormatGroup("rate-format", "Rate display")
                ],
                preferredColumns: 2),
            EventModule(
                DashboardWidgetCatalog.EventGaugeType,
                "Event Gauge",
                "Events",
                "Renders one metric as a gauge.",
                Icons.Material.Filled.DonutLarge,
                typeof(DashboardEventGaugeModuleView),
                "Event gauge",
                "eventGauge",
                [
                    MetricQueryGroup("gauge-source", "Gauge source"),
                    GaugeGroup(),
                    ThresholdGroup()
                ],
                preferredColumns: 2),
            EventModule(
                DashboardWidgetCatalog.EventTableType,
                "Event Table",
                "Events",
                "Lists recent matching runtime events.",
                Icons.Material.Filled.TableRows,
                typeof(DashboardEventTableModuleView),
                "Event table",
                "eventTable",
                [eventFilter, TableGroup()],
                preferredColumns: 2,
                preferredRows: 2)
        ];
    }

    private static DashboardWidgetModule EventModule(
        string type,
        string displayName,
        string category,
        string description,
        string icon,
        Type component,
        string title,
        string instanceNamePrefix,
        IReadOnlyList<DashboardWidgetPropertyGroupDefinition> groups,
        int preferredColumns = 1,
        int preferredRows = 1)
    {
        var configuration = UsesFocusedMetricQuery(type)
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["title"] = title
            }
            : EventConfiguration(title);
        if (string.Equals(type, DashboardWidgetCatalog.EventGaugeType, StringComparison.Ordinal))
        {
            configuration[DashboardWidgetCatalog.GaugeStyleKey] = DashboardWidgetCatalog.GaugeStyleRing;
            configuration[DashboardWidgetCatalog.GaugeMinKey] = DashboardWidgetCatalog.GaugeDefaultMin;
            configuration[DashboardWidgetCatalog.GaugeMaxKey] = DashboardWidgetCatalog.GaugeDefaultMax;
            configuration[DashboardWidgetCatalog.GaugeTargetKey] = DashboardWidgetCatalog.GaugeDefaultTarget;
            configuration[DashboardWidgetCatalog.GaugeWarningKey] = DashboardWidgetCatalog.GaugeDefaultWarning;
            configuration[DashboardWidgetCatalog.GaugeCriticalKey] = DashboardWidgetCatalog.GaugeDefaultCritical;
            configuration[DashboardWidgetCatalog.GaugeNormalColorKey] = DashboardWidgetCatalog.GaugeDefaultNormalColor;
            configuration[DashboardWidgetCatalog.GaugeWarningColorKey] = DashboardWidgetCatalog.GaugeDefaultWarningColor;
            configuration[DashboardWidgetCatalog.GaugeCriticalColorKey] = DashboardWidgetCatalog.GaugeDefaultCriticalColor;
        }

        return new DashboardWidgetModule(
            new DashboardWidgetDescriptor(
                type,
                displayName,
                category,
                description,
                icon,
                displayName,
                RendererKind(type),
                DashboardWidgetEditorKind.Basic,
                ["runtimeEvents"]),
            component,
            component,
            configuration,
            groups,
            new DashboardWidgetStyleDefinition(),
            new DashboardWidgetLayoutContract(1, 1, preferredColumns, preferredRows),
            InstanceNamePrefix: instanceNamePrefix);
    }

    private static bool UsesFocusedMetricQuery(string type)
        => string.Equals(type, DashboardWidgetCatalog.EventCounterType, StringComparison.Ordinal) ||
           string.Equals(type, DashboardWidgetCatalog.EventGaugeType, StringComparison.Ordinal) ||
           string.Equals(type, DashboardWidgetCatalog.EventRateType, StringComparison.Ordinal);

    private static Dictionary<string, string> EventConfiguration(string title)
    {
        var configuration = new Dictionary<string, string>(DashboardEventFilterCatalog.Shared.CreateEmptyConfiguration(), StringComparer.Ordinal)
        {
            ["title"] = title,
            [DashboardWidgetCatalog.PrimaryMetricKey] = DashboardWidgetCatalog.MetricMessages
        };
        return configuration;
    }

    private static DashboardWidgetRendererKind RendererKind(string type)
        => type switch
        {
            DashboardWidgetCatalog.EventRateType => DashboardWidgetRendererKind.Rate,
            DashboardWidgetCatalog.EventGaugeType => DashboardWidgetRendererKind.Gauge,
            DashboardWidgetCatalog.EventTableType => DashboardWidgetRendererKind.EventTable,
            DashboardWidgetCatalog.LatestEventType => DashboardWidgetRendererKind.LatestEvent,
            _ => DashboardWidgetRendererKind.Kpi
        };

    private static DashboardWidgetPropertyGroupDefinition MetricQueryGroup(string id, string title)
        => new(id, title, [
            new("metric", "Metric query", DashboardWidgetPropertyEditorKind.Metric, HelpText: "Reusable metric query used by this widget.")
        ]);

    private static DashboardWidgetPropertyGroupDefinition EventFilterProperties()
        => new("filters", "Filters", [
            new(DashboardEventFilterCatalog.EventTypeKey, "Event", DashboardWidgetPropertyEditorKind.Select),
            new(DashboardEventFilterCatalog.StatusKey, "Status", DashboardWidgetPropertyEditorKind.Select),
            new(DashboardEventFilterCatalog.TopicStartsWithKey, "Topic starts", DashboardWidgetPropertyEditorKind.TopicFilter),
            new(DashboardEventFilterCatalog.TopicNotStartsWithKey, "Exclude topic", DashboardWidgetPropertyEditorKind.TopicFilter)
        ]);

    private static DashboardWidgetPropertyGroupDefinition FormatGroup(string id, string title)
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

    private static DashboardWidgetPropertyGroupDefinition GaugeGroup()
        => new("gauge", "Gauge", [
            new(DashboardWidgetCatalog.GaugeStyleKey, "Shape", DashboardWidgetPropertyEditorKind.Segmented, Options: [
                new(DashboardWidgetCatalog.GaugeStyleRing, "Ring"),
                new(DashboardWidgetCatalog.GaugeStyleMeter, "Meter")
            ]),
            new(DashboardWidgetCatalog.GaugeMinKey, "Min", DashboardWidgetPropertyEditorKind.Number, DefaultValue: DashboardWidgetCatalog.GaugeDefaultMin),
            new(DashboardWidgetCatalog.GaugeMaxKey, "Max", DashboardWidgetPropertyEditorKind.Number, DefaultValue: DashboardWidgetCatalog.GaugeDefaultMax),
            new(DashboardWidgetCatalog.GaugeTargetKey, "Target", DashboardWidgetPropertyEditorKind.Number, DefaultValue: DashboardWidgetCatalog.GaugeDefaultTarget),
            new(DashboardWidgetCatalog.GaugeWarningKey, "Warning", DashboardWidgetPropertyEditorKind.Number, DefaultValue: DashboardWidgetCatalog.GaugeDefaultWarning),
            new(DashboardWidgetCatalog.GaugeCriticalKey, "Critical", DashboardWidgetPropertyEditorKind.Number, DefaultValue: DashboardWidgetCatalog.GaugeDefaultCritical),
            new(DashboardWidgetCatalog.GaugeNormalColorKey, "Normal color", DashboardWidgetPropertyEditorKind.Color, DefaultValue: DashboardWidgetCatalog.GaugeDefaultNormalColor),
            new(DashboardWidgetCatalog.GaugeWarningColorKey, "Warning color", DashboardWidgetPropertyEditorKind.Color, DefaultValue: DashboardWidgetCatalog.GaugeDefaultWarningColor),
            new(DashboardWidgetCatalog.GaugeCriticalColorKey, "Critical color", DashboardWidgetPropertyEditorKind.Color, DefaultValue: DashboardWidgetCatalog.GaugeDefaultCriticalColor)
        ]);

    private static DashboardWidgetPropertyGroupDefinition TableGroup()
        => new("table", "Table", [
            new("rowCount", "Rows", DashboardWidgetPropertyEditorKind.Number),
            new("density", "Density", DashboardWidgetPropertyEditorKind.Select),
            new("payloadPreview", "Payload", DashboardWidgetPropertyEditorKind.Toggle)
        ]);

    private static DashboardWidgetPropertyGroupDefinition FieldGroup(string id, string title)
        => new(id, title, [
            new("showTopic", "Topic", DashboardWidgetPropertyEditorKind.Toggle),
            new("showStatus", "Status", DashboardWidgetPropertyEditorKind.Toggle),
            new("showPayload", "Payload", DashboardWidgetPropertyEditorKind.Toggle),
            new("timestampFormat", "Time", DashboardWidgetPropertyEditorKind.Select)
        ]);
}
