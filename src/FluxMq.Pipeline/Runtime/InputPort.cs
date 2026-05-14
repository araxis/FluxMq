using FluxMq.Pipeline.Definitions;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Pipeline.Runtime;

public abstract class InputPort
{
    private protected InputPort(PortAddress address, Type valueType)
    {
        Address = address;
        ValueType = valueType;
    }

    public PortAddress Address { get; }
    public Type ValueType { get; }
}

public sealed class InputPort<T>(PortAddress address, ITargetBlock<T> target) : InputPort(address, typeof(T))
{
    public ITargetBlock<T> Target { get; } = target;
}
