using FluxMq.Core.Ids;
using FluxMq.Components.Storage;
using FluxMq.Components.Storage.Repositories;
using FluxFlow.Components.Sessions.Contracts;
using FluxFlow.Engine.Components;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Components.Replay;

public sealed class MqttRecorderComponent : IFlowNode, IFlowEventSource
{
    private readonly ISessionStore _sessions;
    private readonly ActionBlock<MqttRecordingRequest> _block;
    private readonly BroadcastBlock<FlowError> _errors;
    private readonly BufferBlock<FlowEvent> _events;

    public MqttRecorderComponent(
        IMessageRepository messages,
        FlowNodeId? id = null,
        int boundedCapacity = 1000,
        int maxDegreeOfParallelism = 1)
        : this(new FluxMqSessionStore(messages), id, boundedCapacity, maxDegreeOfParallelism)
    {
    }

    public MqttRecorderComponent(
        ISessionStore sessions,
        FlowNodeId? id = null,
        int boundedCapacity = 1000,
        int maxDegreeOfParallelism = 1)
    {
        if (maxDegreeOfParallelism <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism), maxDegreeOfParallelism, "Degree of parallelism must be positive.");
        }

        Id = id ?? FlowNodeId.New();
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _errors = new BroadcastBlock<FlowError>(static error => error);
        _events = new BufferBlock<FlowEvent>();
        _block = new ActionBlock<MqttRecordingRequest>(
            RecordAsync,
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
                _events.Complete();
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    public FlowNodeId Id { get; }
    public ISourceBlock<FlowError> Errors => _errors;
    public ISourceBlock<FlowEvent> Events => _events;
    public Task Completion => _block.Completion;
    public ITargetBlock<MqttRecordingRequest> Input => _block;

    public void Complete() => _block.Complete();

    public void Fault(Exception exception)
    {
        PublishError(FlowErrorCodes.NodeFaulted, "MQTT recorder faulted.", exception);
        ((IDataflowBlock)_block).Fault(exception);
    }

    private async Task RecordAsync(MqttRecordingRequest request)
    {
        try
        {
            var record = await _sessions.AppendMessageAsync(
                new SessionAppendRequest
                {
                    Session = new SessionMetadata
                    {
                        SessionId = request.SessionId.ToString(),
                        StartedAt = request.Envelope.ReceivedAt
                    },
                    Input = FluxMqSessionStore.ToRecordInput(request.Envelope),
                    Sequence = 0,
                    Timestamp = request.Envelope.ReceivedAt
                }).ConfigureAwait(false);

            _events.Post(new FlowEvent
            {
                Timestamp = DateTimeOffset.UtcNow,
                Type = FluxMqEventTypes.MqttMessageRecorded,
                Source = "MqttRecorder",
                SourceNodeId = Id,
                Subject = request.Envelope.Topic,
                Status = "recorded",
                Channel = request.Envelope.Topic,
                PayloadBytes = request.Envelope.Payload.Length,
                PayloadPreview = FlowEventPayloadPreview.FromBytes(request.Envelope.Payload),
                Attributes = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["sessionId"] = record.SessionId,
                    ["qos"] = ((int)request.Envelope.QualityOfService).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["retain"] = request.Envelope.Retain.ToString()
                }
            });
        }
        catch (Exception exception)
        {
            PublishError(FlowErrorCodes.ProcessingFailed, "MQTT recording failed.", exception, request.Envelope.Topic);
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
