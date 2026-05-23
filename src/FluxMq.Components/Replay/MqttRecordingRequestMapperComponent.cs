using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Components.Mapping;
using FluxMq.Pipeline.Components;
using FluxMq.Pipeline.Mapping;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Components.Replay;

public sealed class MqttRecordingRequestMapperComponent : IFlowNode
{
    private readonly TransformManyBlock<MqttEnvelope, MqttRecordingRequest> _block;
    private readonly BroadcastBlock<FlowError> _errors;
    private readonly IFlowMapper<MqttEnvelope, MqttRecordingRequest> _mapper;

    public MqttRecordingRequestMapperComponent(
        SessionId sessionId,
        FlowNodeId? id = null,
        int boundedCapacity = 1000)
        : this(new StaticSessionRecordingRequestMapper(sessionId), id, boundedCapacity)
    {
    }

    public MqttRecordingRequestMapperComponent(
        IFlowMapper<MqttEnvelope, MqttRecordingRequest> mapper,
        FlowNodeId? id = null,
        int boundedCapacity = 1000)
    {
        Id = id ?? FlowNodeId.New();
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _errors = new BroadcastBlock<FlowError>(static error => error);
        _block = new TransformManyBlock<MqttEnvelope, MqttRecordingRequest>(
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

    private IEnumerable<MqttRecordingRequest> Map(MqttEnvelope envelope)
    {
        try
        {
            var context = MqttEnvelopeExpressionContextFactory.Create(envelope);
            return [_mapper.Map(envelope, context)];
        }
        catch (Exception exception)
        {
            PublishError(FlowErrorCodes.ProcessingFailed, "MQTT recording request mapping failed.", exception);
            return [];
        }
    }

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

    private sealed class StaticSessionRecordingRequestMapper(SessionId sessionId) : IFlowMapper<MqttEnvelope, MqttRecordingRequest>
    {
        public MqttRecordingRequest Map(MqttEnvelope input, FlowMapContext context)
            => new()
            {
                SessionId = sessionId,
                Envelope = input
            };
    }
}
