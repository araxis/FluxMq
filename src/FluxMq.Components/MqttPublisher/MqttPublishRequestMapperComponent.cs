using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Components.Mapping;
using FluxFlow.Engine.Components;
using FluxFlow.Engine.Mapping;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Components.MqttPublisher;

public sealed class MqttPublishRequestMapperComponent : IFlowNode
{
    private readonly TransformManyBlock<MqttEnvelope, MqttPublishRequest> _block;
    private readonly BroadcastBlock<FlowError> _errors;
    private readonly IFlowMapper<MqttEnvelope, MqttPublishRequest> _mapper;

    public MqttPublishRequestMapperComponent(
        IFlowMapper<MqttEnvelope, MqttPublishRequest> mapper,
        FlowNodeId? id = null,
        int boundedCapacity = 1000)
    {
        Id = id ?? FlowNodeId.New();
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _errors = new BroadcastBlock<FlowError>(static error => error);
        _block = new TransformManyBlock<MqttEnvelope, MqttPublishRequest>(
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
    public ISourceBlock<MqttPublishRequest> Output => _block;

    public void Complete() => _block.Complete();

    public void Fault(Exception exception)
    {
        PublishError(FlowErrorCodes.NodeFaulted, "MQTT publish request mapper faulted.", exception);
        ((IDataflowBlock)_block).Fault(exception);
    }

    private IEnumerable<MqttPublishRequest> Map(MqttEnvelope envelope)
    {
        try
        {
            var context = MqttEnvelopeExpressionContextFactory.Create(envelope);
            return [_mapper.Map(envelope, context)];
        }
        catch (Exception exception)
        {
            PublishError(FlowErrorCodes.ProcessingFailed, "MQTT publish request mapping failed.", exception, envelope.Topic);
            return [];
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
