using FluxMq.Pipeline.Components;
using FluxMq.Pipeline.Definitions;

namespace FluxMq.Pipeline.Runtime;

public sealed record RuntimeNode(
    NodeName Name,
    IFlowNode Node,
    IReadOnlyDictionary<PortName, InputPort> Inputs,
    IReadOnlyDictionary<PortName, OutputPort> Outputs)
{
    public static RuntimeNode Create(
        NodeName name,
        IFlowNode node,
        IEnumerable<InputPort>? inputs = null,
        IEnumerable<OutputPort>? outputs = null)
        => new(
            name,
            node,
            (inputs ?? []).ToDictionary(port => port.Name),
            (outputs ?? []).ToDictionary(port => port.Name));
}
