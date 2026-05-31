using FluxMq.Components.FileWriter;
using FluxMq.Components.JsonSchema;
using FluxMq.Components.Logging;
using FluxMq.Components.MqttPayloadInspector;
using FluxMq.Components.MqttPublisher;
using FluxMq.Components.Replay;
using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxFlow.Engine.Components;
using FluxFlow.Engine.Mapping;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Components.Assertions;

public sealed class FlowAssertionComponent<TInput> : IFlowNode, IFlowEventSource
{
    private const string DefaultAssertionName = "Message assertion";
    private const string DefaultFailureMessage = "Assertion failed.";

    private readonly ActionBlock<TInput> _block;
    private readonly BufferBlock<FlowAssertionResult> _result;
    private readonly BufferBlock<TInput> _passed;
    private readonly BufferBlock<TInput> _failed;
    private readonly BufferBlock<FlowLogEntry> _entries;
    private readonly BroadcastBlock<FlowError> _errors;
    private readonly BufferBlock<FlowEvent> _events;
    private readonly IFlowPredicate<TInput> _predicate;
    private readonly string _assertionName;
    private readonly string _expression;
    private readonly string _failureMessage;

    public FlowAssertionComponent(
        IFlowPredicate<TInput> predicate,
        string assertionName,
        string expression,
        string? failureMessage = null,
        FlowNodeId? id = null,
        int boundedCapacity = 1000)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);

        Id = id ?? FlowNodeId.New();
        _predicate = predicate;
        _assertionName = string.IsNullOrWhiteSpace(assertionName) ? DefaultAssertionName : assertionName.Trim();
        _expression = expression.Trim();
        _failureMessage = string.IsNullOrWhiteSpace(failureMessage) ? DefaultFailureMessage : failureMessage.Trim();
        _errors = new BroadcastBlock<FlowError>(static error => error);
        _entries = new BufferBlock<FlowLogEntry>();
        _events = new BufferBlock<FlowEvent>();
        _result = new BufferBlock<FlowAssertionResult>(new DataflowBlockOptions
        {
            BoundedCapacity = boundedCapacity
        });
        _passed = new BufferBlock<TInput>(new DataflowBlockOptions
        {
            BoundedCapacity = boundedCapacity
        });
        _failed = new BufferBlock<TInput>(new DataflowBlockOptions
        {
            BoundedCapacity = boundedCapacity
        });
        _block = new ActionBlock<TInput>(
            EvaluateAndRouteAsync,
            new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = boundedCapacity,
                EnsureOrdered = true
            });

        _block.Completion.ContinueWith(
            CompleteOutputs,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    public FlowNodeId Id { get; }
    public ITargetBlock<TInput> Input => _block;
    public ISourceBlock<FlowAssertionResult> Result => _result;
    public ISourceBlock<TInput> Passed => _passed;
    public ISourceBlock<TInput> Failed => _failed;
    public ISourceBlock<FlowLogEntry> Entries => _entries;
    public ISourceBlock<FlowEvent> Events => _events;
    public ISourceBlock<FlowError> Errors => _errors;
    public Task Completion => _block.Completion;

    public void Complete() => _block.Complete();

    public void Fault(Exception exception)
    {
        PublishError(FlowErrorCodes.NodeFaulted, "Flow assertion faulted.", exception);
        ((IDataflowBlock)_block).Fault(exception);
    }

    private async Task EvaluateAndRouteAsync(TInput value)
    {
        bool passed;
        try
        {
            passed = _predicate.IsMatch(value);
        }
        catch (Exception exception)
        {
            PublishError(FlowErrorCodes.ProcessingFailed, "Flow assertion expression failed.", exception, GetContext(value));
            return;
        }

        var result = new FlowAssertionResult
        {
            AssertionName = _assertionName,
            Expression = _expression,
            InputType = typeof(TInput).Name,
            Passed = passed,
            Value = value,
            Message = passed ? "Assertion passed." : _failureMessage
        };

        await _result.SendAsync(result).ConfigureAwait(false);
        await (passed ? _passed : _failed).SendAsync(value).ConfigureAwait(false);
        PublishEntry(value, passed);
        PublishEvent(value, result);
    }

    private void CompleteOutputs(Task completion)
    {
        if (completion.IsFaulted && completion.Exception is { } exception)
        {
            ((IDataflowBlock)_result).Fault(exception);
            ((IDataflowBlock)_passed).Fault(exception);
            ((IDataflowBlock)_failed).Fault(exception);
            _errors.Complete();
            _entries.Complete();
            _events.Complete();
            return;
        }

        _result.Complete();
        _passed.Complete();
        _failed.Complete();
        _errors.Complete();
        _entries.Complete();
        _events.Complete();
    }

    private void PublishError(int code, string message, Exception exception, string? context = null)
    {
        _errors.Post(new FlowError
        {
            NodeId = Id,
            Code = code,
            Message = message,
            Exception = exception,
            Context = context
        });
    }

    private void PublishEntry(TInput value, bool passed)
    {
        var (topic, payloadBytes) = GetLogShape(value);
        _entries.Post(new FlowLogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            Severity = passed ? FlowLogSeverity.Info : FlowLogSeverity.Warning,
            Source = "FlowAssertion",
            Message = passed
                ? $"Assertion passed: {_assertionName}."
                : $"Assertion failed: {_assertionName}.",
            RelatedNodeId = Id,
            Topic = topic,
            PayloadBytes = payloadBytes,
            Context = $"passed={passed}; inputType={typeof(TInput).Name}; expression={_expression}"
        });
    }

    private void PublishEvent(TInput value, FlowAssertionResult result)
    {
        var (topic, payloadBytes) = GetLogShape(value);
        _events.Post(new FlowEvent
        {
            Timestamp = DateTimeOffset.UtcNow,
            Type = FluxMqEventTypes.AssertionEvaluated,
            Source = "FlowAssertion",
            SourceNodeId = Id,
            Subject = _assertionName,
            Status = result.Passed ? "passed" : "failed",
            Channel = topic,
            PayloadBytes = payloadBytes,
            Attributes = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["assertionName"] = _assertionName,
                ["inputType"] = typeof(TInput).Name,
                ["passed"] = result.Passed.ToString()
            }
        });
    }

    private static string? GetContext(TInput value)
        => value switch
        {
            MqttEnvelope envelope => envelope.Topic,
            MqttPublishRequest request => request.Topic,
            MqttRecordingRequest request => request.Envelope.Topic,
            InspectedMqttMessage inspected => inspected.Envelope.Topic,
            JsonSchemaValidationResult validation => validation.Envelope.Topic,
            FileWriteRequest request => request.Path,
            _ => typeof(TInput).Name
        };

    private static (string? Topic, int? PayloadBytes) GetLogShape(TInput value)
        => value switch
        {
            MqttEnvelope envelope => (envelope.Topic, envelope.Payload.Length),
            MqttPublishRequest request => (request.Topic, request.Payload.Length),
            MqttRecordingRequest request => (request.Envelope.Topic, request.Envelope.Payload.Length),
            InspectedMqttMessage inspected => (inspected.Envelope.Topic, inspected.Envelope.Payload.Length),
            JsonSchemaValidationResult validation => (validation.Envelope.Topic, validation.Envelope.Payload.Length),
            FileWriteRequest request => (null, request.Content.Length),
            _ => (null, null)
        };
}
