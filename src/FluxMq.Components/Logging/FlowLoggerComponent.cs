using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxFlow.Engine.Components;
using System.Text;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Components.Logging;

public sealed class FlowLoggerComponent : IFlowNode
{
    private readonly Lock _sync = new();
    private readonly Queue<FlowLogEntry> _recentEntries = new();
    private readonly ActionBlock<MqttEnvelope> _input;
    private readonly ActionBlock<FlowError> _flowErrors;
    private readonly BufferBlock<FlowLogEntry> _entries;
    private readonly BroadcastBlock<FlowError> _errors;
    private readonly Task _completion;
    private readonly bool _includePayloadPreview;
    private readonly int _maxEntries;
    private readonly int _maxPayloadPreviewChars;

    public FlowLoggerComponent(
        FlowNodeId? id = null,
        int boundedCapacity = 1000,
        int maxEntries = 500,
        bool includePayloadPreview = false,
        int maxPayloadPreviewChars = 512,
        bool waitForMessageInputCompletion = true,
        bool waitForFlowErrorInputCompletion = true)
    {
        if (boundedCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(boundedCapacity), boundedCapacity, "Bounded capacity must be positive.");
        }

        if (maxEntries <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxEntries), maxEntries, "Maximum entries must be positive.");
        }

        if (maxPayloadPreviewChars <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxPayloadPreviewChars), maxPayloadPreviewChars, "Payload preview length must be positive.");
        }

        Id = id ?? FlowNodeId.New();
        _maxEntries = maxEntries;
        _includePayloadPreview = includePayloadPreview;
        _maxPayloadPreviewChars = maxPayloadPreviewChars;
        _entries = new BufferBlock<FlowLogEntry>();
        _errors = new BroadcastBlock<FlowError>(static error => error);
        _input = new ActionBlock<MqttEnvelope>(
            ObserveMessage,
            new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = boundedCapacity,
                EnsureOrdered = true,
                MaxDegreeOfParallelism = 1
            });
        _flowErrors = new ActionBlock<FlowError>(
            ObserveFlowError,
            new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = boundedCapacity,
                EnsureOrdered = true,
                MaxDegreeOfParallelism = 1
            });

        var completions = new List<Task>(2);
        if (waitForMessageInputCompletion)
        {
            completions.Add(_input.Completion);
        }

        if (waitForFlowErrorInputCompletion)
        {
            completions.Add(_flowErrors.Completion);
        }

        _completion = completions.Count == 0 ? Task.CompletedTask : Task.WhenAll(completions);
        _completion.ContinueWith(
            _ =>
            {
                _entries.Complete();
                _errors.Complete();
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    public FlowNodeId Id { get; }
    public ISourceBlock<FlowError> Errors => _errors;
    public Task Completion => _completion;
    public ITargetBlock<MqttEnvelope> Input => _input;
    public ITargetBlock<FlowError> FlowErrors => _flowErrors;
    public ISourceBlock<FlowLogEntry> Entries => _entries;

    public IReadOnlyList<FlowLogEntry> RecentEntries
    {
        get
        {
            lock (_sync)
            {
                return _recentEntries.ToArray();
            }
        }
    }

    public void Complete()
    {
        _input.Complete();
        _flowErrors.Complete();
    }

    public void Fault(Exception exception)
    {
        PublishError(FlowErrorCodes.NodeFaulted, "Flow logger faulted.", exception);
        ((IDataflowBlock)_input).Fault(exception);
        ((IDataflowBlock)_flowErrors).Fault(exception);
    }

    private void ObserveMessage(MqttEnvelope envelope)
    {
        try
        {
            PublishEntry(new FlowLogEntry
            {
                Timestamp = DateTimeOffset.UtcNow,
                Severity = FlowLogSeverity.Info,
                Source = "MqttEnvelope",
                Message = $"Received MQTT message on '{envelope.Topic}'.",
                Topic = envelope.Topic,
                PayloadBytes = envelope.Payload.Length,
                PayloadPreview = BuildPayloadPreview(envelope.Payload),
                Context = $"qos={(int)envelope.QualityOfService}; retain={envelope.Retain}"
            });
        }
        catch (Exception exception)
        {
            PublishError(FlowErrorCodes.ProcessingFailed, "Flow logger message capture failed.", exception, envelope.Topic);
        }
    }

    private void ObserveFlowError(FlowError error)
    {
        try
        {
            PublishEntry(new FlowLogEntry
            {
                Timestamp = error.OccurredAt,
                Severity = FlowLogSeverity.Error,
                Source = "FlowError",
                Message = error.Message,
                RelatedNodeId = error.NodeId,
                ErrorCode = error.Code,
                Context = error.Context
            });
        }
        catch (Exception exception)
        {
            PublishError(FlowErrorCodes.ProcessingFailed, "Flow logger error capture failed.", exception, error.Context);
        }
    }

    private string? BuildPayloadPreview(byte[] payload)
    {
        if (!_includePayloadPreview || payload.Length == 0)
        {
            return null;
        }

        var text = Encoding.UTF8.GetString(payload);
        text = text.Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);

        return text.Length <= _maxPayloadPreviewChars
            ? text
            : text[.._maxPayloadPreviewChars] + "...";
    }

    private void PublishEntry(FlowLogEntry entry)
    {
        lock (_sync)
        {
            _recentEntries.Enqueue(entry);
            while (_recentEntries.Count > _maxEntries)
            {
                _recentEntries.Dequeue();
            }
        }

        _entries.Post(entry);
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
}
