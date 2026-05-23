using FluxMq.UI.Components.Diagram;
using FluxMq.UI.Components.Workspace.Nodes.ConditionRouter;
using FluxMq.UI.Components.Workspace.Nodes.ConnectionStateTrigger;
using FluxMq.UI.Components.Workspace.Nodes.DynamicMapper;
using FluxMq.UI.Components.Workspace.Nodes.Generic;
using FluxMq.UI.Components.Workspace.Nodes.JsonSchemaValidator;
using FluxMq.UI.Components.Workspace.Nodes.MessageFilter;
using FluxMq.UI.Components.Workspace.Nodes.MetricNode;
using FluxMq.UI.Components.Workspace.Nodes.MqttTrigger;
using FluxMq.UI.Components.Workspace.Nodes.PayloadInspector;
using FluxMq.UI.Components.Workspace.Nodes.SessionSource;

namespace FluxMq.UI.Services;

/// <summary>
/// Maps a flow node type (e.g. "mqtt.trigger") to the Razor component that
/// renders its in-diagram widget. The diagram's per-node template looks up
/// the type and renders it via &lt;DynamicComponent&gt;, so each component kind
/// owns its own UI without sharing a giant switch statement.
/// </summary>
public sealed class NodeWidgetRegistry
{
    private readonly Dictionary<string, Type> _widgets = new(StringComparer.Ordinal)
    {
        ["session.source"] = typeof(StoredSessionSourceNodeWidget),
        ["mqtt.live-source"] = typeof(GenericFlowNodeWidget),
        ["mqtt.trigger"] = typeof(MqttTriggerNodeWidget),
        ["mqtt.connection-state-trigger"] = typeof(ConnectionStateTriggerNodeWidget),
        ["mqtt.message-filter"] = typeof(MessageFilterNodeWidget),
        ["generated.source"] = typeof(GenericFlowNodeWidget),
        ["replay.source"] = typeof(GenericFlowNodeWidget),
        ["mqtt.payload-inspector"] = typeof(PayloadInspectorNodeWidget),
        ["mqtt.condition-router"] = typeof(ConditionRouterNodeWidget),
        ["json.schema-validator"] = typeof(JsonSchemaValidatorNodeWidget),
        ["flow.mapper"] = typeof(DynamicMapperNodeWidget),
        // Hidden compatibility aliases for definitions created before flow.mapper.
        ["mqtt.publish-request"] = typeof(GenericFlowNodeWidget),
        ["mqtt.publisher"] = typeof(GenericFlowNodeWidget),
        ["mqtt.recording-request"] = typeof(GenericFlowNodeWidget),
        ["mqtt.recorder"] = typeof(GenericFlowNodeWidget),
        ["file.write-request"] = typeof(GenericFlowNodeWidget),
        ["file.writer"] = typeof(GenericFlowNodeWidget),
        ["mqtt.metrics"] = typeof(MqttMetricsNodeWidget),
        ["mqtt.metrics-sink"] = typeof(MqttMetricsNodeWidget)
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
