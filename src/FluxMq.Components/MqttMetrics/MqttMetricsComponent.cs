using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Pipeline.Components;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Components.MqttMetrics;

public sealed class MqttMetricsComponent : IFlowNode
{
    private readonly Lock _sync = new();
    private readonly HashSet<string> _topics = [];
    private readonly ActionBlock<MqttEnvelope> _block;
    private readonly BroadcastBlock<FlowError> _errors;
    private readonly BroadcastBlock<MqttMetricsSnapshot> _snapshots;
    private long _messageCount;
    private long _totalPayloadBytes;
    private long _minPayloadBytes;
    private long _maxPayloadBytes;
    private long _retainedMessageCount;
    private string? _lastTopic;
    private DateTimeOffset? _lastReceivedAt;

    public MqttMetricsComponent(
        FlowNodeId? id = null,
        int boundedCapacity = 1000)
    {
        Id = id ?? FlowNodeId.New();
        _errors = new BroadcastBlock<FlowError>(static error => error);
        _snapshots = new BroadcastBlock<MqttMetricsSnapshot>(static snapshot => snapshot);
        _block = new ActionBlock<MqttEnvelope>(
            Observe,
            new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = boundedCapacity,
                EnsureOrdered = true,
                MaxDegreeOfParallelism = 1
            });

        _block.Completion.ContinueWith(
            _ =>
            {
                _snapshots.Complete();
                _errors.Complete();
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    public FlowNodeId Id { get; }
    public ISourceBlock<FlowError> Errors => _errors;
    public Task Completion => _block.Completion;
    public ITargetBlock<MqttEnvelope> Input => _block;
    public ISourceBlock<MqttMetricsSnapshot> Snapshots => _snapshots;
    public MqttMetricsSnapshot Current
    {
        get
        {
            lock (_sync)
            {
                return CreateSnapshot();
            }
        }
    }

    public void Complete() => _block.Complete();

    public void Fault(Exception exception)
    {
        PublishError(FlowErrorCodes.NodeFaulted, "MQTT metrics observer faulted.", exception);
        ((IDataflowBlock)_block).Fault(exception);
    }

    private void Observe(MqttEnvelope envelope)
    {
        try
        {
            var payloadLength = envelope.Payload.Length;
            MqttMetricsSnapshot snapshot;

            lock (_sync)
            {
                _messageCount++;
                _totalPayloadBytes += payloadLength;
                _minPayloadBytes = _messageCount == 1 ? payloadLength : Math.Min(_minPayloadBytes, payloadLength);
                _maxPayloadBytes = Math.Max(_maxPayloadBytes, payloadLength);
                _lastTopic = envelope.Topic;
                _lastReceivedAt = envelope.ReceivedAt;
                _topics.Add(envelope.Topic);

                if (envelope.Retain)
                {
                    _retainedMessageCount++;
                }

                snapshot = CreateSnapshot();
            }

            _snapshots.Post(snapshot);
        }
        catch (Exception exception)
        {
            PublishError(FlowErrorCodes.ProcessingFailed, "MQTT metrics update failed.", exception, envelope.Topic);
        }
    }

    private MqttMetricsSnapshot CreateSnapshot() => new()
    {
        MessageCount = _messageCount,
        TotalPayloadBytes = _totalPayloadBytes,
        MinPayloadBytes = _messageCount == 0 ? 0 : _minPayloadBytes,
        MaxPayloadBytes = _maxPayloadBytes,
        RetainedMessageCount = _retainedMessageCount,
        UniqueTopicCount = _topics.Count,
        LastTopic = _lastTopic,
        LastReceivedAt = _lastReceivedAt
    };

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
