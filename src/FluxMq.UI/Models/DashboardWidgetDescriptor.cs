namespace FluxMq.UI.Models;

public enum DashboardWidgetRendererKind
{
    Unknown,
    Kpi,
    StatusStrip,
    StatusValue,
    Rate,
    Gauge,
    Chart,
    Donut,
    EventTable,
    LatestEvent,
    TopicTree,
    TopicActivity,
    PayloadDistribution,
    QosRetainBreakdown,
    QosBreakdown,
    RetainBreakdown
}

public enum DashboardWidgetEditorKind
{
    Basic,
    MetricTile,
    StatusStrip,
    Gauge,
    Chart,
    Table,
    TopicTree,
    Payload,
    Breakdown
}

public sealed record DashboardWidgetDescriptor(
    string Type,
    string DisplayName,
    string Category,
    string Description,
    string Icon,
    string Preview = "",
    DashboardWidgetRendererKind RendererKind = DashboardWidgetRendererKind.Unknown,
    DashboardWidgetEditorKind EditorKind = DashboardWidgetEditorKind.Basic,
    IReadOnlyList<string>? DataRequirements = null)
{
    public IReadOnlyList<string> DataRequirements { get; init; } = DataRequirements ?? [];
}
