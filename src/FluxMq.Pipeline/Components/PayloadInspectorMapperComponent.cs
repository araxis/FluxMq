using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Core.Payloads;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Pipeline.Components;

public sealed class PayloadInspectorMapperComponent : IFlowNode
{
    private readonly BroadcastBlock<FlowError> _errors;
    private readonly TransformBlock<MqttEnvelope, InspectedMqttMessage> _block;

    public PayloadInspectorMapperComponent(FlowNodeId? id = null, int boundedCapacity = 1000)
    {
        Id = id ?? FlowNodeId.New();
        _errors = new BroadcastBlock<FlowError>(static error => error);
        _block = new TransformBlock<MqttEnvelope, InspectedMqttMessage>(
            Inspect,
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
    public ISourceBlock<InspectedMqttMessage> Output => _block;

    public void Complete()
    {
        _block.Complete();
    }

    public void Fault(Exception exception)
    {
        PublishError("Payload inspector mapper faulted.", exception);
        ((IDataflowBlock)_block).Fault(exception);
    }

    private InspectedMqttMessage Inspect(MqttEnvelope envelope)
    {
        try
        {
            return new InspectedMqttMessage(envelope, PayloadInspector.Inspect(envelope.Payload));
        }
        catch (Exception exception)
        {
            PublishError("Payload inspection failed.", exception, envelope.Topic);
            return new InspectedMqttMessage(envelope, PayloadInspector.Inspect([]));
        }
    }

    private void PublishError(string message, Exception exception, string? context = null)
    {
        _errors.Post(new FlowError
        {
            NodeId = Id,
            Message = message,
            Exception = exception,
            Context = context
        });
    }
}
