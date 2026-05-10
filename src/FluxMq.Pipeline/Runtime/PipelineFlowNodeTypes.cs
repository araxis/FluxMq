using FluxMq.Pipeline.Definitions;

namespace FluxMq.Pipeline.Runtime;

public static class PipelineFlowNodeTypes
{
    /// <summary>Resource node: owns the live MQTT session (broker settings, lifecycle).</summary>
    public static readonly FlowNodeType Connection = new("mqtt.connection");

    /// <summary>Workflow node: bound to a connection, owns its own subscription list, emits envelopes.</summary>
    public static readonly FlowNodeType Trigger = new("mqtt.trigger");

    public static readonly FlowNodeType PayloadInspector = new("mqtt.payload-inspector");
    public static readonly FlowNodeType MetricsSink = new("mqtt.metrics-sink");
}
