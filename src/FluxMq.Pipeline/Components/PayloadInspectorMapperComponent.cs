using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Core.Payloads;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Pipeline.Components;

public sealed class PayloadInspectorMapperComponent : IFlowNode
{
    private readonly TransformBlock<MqttEnvelope, InspectedMqttMessage> _block;

    public PayloadInspectorMapperComponent(FlowNodeId? id = null, int boundedCapacity = 1000)
    {
        Id = id ?? FlowNodeId.New();
        _block = new TransformBlock<MqttEnvelope, InspectedMqttMessage>(
            static envelope => new InspectedMqttMessage(envelope, PayloadInspector.Inspect(envelope.Payload)),
            new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = boundedCapacity,
                EnsureOrdered = true
            });
    }

    public FlowNodeId Id { get; }
    public Task Completion => _block.Completion;
    public ITargetBlock<MqttEnvelope> Input => _block;
    public ISourceBlock<InspectedMqttMessage> Output => _block;

    public void Complete() => _block.Complete();

    public void Fault(Exception exception) => ((IDataflowBlock)_block).Fault(exception);
}
