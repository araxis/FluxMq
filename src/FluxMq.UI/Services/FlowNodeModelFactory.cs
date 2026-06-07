using FluxMq.UI.Components.Diagram;
using FluxMq.UI.Components.Workspace.Nodes.Actors;
using FluxMq.UI.Components.Workspace.Nodes.ConditionRouter;
using FluxMq.UI.Components.Workspace.Nodes.ConnectionStateTrigger;
using FluxMq.UI.Components.Workspace.Nodes.DynamicMapper;
using FluxMq.UI.Components.Workspace.Nodes.FlowAssertion;
using FluxMq.UI.Components.Workspace.Nodes.Http;
using FluxMq.UI.Components.Workspace.Nodes.JsonSchemaValidator;
using FluxMq.UI.Components.Workspace.Nodes.MessageFilter;
using FluxMq.UI.Components.Workspace.Nodes.MetricNode;
using FluxMq.UI.Components.Workspace.Nodes.MetricSource;
using FluxMq.UI.Components.Workspace.Nodes.MqttTrigger;
using FluxMq.UI.Components.Workspace.Nodes.Payloads;
using FluxMq.UI.Components.Workspace.Nodes.PayloadInspector;
using FluxMq.UI.Components.Workspace.Nodes.Routing;
using FluxMq.UI.Components.Workspace.Nodes.SessionSource;
using FluxMq.UI.Components.Workspace.Nodes.Sources;
using FluxMq.UI.Components.Workspace.Nodes.StateReducer;
using FluxMq.UI.Components.Workspace.Nodes.Timers;
using FluxMq.UI.Models;
using DiagramPoint = Blazor.Diagrams.Core.Geometry.Point;

namespace FluxMq.UI.Services;

public static class FlowNodeModelFactory
{
    public static FlowDiagramNodeModel Create(
        string id,
        DiagramPoint position,
        string nodeName,
        string nodeType,
        FlowComponentDescriptor? descriptor,
        bool isResource) => nodeType switch
    {
        "mqtt.trigger" => new MqttTriggerNodeModel(id, position, nodeName, descriptor, isResource),
        "mqtt.connection-state-trigger" => new ConnectionStateTriggerNodeModel(id, position, nodeName, descriptor, isResource),
        "flow.filter" => new MessageFilterNodeModel(id, position, nodeName, descriptor, isResource),
        "flow.when" => new ConditionRouterNodeModel(id, position, nodeName, descriptor, isResource),
        RoutingNodeTypes.Switch => new RoutingSwitchNodeModel(id, position, nodeName, descriptor, isResource),
        RoutingNodeTypes.Correlation => new RoutingCorrelationNodeModel(id, position, nodeName, descriptor, isResource),
        RoutingNodeTypes.Window => new RoutingWindowNodeModel(id, position, nodeName, descriptor, isResource),
        RoutingNodeTypes.Join => new RoutingJoinNodeModel(id, position, nodeName, descriptor, isResource),
        RoutingNodeTypes.Fork => new RoutingForkNodeModel(id, position, nodeName, descriptor, isResource),
        RoutingNodeTypes.Merge => new RoutingMergeNodeModel(id, position, nodeName, descriptor, isResource),
        "flow.assert" => new FlowAssertionNodeModel(id, position, nodeName, descriptor, isResource),
        "json.schema-validator" => new JsonSchemaValidatorNodeModel(id, position, nodeName, descriptor, isResource),
        "flow.mapper" => new DynamicMapperNodeModel(id, position, nodeName, descriptor, isResource),
        "state.reducer" => new StateReducerNodeModel(id, position, nodeName, descriptor, isResource),
        "generated.source" => new GeneratedSourceNodeModel(id, position, nodeName, descriptor, isResource),
        "metric.source" => new MetricSourceNodeModel(id, position, nodeName, descriptor, isResource),
        "replay.source" => new ReplaySourceNodeModel(id, position, nodeName, descriptor, isResource),
        "session.source" => new SessionSourceNodeModel(id, position, nodeName, descriptor, isResource),
        "mqtt.publisher" => new MqttPublisherNodeModel(id, position, nodeName, descriptor, isResource),
        "mqtt.recorder" => new MqttRecorderNodeModel(id, position, nodeName, descriptor, isResource),
        "file.writer" => new FileWriterNodeModel(id, position, nodeName, descriptor, isResource),
        "http.request" => new HttpRequestNodeModel(id, position, nodeName, descriptor, isResource),
        "payload.inspect" => new PayloadInspectNodeModel(id, position, nodeName, descriptor, isResource),
        "mqtt.payload-inspector" => new PayloadInspectorNodeModel(id, position, nodeName, descriptor, isResource),
        "mqtt.metrics" => new MqttMetricsNodeModel(id, position, nodeName, descriptor, isResource),
        TimerNodeTypes.Interval => new TimerIntervalNodeModel(id, position, nodeName, descriptor, isResource),
        TimerNodeTypes.Schedule => new TimerScheduleNodeModel(id, position, nodeName, descriptor, isResource),
        TimerNodeTypes.Delay => new TimerDelayNodeModel(id, position, nodeName, descriptor, isResource),
        TimerNodeTypes.Debounce => new TimerDebounceNodeModel(id, position, nodeName, descriptor, isResource),
        TimerNodeTypes.Throttle => new TimerThrottleNodeModel(id, position, nodeName, descriptor, isResource),
        _ => new FlowDiagramNodeModel(id, position, nodeName, nodeType, descriptor, isResource)
    };
}
