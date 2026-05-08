using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Pipeline.Components;

public sealed class TopicFilterComponent : IFlowNode
{
    private readonly TransformManyBlock<MqttEnvelope, MqttEnvelope> _block;

    public TopicFilterComponent(Func<MqttEnvelope, bool> predicate, FlowNodeId? id = null, int boundedCapacity = 1000)
    {
        Id = id ?? FlowNodeId.New();
        _block = new TransformManyBlock<MqttEnvelope, MqttEnvelope>(
            envelope => predicate(envelope) ? [envelope] : [],
            new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = boundedCapacity,
                EnsureOrdered = true
            });
    }

    public FlowNodeId Id { get; }
    public Task Completion => _block.Completion;
    public ITargetBlock<MqttEnvelope> Input => _block;
    public ISourceBlock<MqttEnvelope> Output => _block;

    public static TopicFilterComponent Prefix(string topicPrefix, StringComparison comparison = StringComparison.Ordinal)
        => new(envelope => envelope.Topic.StartsWith(topicPrefix, comparison));

    public void Complete() => _block.Complete();

    public void Fault(Exception exception) => ((IDataflowBlock)_block).Fault(exception);
}
