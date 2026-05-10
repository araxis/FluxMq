using FluxMq.UI.Models;
using Microsoft.AspNetCore.Components;

namespace FluxMq.UI.Components.Workspace.Nodes;

/// <summary>
/// Base class for per-type node widgets. Each concrete widget (MqttConnection,
/// Trigger, Inspector, MetricsSink, ...) inherits this and renders its own
/// header + body for the node it represents. Common chrome (selection state,
/// activity badge, port chips) is shared via the partial methods below.
/// </summary>
public abstract class NodeWidgetBase : ComponentBase
{
    [Parameter, EditorRequired]
    public FlowDiagramNodeModel Node { get; set; } = null!;

    protected void Toggle() => Node.Toggle();
}
