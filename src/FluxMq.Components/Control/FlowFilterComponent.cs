using FluxFlow.Components.Control.Contracts;
using FluxFlow.Components.Control.Nodes;
using FluxFlow.Components.Control.Options;
using FluxFlow.Engine.Components;
using FluxFlow.Engine.Mapping;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Components.Control;

public sealed class FlowFilterComponent<TInput> : IFlowNode
{
    private readonly FilterNode<TInput> _node;
    private readonly TransformBlock<TInput, TInput> _output;
    private long _passedCount;

    public FlowFilterComponent(
        ControlExpressionOptions options,
        IFlowExpressionEngine expressionEngine,
        IControlContextFactory contextFactory,
        ControlNodeContext nodeContext)
    {
        _node = new FilterNode<TInput>(
            options,
            expressionEngine,
            contextFactory,
            nodeContext);
        _output = new TransformBlock<TInput, TInput>(
            value =>
            {
                Interlocked.Increment(ref _passedCount);
                return value;
            },
            new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = options.BoundedCapacity,
                EnsureOrdered = true
            });
        _node.Output.LinkTo(_output, new DataflowLinkOptions { PropagateCompletion = true });
    }

    public FlowNodeId Id => _node.Id;
    public ISourceBlock<FlowError> Errors => _node.Errors;
    public Task Completion => Task.WhenAll(_node.Completion, _output.Completion);
    public ITargetBlock<TInput> Input => _node.Input;
    public ISourceBlock<TInput> Output => _output;
    public long PassedCount => Interlocked.Read(ref _passedCount);

    public void Complete()
        => _node.Complete();

    public void Fault(Exception exception)
        => _node.Fault(exception);
}
