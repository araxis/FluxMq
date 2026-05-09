using FluxMq.Pipeline.Definitions;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Pipeline.Runtime;

public abstract class FlowOutputPort
{
    private protected FlowOutputPort(FlowPortName name, Type valueType)
    {
        Name = name;
        ValueType = valueType;
    }

    public FlowPortName Name { get; }
    public Type ValueType { get; }

    public abstract IDisposable? TryLinkTo(
        FlowInputPort input,
        bool propagateCompletion,
        out FlowApplicationRuntimeBuildError? error);
}

public sealed class FlowOutputPort<T> : FlowOutputPort
{
    public FlowOutputPort(FlowPortName name, ISourceBlock<T> source)
        : base(name, typeof(T))
    {
        Source = source;
    }

    public ISourceBlock<T> Source { get; }

    public override IDisposable? TryLinkTo(
        FlowInputPort input,
        bool propagateCompletion,
        out FlowApplicationRuntimeBuildError? error)
    {
        if (input is not FlowInputPort<T> typedInput)
        {
            error = new(
                FlowApplicationRuntimeBuildErrorCode.PortTypeMismatch,
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
                FlowApplicationRuntimeBuildErrorCode.LinkFailed,
                $"Failed to link '{Name}' to '{input.Name}': {exception.Message}",
                PortName: input.Name);
            return null;
        }
    }
}
