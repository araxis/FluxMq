using Microsoft.AspNetCore.Components;

namespace FluxMq.UI.Components.Diagram;

/// <summary>
/// Base class for per-type node widgets. Each concrete widget (MqttConnection,
/// Trigger, Inspector, Metrics, ...) inherits this and renders its own
/// header + body for the node it represents. Common chrome (selection state,
/// activity state, and port labels) is shared via the partial methods below.
/// </summary>
public abstract class NodeWidgetBase : ComponentBase
{
    [Parameter, EditorRequired]
    public FlowDiagramNodeModel Node { get; set; } = null!;

    protected void Toggle() => Node.Toggle();
}
