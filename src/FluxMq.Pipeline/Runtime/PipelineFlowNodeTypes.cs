using FluxMq.Pipeline.Definitions;

namespace FluxMq.Pipeline.Runtime;

public static class PipelineFlowNodeTypes
{
    public static readonly FlowNodeType MessageSource = new("mqtt.message-source");
    public static readonly FlowNodeType PayloadInspector = new("mqtt.payload-inspector");
    public static readonly FlowNodeType MetricsSink = new("mqtt.metrics-sink");
}
