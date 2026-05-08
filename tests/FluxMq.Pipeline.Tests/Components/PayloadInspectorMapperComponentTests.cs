using System.Text;
using FluentAssertions;
using FluxMq.Core.Models;
using FluxMq.Core.Payloads;
using FluxMq.Pipeline.Components;
using MQTTnet.Protocol;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Pipeline.Tests.Components;

public sealed class PayloadInspectorMapperComponentTests
{
    [Fact]
    public async Task Output_MapsEnvelopeToInspectedMessage()
    {
        var component = new PayloadInspectorMapperComponent();
        var received = new List<InspectedMqttMessage>();
        var sink = new ActionBlock<InspectedMqttMessage>(received.Add);

        component.Output.LinkTo(sink, new DataflowLinkOptions { PropagateCompletion = true });

        component.Input.Post(new MqttEnvelope
        {
            Topic = "factory/status",
            Payload = Encoding.UTF8.GetBytes("""{"ok":true}"""),
            QualityOfService = MqttQualityOfServiceLevel.AtLeastOnce
        });
        component.Input.Complete();

        await sink.Completion;

        var message = received.Should().ContainSingle().Subject;
        message.Envelope.Topic.Should().Be("factory/status");
        message.Payload.Format.Should().Be(PayloadFormat.Json);
    }
}
