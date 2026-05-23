using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Pipeline.Components;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Components.Replay;

public sealed class MqttRecordingRequestMapperComponent : IFlowNode
{
    private readonly SessionId _sessionId;
    private readonly TransformBlock<MqttEnvelope, MqttRecordingRequest> _block;
    private readonly BroadcastBlock<FlowError> _errors;

    public MqttRecordingRequestMapperComponent(
        SessionId sessionId,
        FlowNodeId? id = null,
        int boundedCapacity = 1000)
    {
        Id = id ?? FlowNodeId.New();
        _sessionId = sessionId;
        _errors = new BroadcastBlock<FlowError>(static error => error);
        _block = new TransformBlock<MqttEnvelope, MqttRecordingRequest>(
            Map,
            new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = boundedCapacity,
                EnsureOrdered = true
            });

        _block.Completion.ContinueWith(
            _ => _errors.Complete(),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    public FlowNodeId Id { get; }
    public ISourceBlock<FlowError> Errors => _errors;
    public Task Completion => _block.Completion;
    public ITargetBlock<MqttEnvelope> Input => _block;
    public ISourceBlock<MqttRecordingRequest> Output => _block;

    public void Complete() => _block.Complete();

    public void Fault(Exception exception)
    {
        PublishError(FlowErrorCodes.NodeFaulted, "MQTT recording request mapper faulted.", exception);
        ((IDataflowBlock)_block).Fault(exception);
    }

    private MqttRecordingRequest Map(MqttEnvelope envelope) => new()
    {
        SessionId = _sessionId,
        Envelope = envelope
    };

    private void PublishError(int code, string message, Exception exception)
    {
        _errors.Post(new FlowError
        {
            NodeId = Id,
            Code = code,
            Message = message,
            Exception = exception
        });
    }
}
