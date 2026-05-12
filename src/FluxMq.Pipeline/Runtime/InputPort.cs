using FluxMq.Pipeline.Definitions;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Pipeline.Runtime;

public abstract class InputPort
{
    private protected InputPort(PortName name, Type valueType)
    {
        Name = name;
        ValueType = valueType;
    }

    public PortName Name { get; }
    public Type ValueType { get; }
}

public sealed class InputPort<T>(PortName name, ITargetBlock<T> target) : InputPort(name, typeof(T))
{
    public ITargetBlock<T> Target { get; } = target;
}
