using FluxMq.Core.Ids;
using FluxMq.Core.Mqtt;
using FluxMq.Components.Logging;
using FluxFlow.Engine.Components;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Components.MqttPublisher;

public sealed class MqttPublisherComponent : IFlowNode, IFlowEventSource
{
    private readonly IMqttBrokerClient _client;
    private readonly ActionBlock<MqttPublishRequest> _block;
    private readonly BroadcastBlock<FlowError> _errors;
    private readonly BufferBlock<FlowLogEntry> _entries;
    private readonly BufferBlock<FlowEvent> _events;
    private int _publishedCount;
    private string? _lastPublishedTopic;

    public MqttPublisherComponent(
        IMqttBrokerClient client,
        FlowNodeId? id = null,
        int boundedCapacity = 1000,
        int maxDegreeOfParallelism = 1)
    {
        if (maxDegreeOfParallelism <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism), maxDegreeOfParallelism, "Degree of parallelism must be positive.");
        }

        Id = id ?? FlowNodeId.New();
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _errors = new BroadcastBlock<FlowError>(static error => error);
        _entries = new BufferBlock<FlowLogEntry>();
        _events = new BufferBlock<FlowEvent>();
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
                _events.Complete();
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    public FlowNodeId Id { get; }
    public ISourceBlock<FlowError> Errors => _errors;
    public ISourceBlock<FlowLogEntry> Entries => _entries;
    public ISourceBlock<FlowEvent> Events => _events;
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
            await _client.PublishAsync(
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
            _events.Post(new FlowEvent
            {
                Timestamp = DateTimeOffset.UtcNow,
                Type = FluxMqEventTypes.MqttMessagePublished,
                Source = "MqttPublisher",
                SourceNodeId = Id,
                Subject = request.Topic,
                Status = "published",
                Channel = request.Topic,
                PayloadBytes = request.Payload.Length,
                PayloadPreview = FlowEventPayloadPreview.FromBytes(request.Payload),
                Attributes = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["qos"] = ((int)request.QualityOfService).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["retain"] = request.Retain.ToString()
                }
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
