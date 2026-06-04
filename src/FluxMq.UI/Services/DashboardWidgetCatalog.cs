using FluxMq.UI.Models;
using MudBlazor;

namespace FluxMq.UI.Services;

public sealed class DashboardWidgetCatalog
{
    public const string EventCounterType = "event.counter";
    public const string LatestEventType = "event.latest";
    public const string EventRateType = "event.rate";
    public const string EventGaugeType = "event.gauge";
    public const string EventChartType = "event.chart";
    public const string TopicTreeType = "topic.tree";
    public const string ExcludeSystemTopicsKey = "excludeSystemTopics";

    private readonly IReadOnlyList<DashboardWidgetDescriptor> _widgets =
    [
        new(
            EventCounterType,
            "Event Counter",
            "Events",
            "Counts runtime events with optional event type and topic filters.",
            Icons.Material.Filled.Numbers),
        new(
            LatestEventType,
            "Latest Event",
            "Events",
            "Shows the latest runtime event that matches optional filters.",
            Icons.Material.Filled.Bolt),
        new(
            EventRateType,
            "Event Rate",
            "Events",
            "Shows the current event rate for matching runtime events.",
            Icons.Material.Filled.Speed),
        new(
            EventGaugeType,
            "Event Gauge",
            "Events",
            "Shows matching runtime events as a compact activity gauge.",
            Icons.Material.Filled.DonutLarge),
        new(
            EventChartType,
            "Event Chart",
            "Events",
            "Shows matching runtime event activity over the last minute.",
            Icons.Material.Filled.BarChart),
        new(
            TopicTreeType,
            "Topic Tree",
            "Topics",
            "Shows live MQTT topics as a dashboard tree.",
            Icons.Material.Filled.AccountTree)
    ];

    public IReadOnlyList<DashboardWidgetDescriptor> Widgets => _widgets;

    public DashboardWidgetDescriptor? Find(string type)
        => _widgets.FirstOrDefault(widget => string.Equals(widget.Type, type, StringComparison.Ordinal));

    public static bool IsEventWidget(string type)
        => string.Equals(type, EventCounterType, StringComparison.Ordinal) ||
           string.Equals(type, LatestEventType, StringComparison.Ordinal) ||
           string.Equals(type, EventRateType, StringComparison.Ordinal) ||
           string.Equals(type, EventGaugeType, StringComparison.Ordinal) ||
           string.Equals(type, EventChartType, StringComparison.Ordinal);

    public static bool IsTopicTreeWidget(string type)
        => string.Equals(type, TopicTreeType, StringComparison.Ordinal);
}
