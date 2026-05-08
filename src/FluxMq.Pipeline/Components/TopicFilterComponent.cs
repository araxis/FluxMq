using FluxMq.Core.Models;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Pipeline.Components;

public sealed class TopicFilterComponent
{
    private readonly TransformManyBlock<MqttEnvelope, MqttEnvelope> _block;

    public TopicFilterComponent(Func<MqttEnvelope, bool> predicate, int boundedCapacity = 1000)
    {
        _block = new TransformManyBlock<MqttEnvelope, MqttEnvelope>(
            envelope => predicate(envelope) ? [envelope] : [],
            new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = boundedCapacity,
                EnsureOrdered = true
            });
    }

    public ITargetBlock<MqttEnvelope> Input => _block;
    public ISourceBlock<MqttEnvelope> Output => _block;

    public static TopicFilterComponent Prefix(string topicPrefix, StringComparison comparison = StringComparison.Ordinal)
        => new(envelope => envelope.Topic.StartsWith(topicPrefix, comparison));
}
