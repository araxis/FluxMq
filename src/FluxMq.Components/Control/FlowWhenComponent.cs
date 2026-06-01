using FluxMq.Components.Logging;
using FluxFlow.Components.Control;
using FluxFlow.Components.Control.Contracts;
using FluxFlow.Components.Control.Nodes;
using FluxFlow.Components.Control.Options;
using FluxFlow.Engine.Components;
using FluxFlow.Engine.Mapping;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Components.Control;

public sealed class FlowWhenComponent<TInput> : IFlowNode
{
    private readonly WhenNode<TInput> _node;
    private readonly TransformBlock<TInput, TInput> _whenTrue;
    private readonly TransformBlock<TInput, TInput> _whenFalse;
    private readonly BufferBlock<FlowLogEntry> _entries = new();

    public FlowWhenComponent(
        ControlExpressionOptions options,
        IFlowExpressionEngine expressionEngine,
        IControlContextFactory contextFactory,
        ControlNodeContext nodeContext)
    {
        _node = new WhenNode<TInput>(
            options,
            expressionEngine,
            contextFactory,
            nodeContext);
        _whenTrue = CreateOutput(options.BoundedCapacity, ControlComponentPorts.WhenTrue, matched: true);
        _whenFalse = CreateOutput(options.BoundedCapacity, ControlComponentPorts.WhenFalse, matched: false);

        _node.WhenTrue.LinkTo(_whenTrue, new DataflowLinkOptions { PropagateCompletion = true });
        _node.WhenFalse.LinkTo(_whenFalse, new DataflowLinkOptions { PropagateCompletion = true });
        Task.WhenAll(_whenTrue.Completion, _whenFalse.Completion).ContinueWith(
            CompleteEntries,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    public FlowNodeId Id => _node.Id;
    public ISourceBlock<FlowError> Errors => _node.Errors;
    public Task Completion => Task.WhenAll(_node.Completion, _whenTrue.Completion, _whenFalse.Completion);
    public ITargetBlock<TInput> Input => _node.Input;
    public ISourceBlock<TInput> WhenTrue => _whenTrue;
    public ISourceBlock<TInput> WhenFalse => _whenFalse;
    public ISourceBlock<FlowLogEntry> Entries => _entries;

    public void Complete()
        => _node.Complete();

    public void Fault(Exception exception)
        => _node.Fault(exception);

    private TransformBlock<TInput, TInput> CreateOutput(int boundedCapacity, string route, bool matched)
        => new(
            value =>
            {
                PublishEntry(value, route, matched);
                return value;
            },
            new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = boundedCapacity,
                EnsureOrdered = true
            });

    private void PublishEntry(TInput value, string route, bool matched)
    {
        var (topic, payloadBytes) = FlowControlLogShape.Shape(value);
        _entries.Post(new FlowLogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            Severity = FlowLogSeverity.Info,
            Source = "FlowWhen",
            Message = $"Routed input to {route}.",
            RelatedNodeId = Id,
            Topic = topic,
            PayloadBytes = payloadBytes,
            Context = FlowControlLogShape.Context(value, $"matched={matched}")
        });
    }

    private void CompleteEntries(Task completion)
    {
        if (completion.IsFaulted && completion.Exception is { } exception)
        {
            ((IDataflowBlock)_entries).Fault(exception);
            return;
        }

        _entries.Complete();
    }
}
