using FluxMq.Pipeline.Definitions;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Pipeline.Runtime;

public abstract class FlowInputPort
{
    private protected FlowInputPort(FlowPortName name, Type valueType)
    {
        Name = name;
        ValueType = valueType;
    }

    public FlowPortName Name { get; }
    public Type ValueType { get; }
}

public sealed class FlowInputPort<T> : FlowInputPort
{
    public FlowInputPort(FlowPortName name, ITargetBlock<T> target)
        : base(name, typeof(T))
    {
        Target = target;
    }

    public ITargetBlock<T> Target { get; }
}
