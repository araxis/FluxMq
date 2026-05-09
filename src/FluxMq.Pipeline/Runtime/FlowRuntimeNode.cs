using FluxMq.Pipeline.Components;
using FluxMq.Pipeline.Definitions;

namespace FluxMq.Pipeline.Runtime;

public sealed record FlowRuntimeNode(
    FlowNodeName Name,
    IFlowNode Node,
    IReadOnlyDictionary<FlowPortName, FlowInputPort> Inputs,
    IReadOnlyDictionary<FlowPortName, FlowOutputPort> Outputs)
{
    public static FlowRuntimeNode Create(
        FlowNodeName name,
        IFlowNode node,
        IEnumerable<FlowInputPort>? inputs = null,
        IEnumerable<FlowOutputPort>? outputs = null)
        => new(
            name,
            node,
            (inputs ?? []).ToDictionary(port => port.Name),
            (outputs ?? []).ToDictionary(port => port.Name));
}
