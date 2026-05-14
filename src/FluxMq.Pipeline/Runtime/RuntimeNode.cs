using FluxMq.Pipeline.Components;
using FluxMq.Pipeline.Definitions;

namespace FluxMq.Pipeline.Runtime;

public sealed record RuntimeNode(
    NodeAddress Address,
    IFlowNode Node,
    IReadOnlyList<InputPort> Inputs,
    IReadOnlyList<OutputPort> Outputs)
{
    public InputPort? FindInput(PortName port)
        => Inputs.FirstOrDefault(p => p.Address.Port == port);

    public OutputPort? FindOutput(PortName port)
        => Outputs.FirstOrDefault(p => p.Address.Port == port);

    public static RuntimeNode Create(
        NodeAddress address,
        IFlowNode node,
        IEnumerable<InputPort>? inputs = null,
        IEnumerable<OutputPort>? outputs = null)
        => new(
            address,
            node,
            (inputs ?? []).ToArray(),
            (outputs ?? []).ToArray());
}
