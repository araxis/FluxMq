using FluxMq.Pipeline.Definitions;

namespace FluxMq.Pipeline.Runtime;

public static class PipelineFlowNodeTypes
{
    /// <summary>Resource node: owns the live MQTT session (broker settings, lifecycle).</summary>
    public static readonly NodeType Connection = new("mqtt.connection");

    /// <summary>Workflow node: bound to a connection, owns its own subscription list, emits envelopes.</summary>
    public static readonly NodeType Trigger = new("mqtt.trigger");

    public static readonly NodeType TrafficSource = new("traffic.source");
    public static readonly NodeType PayloadInspector = new("mqtt.payload-inspector");
    public static readonly NodeType MetricsSink = new("mqtt.metrics-sink");
}
