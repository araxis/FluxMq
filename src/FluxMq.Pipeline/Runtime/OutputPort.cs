using FluxMq.Pipeline.Components;
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
    public abstract Task Completion { get; }
    public abstract bool DrainWhenUnlinked { get; }

    public abstract IDisposable? TryLinkTo(
        InputPort input,
        bool propagateCompletion,
        out ApplicationRuntimeBuildError? error);

    public abstract IDisposable LinkToDiscard();
}

public sealed class OutputPort<T>(
    PortAddress address,
    ISourceBlock<T> source,
    bool drainWhenUnlinked = true)
    : OutputPort(address, typeof(T))
{
    public ISourceBlock<T> Source { get; } = source;
    public override Task Completion => Source.Completion;
    public override bool DrainWhenUnlinked { get; } = drainWhenUnlinked && typeof(T) != typeof(FlowError);

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

    public override IDisposable LinkToDiscard()
        => Source.LinkTo(
            DataflowBlock.NullTarget<T>(),
            new DataflowLinkOptions { PropagateCompletion = true });
}
