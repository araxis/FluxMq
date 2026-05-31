using FluxMq.Core.Ids;
using FluxMq.Components.Storage.Repositories;
using FluxFlow.Engine.Components;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Components.Replay;

public sealed class MqttRecorderComponent : IFlowNode, IFlowEventSource
{
    private readonly IMessageRepository _messages;
    private readonly ActionBlock<MqttRecordingRequest> _block;
    private readonly BroadcastBlock<FlowError> _errors;
    private readonly BufferBlock<FlowEvent> _events;

    public MqttRecorderComponent(
        IMessageRepository messages,
        FlowNodeId? id = null,
        int boundedCapacity = 1000,
        int maxDegreeOfParallelism = 1)
    {
        if (maxDegreeOfParallelism <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism), maxDegreeOfParallelism, "Degree of parallelism must be positive.");
        }

        Id = id ?? FlowNodeId.New();
        _messages = messages ?? throw new ArgumentNullException(nameof(messages));
        _errors = new BroadcastBlock<FlowError>(static error => error);
        _events = new BufferBlock<FlowEvent>();
        _block = new ActionBlock<MqttRecordingRequest>(
            Record,
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

    private void Record(MqttRecordingRequest request)
    {
        try
        {
            _messages.Add(request.SessionId, request.Envelope);
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
                    ["sessionId"] = request.SessionId.ToString(),
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
