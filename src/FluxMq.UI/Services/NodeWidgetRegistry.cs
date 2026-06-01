using FluxMq.UI.Components.Diagram;
using FluxMq.UI.Components.Workspace.Nodes.Actors;
using FluxMq.UI.Components.Workspace.Nodes.ConditionRouter;
using FluxMq.UI.Components.Workspace.Nodes.ConnectionStateTrigger;
using FluxMq.UI.Components.Workspace.Nodes.DynamicMapper;
using FluxMq.UI.Components.Workspace.Nodes.FlowAssertion;
using FluxMq.UI.Components.Workspace.Nodes.JsonSchemaValidator;
using FluxMq.UI.Components.Workspace.Nodes.MessageFilter;
using FluxMq.UI.Components.Workspace.Nodes.MetricNode;
using FluxMq.UI.Components.Workspace.Nodes.MqttTrigger;
using FluxMq.UI.Components.Workspace.Nodes.PayloadInspector;
using FluxMq.UI.Components.Workspace.Nodes.SessionSource;
using FluxMq.UI.Components.Workspace.Nodes.Sources;
using FluxMq.UI.Components.Workspace.Nodes.StateReducer;

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
        ["mqtt.trigger"] = typeof(MqttTriggerNodeWidget),
        ["mqtt.connection-state-trigger"] = typeof(ConnectionStateTriggerNodeWidget),
        ["flow.filter"] = typeof(MessageFilterNodeWidget),
        ["generated.source"] = typeof(GeneratedSourceNodeWidget),
        ["replay.source"] = typeof(ReplaySourceNodeWidget),
        ["mqtt.payload-inspector"] = typeof(PayloadInspectorNodeWidget),
        ["flow.when"] = typeof(ConditionRouterNodeWidget),
        ["flow.assert"] = typeof(FlowAssertionNodeWidget),
        ["json.schema-validator"] = typeof(JsonSchemaValidatorNodeWidget),
        ["flow.mapper"] = typeof(DynamicMapperNodeWidget),
        ["state.reducer"] = typeof(StateReducerNodeWidget),
        ["mqtt.publisher"] = typeof(MqttPublisherNodeWidget),
        ["mqtt.recorder"] = typeof(MqttRecorderNodeWidget),
        ["file.writer"] = typeof(FileWriterNodeWidget),
        ["mqtt.metrics"] = typeof(MqttMetricsNodeWidget)
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
