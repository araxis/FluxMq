using FluxMq.Pipeline.Definitions;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Pipeline.Runtime;

public abstract class OutputPort
{
    private protected OutputPort(PortName name, Type valueType)
    {
        Name = name;
        ValueType = valueType;
    }

    public PortName Name { get; }
    public Type ValueType { get; }

    public abstract IDisposable? TryLinkTo(
        InputPort input,
        bool propagateCompletion,
        out ApplicationRuntimeBuildError? error);
}

public sealed class OutputPort<T> : OutputPort
{
    public OutputPort(PortName name, ISourceBlock<T> source)
        : base(name, typeof(T))
    {
        Source = source;
    }

    public ISourceBlock<T> Source { get; }

    public override IDisposable? TryLinkTo(
        InputPort input,
        bool propagateCompletion,
        out ApplicationRuntimeBuildError? error)
    {
        if (input is not InputPort<T> typedInput)
        {
            error = new(
                ApplicationRuntimeBuildErrorCode.PortTypeMismatch,
                $"Cannot link '{Name}' ({ValueType.Name}) to '{input.Name}' ({input.ValueType.Name}).",
                PortName: input.Name);
            return null;
        }

        try
        {
            error = null;
            return Source.LinkTo(
                typedInput.Target,
                new DataflowLinkOptions { PropagateCompletion = propagateCompletion });
        }
        catch (Exception exception)
        {
            error = new(
                ApplicationRuntimeBuildErrorCode.LinkFailed,
                $"Failed to link '{Name}' to '{input.Name}': {exception.Message}",
                PortName: input.Name);
            return null;
        }
    }
}
