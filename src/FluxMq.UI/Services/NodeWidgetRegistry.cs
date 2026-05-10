using FluxMq.UI.Components.Workspace.Nodes;

namespace FluxMq.UI.Services;

/// <summary>
/// Maps a flow node type (e.g. "mqtt.connection", "mqtt.trigger") to the Razor
/// component that renders its in-diagram widget. The diagram's per-node template
/// looks up the type and renders it via &lt;DynamicComponent&gt;, so each component
/// kind owns its own UI without sharing a giant switch statement.
/// </summary>
public sealed class NodeWidgetRegistry
{
    private readonly Dictionary<string, Type> _widgets = new(StringComparer.Ordinal)
    {
        ["mqtt.connection"] = typeof(MqttConnectionNodeWidget),
        ["mqtt.trigger"] = typeof(MqttTriggerNodeWidget),
        ["mqtt.payload-inspector"] = typeof(PayloadInspectorNodeWidget),
        ["mqtt.metrics-sink"] = typeof(MetricsSinkNodeWidget)
    };

    /// <summary>Returns the widget type for the given node type, or the fallback default widget.</summary>
    public Type Resolve(string nodeType)
        => _widgets.TryGetValue(nodeType, out var type) ? type : typeof(DefaultNodeWidget);

    /// <summary>Registers (or replaces) the widget for a node type. Call from app start to extend.</summary>
    public NodeWidgetRegistry Register(string nodeType, Type widgetType)
    {
        ArgumentNullException.ThrowIfNull(widgetType);
        _widgets[nodeType] = widgetType;
        return this;
    }
}
