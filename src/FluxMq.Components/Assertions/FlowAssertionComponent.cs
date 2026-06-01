using FluxMq.Components.Control;
using FluxMq.Components.Logging;
using FluxFlow.Components.Control.Contracts;
using FluxFlow.Components.Control.Nodes;
using FluxFlow.Components.Control.Options;
using FluxFlow.Engine.Components;
using FluxFlow.Engine.Mapping;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Components.Assertions;

public sealed class FlowAssertionComponent<TInput> : IFlowNode, IFlowEventSource
{
    private readonly AssertNode<TInput> _node;
    private readonly TransformBlock<ControlAssertionResult, FlowAssertionResult> _result;
    private readonly BufferBlock<FlowLogEntry> _entries = new();
    private readonly BufferBlock<FlowEvent> _events = new();

    public FlowAssertionComponent(
        ControlExpressionOptions options,
        IFlowExpressionEngine expressionEngine,
        IControlContextFactory contextFactory,
        ControlNodeContext nodeContext)
    {
        _node = new AssertNode<TInput>(
            options,
            expressionEngine,
            contextFactory,
            nodeContext);
        _result = new TransformBlock<ControlAssertionResult, FlowAssertionResult>(
            PublishResult,
            new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = options.BoundedCapacity,
                EnsureOrdered = true
            });

        _node.Result.LinkTo(_result, new DataflowLinkOptions { PropagateCompletion = true });
        _result.Completion.ContinueWith(
            CompleteTelemetry,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    public FlowNodeId Id => _node.Id;
    public ITargetBlock<TInput> Input => _node.Input;
    public ISourceBlock<FlowAssertionResult> Result => _result;
    public ISourceBlock<TInput> Passed => _node.Passed;
    public ISourceBlock<TInput> Failed => _node.Failed;
    public ISourceBlock<FlowLogEntry> Entries => _entries;
    public ISourceBlock<FlowEvent> Events => _events;
    public ISourceBlock<FlowError> Errors => _node.Errors;
    public Task Completion => Task.WhenAll(_node.Completion, _result.Completion);

    public void Complete()
        => _node.Complete();

    public void Fault(Exception exception)
        => _node.Fault(exception);

    private FlowAssertionResult PublishResult(ControlAssertionResult packageResult)
    {
        var result = new FlowAssertionResult
        {
            AssertionName = packageResult.Name,
            Expression = packageResult.Expression,
            InputType = packageResult.InputType,
            Passed = packageResult.Passed,
            Value = packageResult.Value,
            Message = packageResult.Message,
            EvaluatedAt = packageResult.EvaluatedAt
        };

        PublishEntry(result);
        PublishEvent(result);
        return result;
    }

    private void PublishEntry(FlowAssertionResult result)
    {
        var value = result.Value is TInput typed ? typed : default;
        var (topic, payloadBytes) = value is null
            ? (null, null)
            : FlowControlLogShape.Shape(value);
        _entries.Post(new FlowLogEntry
        {
            Timestamp = result.EvaluatedAt,
            Severity = result.Passed ? FlowLogSeverity.Info : FlowLogSeverity.Warning,
            Source = "FlowAssertion",
            Message = result.Passed
                ? $"Assertion passed: {result.AssertionName}."
                : $"Assertion failed: {result.AssertionName}.",
            RelatedNodeId = Id,
            Topic = topic,
            PayloadBytes = payloadBytes,
            Context = $"passed={result.Passed}; inputType={result.InputType}; expression={result.Expression}"
        });
    }

    private void PublishEvent(FlowAssertionResult result)
    {
        var value = result.Value is TInput typed ? typed : default;
        var (topic, payloadBytes) = value is null
            ? (null, null)
            : FlowControlLogShape.Shape(value);
        _events.Post(new FlowEvent
        {
            Timestamp = result.EvaluatedAt,
            Type = FluxMqEventTypes.AssertionEvaluated,
            Source = "FlowAssertion",
            SourceNodeId = Id,
            Subject = result.AssertionName,
            Status = result.Passed ? "passed" : "failed",
            Channel = topic,
            PayloadBytes = payloadBytes,
            Attributes = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["assertionName"] = result.AssertionName,
                ["inputType"] = result.InputType,
                ["passed"] = result.Passed.ToString()
            }
        });
    }

    private void CompleteTelemetry(Task completion)
    {
        if (completion.IsFaulted && completion.Exception is { } exception)
        {
            ((IDataflowBlock)_entries).Fault(exception);
            ((IDataflowBlock)_events).Fault(exception);
            return;
        }

        _entries.Complete();
        _events.Complete();
    }
}
