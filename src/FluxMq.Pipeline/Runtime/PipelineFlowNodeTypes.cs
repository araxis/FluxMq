using FluxMq.Pipeline.Definitions;

namespace FluxMq.Pipeline.Runtime;

public static class PipelineFlowNodeTypes
{
    /// <summary>Resource node: owns the live MQTT session (broker settings, lifecycle).</summary>
    public static readonly NodeType Connection = new("mqtt.connection");

    /// <summary>Workflow node: bound to a connection, owns its own subscription list, emits envelopes.</summary>
    public static readonly NodeType Trigger = new("mqtt.trigger");

    /// <summary>Workflow node: owns a live MQTT session directly and emits envelopes.</summary>
    public static readonly NodeType LiveSource = new("mqtt.live-source");

    /// <summary>Workflow node: streams envelopes from a stored session.</summary>
    public static readonly NodeType StoredSessionSource = new("session.source");

    /// <summary>Workflow node: emits deterministic generated/test envelopes.</summary>
    public static readonly NodeType GeneratedSource = new("generated.source");

    public static readonly NodeType PayloadInspector = new("mqtt.payload-inspector");
    public static readonly NodeType MqttMetrics = new("mqtt.metrics");
    public static readonly NodeType MessageFilter = new("mqtt.message-filter");

    /// <summary>User-facing mapper node. Configuration chooses input/output types and mapping engine.</summary>
    public static readonly NodeType DynamicMapper = new("flow.mapper");

    /// <summary>Compatibility/internal mapper type for definitions created before the generic mapper surface.</summary>
    public static readonly NodeType PublishRequestMapper = new("mqtt.publish-request");
    public static readonly NodeType MqttPublisher = new("mqtt.publisher");

    /// <summary>Compatibility/internal mapper type for definitions created before the generic mapper surface.</summary>
    public static readonly NodeType RecordingRequestMapper = new("mqtt.recording-request");
    public static readonly NodeType MqttRecorder = new("mqtt.recorder");

    /// <summary>Compatibility/internal mapper type for definitions created before the generic mapper surface.</summary>
    public static readonly NodeType FileWriteRequestMapper = new("file.write-request");
    public static readonly NodeType FileWriter = new("file.writer");
}
