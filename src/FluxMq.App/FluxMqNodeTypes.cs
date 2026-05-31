using FluxFlow.Engine.Definitions;

namespace FluxMq.App;

public static class FluxMqNodeTypes
{
    public static readonly NodeType Connection = new("mqtt.connection");
    public static readonly NodeType Trigger = new("mqtt.trigger");
    public static readonly NodeType ConnectionStateTrigger = new("mqtt.connection-state-trigger");
    public static readonly NodeType StoredSessionSource = new("session.source");
    public static readonly NodeType ReplaySource = new("replay.source");
    public static readonly NodeType GeneratedSource = new("generated.source");
    public static readonly NodeType PayloadInspector = new("mqtt.payload-inspector");
    public static readonly NodeType MqttMetrics = new("mqtt.metrics");
    public static readonly NodeType FlowLogger = new("flow.logger");
    public static readonly NodeType MessageFilter = new("mqtt.message-filter");
    public static readonly NodeType ConditionRouter = new("mqtt.condition-router");
    public static readonly NodeType FlowAssertion = new("flow.assertion");
    public static readonly NodeType JsonSchemaValidator = new("json.schema-validator");
    public static readonly NodeType DynamicMapper = new("flow.mapper");
    public static readonly NodeType MqttPublisher = new("mqtt.publisher");
    public static readonly NodeType MqttRecorder = new("mqtt.recorder");
    public static readonly NodeType FileWriter = new("file.writer");
}
