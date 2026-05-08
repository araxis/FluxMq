using FluxMq.Core.Models;
using FluxMq.Core.Payloads;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Pipeline.Components;

public sealed class PayloadInspectorMapperComponent
{
    private readonly TransformBlock<MqttEnvelope, InspectedMqttMessage> _block;

    public PayloadInspectorMapperComponent(int boundedCapacity = 1000)
    {
        _block = new TransformBlock<MqttEnvelope, InspectedMqttMessage>(
            static envelope => new InspectedMqttMessage(envelope, PayloadInspector.Inspect(envelope.Payload)),
            new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = boundedCapacity,
                EnsureOrdered = true
            });
    }

    public ITargetBlock<MqttEnvelope> Input => _block;
    public ISourceBlock<InspectedMqttMessage> Output => _block;
}
