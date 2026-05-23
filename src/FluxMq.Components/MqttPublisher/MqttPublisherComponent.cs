using FluxMq.Core.Ids;
using FluxMq.Core.Session;
using FluxMq.Components.Logging;
using FluxMq.Pipeline.Components;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Components.MqttPublisher;

public sealed class MqttPublisherComponent : IFlowNode
{
    private readonly IMqttSession _session;
    private readonly ActionBlock<MqttPublishRequest> _block;
    private readonly BroadcastBlock<FlowError> _errors;
    private readonly BroadcastBlock<FlowLogEntry> _entries;
    private int _publishedCount;
    private string? _lastPublishedTopic;

    public MqttPublisherComponent(
        IMqttSession session,
        FlowNodeId? id = null,
        int boundedCapacity = 1000,
        int maxDegreeOfParallelism = 1)
    {
        if (maxDegreeOfParallelism <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism), maxDegreeOfParallelism, "Degree of parallelism must be positive.");
        }

        Id = id ?? FlowNodeId.New();
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _errors = new BroadcastBlock<FlowError>(static error => error);
        _entries = new BroadcastBlock<FlowLogEntry>(static entry => entry);
        _block = new ActionBlock<MqttPublishRequest>(
            PublishAsync,
            new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = boundedCapacity,
                EnsureOrdered = true,
                MaxDegreeOfParallelism = maxDegreeOfParallelism
            });

        _block.Completion.ContinueWith(
            _ =>
            {
                _errors.Complete();
                _entries.Complete();
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    public FlowNodeId Id { get; }
    public ISourceBlock<FlowError> Errors => _errors;
    public ISourceBlock<FlowLogEntry> Entries => _entries;
    public Task Completion => _block.Completion;
    public ITargetBlock<MqttPublishRequest> Input => _block;
    public int PublishedCount => Volatile.Read(ref _publishedCount);
    public string? LastPublishedTopic => _lastPublishedTopic;

    public void Complete() => _block.Complete();

    public void Fault(Exception exception)
    {
        PublishError(FlowErrorCodes.NodeFaulted, "MQTT publisher faulted.", exception);
        ((IDataflowBlock)_block).Fault(exception);
    }

    private async Task PublishAsync(MqttPublishRequest request)
    {
        try
        {
            await _session.PublishAsync(
                request.Topic,
                request.Payload,
                request.QualityOfService,
                request.Retain).ConfigureAwait(false);

            Interlocked.Increment(ref _publishedCount);
            _lastPublishedTopic = request.Topic;
            _entries.Post(new FlowLogEntry
            {
                Timestamp = DateTimeOffset.UtcNow,
                Severity = FlowLogSeverity.Info,
                Source = "MqttPublisher",
                Message = $"Published MQTT message to '{request.Topic}'.",
                RelatedNodeId = Id,
                Topic = request.Topic,
                PayloadBytes = request.Payload.Length,
                Context = $"qos={(int)request.QualityOfService}; retain={request.Retain}"
            });
        }
        catch (Exception exception)
        {
            PublishError(FlowErrorCodes.ProcessingFailed, "MQTT publish failed.", exception, request.Topic);
        }
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
