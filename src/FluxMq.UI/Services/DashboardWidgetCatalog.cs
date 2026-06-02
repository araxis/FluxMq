using FluxMq.UI.Models;
using MudBlazor;

namespace FluxMq.UI.Services;

public sealed class DashboardWidgetCatalog
{
    public const string EventCounterType = "event.counter";
    public const string LatestEventType = "event.latest";
    public const string EventRateType = "event.rate";

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
            Icons.Material.Filled.Speed)
    ];

    public IReadOnlyList<DashboardWidgetDescriptor> Widgets => _widgets;

    public DashboardWidgetDescriptor? Find(string type)
        => _widgets.FirstOrDefault(widget => string.Equals(widget.Type, type, StringComparison.Ordinal));
}
