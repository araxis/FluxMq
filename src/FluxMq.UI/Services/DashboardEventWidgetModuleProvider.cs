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
                    MetricQueryGroup("counter-source", "Counter source")
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
            configuration[DashboardEventGaugeWidgetOptions.StyleKey] = DashboardEventGaugeWidgetOptions.StyleRing;
            configuration[DashboardEventGaugeWidgetOptions.MinKey] = DashboardEventGaugeWidgetOptions.DefaultMin;
            configuration[DashboardEventGaugeWidgetOptions.MaxKey] = DashboardEventGaugeWidgetOptions.DefaultMax;
            configuration[DashboardEventGaugeWidgetOptions.TargetKey] = DashboardEventGaugeWidgetOptions.DefaultTarget;
            configuration[DashboardEventGaugeWidgetOptions.WarningKey] = DashboardEventGaugeWidgetOptions.DefaultWarning;
            configuration[DashboardEventGaugeWidgetOptions.CriticalKey] = DashboardEventGaugeWidgetOptions.DefaultCritical;
            configuration[DashboardEventGaugeWidgetOptions.NormalColorKey] = DashboardEventGaugeWidgetOptions.DefaultNormalColor;
            configuration[DashboardEventGaugeWidgetOptions.WarningColorKey] = DashboardEventGaugeWidgetOptions.DefaultWarningColor;
            configuration[DashboardEventGaugeWidgetOptions.CriticalColorKey] = DashboardEventGaugeWidgetOptions.DefaultCriticalColor;
        }
        else if (string.Equals(type, DashboardWidgetCatalog.EventCounterType, StringComparison.Ordinal))
        {
            foreach (var (key, value) in DashboardMetricVisualizationCatalog.Find(DashboardMetricVisualizationIds.Value)!.DefaultConfiguration)
            {
                configuration[key] = value;
            }

            configuration[DashboardMetricValueVisualizationOptions.TitleKey] = title;
            configuration[DashboardMetricValueVisualizationOptions.SubtitleKey] = "All runtime events";
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
            new(DashboardEventGaugeWidgetOptions.StyleKey, "Shape", DashboardWidgetPropertyEditorKind.Segmented, Options: [
                new(DashboardEventGaugeWidgetOptions.StyleRing, "Ring"),
                new(DashboardEventGaugeWidgetOptions.StyleMeter, "Meter")
            ]),
            new(DashboardEventGaugeWidgetOptions.MinKey, "Min", DashboardWidgetPropertyEditorKind.Number, DefaultValue: DashboardEventGaugeWidgetOptions.DefaultMin),
            new(DashboardEventGaugeWidgetOptions.MaxKey, "Max", DashboardWidgetPropertyEditorKind.Number, DefaultValue: DashboardEventGaugeWidgetOptions.DefaultMax),
            new(DashboardEventGaugeWidgetOptions.TargetKey, "Target", DashboardWidgetPropertyEditorKind.Number, DefaultValue: DashboardEventGaugeWidgetOptions.DefaultTarget),
            new(DashboardEventGaugeWidgetOptions.WarningKey, "Warning", DashboardWidgetPropertyEditorKind.Number, DefaultValue: DashboardEventGaugeWidgetOptions.DefaultWarning),
            new(DashboardEventGaugeWidgetOptions.CriticalKey, "Critical", DashboardWidgetPropertyEditorKind.Number, DefaultValue: DashboardEventGaugeWidgetOptions.DefaultCritical),
            new(DashboardEventGaugeWidgetOptions.NormalColorKey, "Normal color", DashboardWidgetPropertyEditorKind.Color, DefaultValue: DashboardEventGaugeWidgetOptions.DefaultNormalColor),
            new(DashboardEventGaugeWidgetOptions.WarningColorKey, "Warning color", DashboardWidgetPropertyEditorKind.Color, DefaultValue: DashboardEventGaugeWidgetOptions.DefaultWarningColor),
            new(DashboardEventGaugeWidgetOptions.CriticalColorKey, "Critical color", DashboardWidgetPropertyEditorKind.Color, DefaultValue: DashboardEventGaugeWidgetOptions.DefaultCriticalColor)
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
