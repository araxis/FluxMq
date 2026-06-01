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
using FluxMq.UI.Components.Workspace.Nodes.MqttTrigger;
using FluxMq.UI.Components.Workspace.Nodes.Payloads;
using FluxMq.UI.Components.Workspace.Nodes.PayloadInspector;
using FluxMq.UI.Components.Workspace.Nodes.SessionSource;
using FluxMq.UI.Components.Workspace.Nodes.Sources;
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
        "flow.assert" => new FlowAssertionNodeModel(id, position, nodeName, descriptor, isResource),
        "json.schema-validator" => new JsonSchemaValidatorNodeModel(id, position, nodeName, descriptor, isResource),
        "flow.mapper" => new DynamicMapperNodeModel(id, position, nodeName, descriptor, isResource),
        "generated.source" => new GeneratedSourceNodeModel(id, position, nodeName, descriptor, isResource),
        "replay.source" => new ReplaySourceNodeModel(id, position, nodeName, descriptor, isResource),
        "session.source" => new SessionSourceNodeModel(id, position, nodeName, descriptor, isResource),
        "mqtt.publisher" => new MqttPublisherNodeModel(id, position, nodeName, descriptor, isResource),
        "mqtt.recorder" => new MqttRecorderNodeModel(id, position, nodeName, descriptor, isResource),
        "file.writer" => new FileWriterNodeModel(id, position, nodeName, descriptor, isResource),
        "http.request" => new HttpRequestNodeModel(id, position, nodeName, descriptor, isResource),
        "payload.inspect" => new PayloadInspectNodeModel(id, position, nodeName, descriptor, isResource),
        "mqtt.payload-inspector" => new PayloadInspectorNodeModel(id, position, nodeName, descriptor, isResource),
        "mqtt.metrics" => new MqttMetricsNodeModel(id, position, nodeName, descriptor, isResource),
        _ => new FlowDiagramNodeModel(id, position, nodeName, nodeType, descriptor, isResource)
    };
}
