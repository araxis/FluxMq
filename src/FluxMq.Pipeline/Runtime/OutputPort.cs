using FluxMq.Pipeline.Definitions;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Pipeline.Runtime;

public abstract class OutputPort
{
    private protected OutputPort(PortAddress address, Type valueType)
    {
        Address = address;
        ValueType = valueType;
    }

    public PortAddress Address { get; }
    public Type ValueType { get; }

    public abstract IDisposable? TryLinkTo(
        InputPort input,
        bool propagateCompletion,
        out ApplicationRuntimeBuildError? error);
}

public sealed class OutputPort<T>(PortAddress address, ISourceBlock<T> source) : OutputPort(address, typeof(T))
{
    public ISourceBlock<T> Source { get; } = source;

    public override IDisposable? TryLinkTo(
        InputPort input,
        bool propagateCompletion,
        out ApplicationRuntimeBuildError? error)
    {
        if (input is not InputPort<T> typedInput)
        {
            error = new(
                ApplicationRuntimeBuildErrorCode.PortTypeMismatch,
                $"Cannot link '{Address}' ({ValueType.Name}) to '{input.Address}' ({input.ValueType.Name}).",
                PortName: input.Address.Port);
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
                $"Failed to link '{Address}' to '{input.Address}': {exception.Message}",
                PortName: input.Address.Port);
            return null;
        }
    }
}
