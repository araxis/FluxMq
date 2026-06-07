using FluxMq.Core.Models;
using FluxMq.UI.Models;
using FluxMq.UI.Services;

namespace FluxMq.UI.Components.Workspace;

public sealed record DashboardWidgetRenderContext(
    FlowWorkspaceService Project,
    DashboardWidgetSnapshot Widget,
    DashboardWidgetRenderMode Mode,
    DashboardEventSnapshot? SnapshotOverride = null,
    IReadOnlyList<MqttEnvelope>? TopicMessagesOverride = null,
    DashboardMetricValue? MetricValueOverride = null)
{
    public DashboardEventSnapshot Snapshot => SnapshotOverride ?? Project.GetDashboardEventSnapshot(Widget);

    public DashboardMetricValue MetricValue => MetricValueOverride ?? Project.GetDashboardMetricValue(Widget);

    public IReadOnlyList<MqttEnvelope>? TopicMessages => TopicMessagesOverride;
}

public enum DashboardWidgetRenderMode
{
    EditCell,
    Live
}
