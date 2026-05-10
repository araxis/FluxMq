using System.Text;
using FluentAssertions;
using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Core.Payloads;
using FluxMq.Pipeline.Components;
using MQTTnet.Protocol;
using System.Threading.Tasks.Dataflow;
using FluxMq.Pipeline.Components.MqttPayloadInspector;

namespace FluxMq.Pipeline.Tests.Components;

public sealed class PayloadInspectorMapperComponentTests
{
    [Fact]
    public async Task Output_MapsEnvelopeToInspectedMessage()
    {
        var nodeId = FlowNodeId.New();
        var component = new PayloadInspectorMapperComponent(nodeId);
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
        component.Id.Should().Be(nodeId);
        component.Completion.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task Complete_CompletesInputAndOutput()
    {
        var component = new PayloadInspectorMapperComponent();
        var sink = new ActionBlock<InspectedMqttMessage>(_ => { });
        component.Output.LinkTo(sink, new DataflowLinkOptions { PropagateCompletion = true });

        component.Complete();
        await Task.WhenAll(component.Completion, sink.Completion);

        component.Completion.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task Fault_PublishesError()
    {
        var component = new PayloadInspectorMapperComponent();
        var errors = new List<FlowError>();
        var errorSink = new ActionBlock<FlowError>(errors.Add);
        var failure = new InvalidOperationException("inspect failed");

        component.Errors.LinkTo(errorSink, new DataflowLinkOptions { PropagateCompletion = true });
        component.Fault(failure);

        var act = async () => await component.Completion;
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("inspect failed");
        await errorSink.Completion;

        var error = errors.Should().ContainSingle().Subject;
        error.Code.Should().Be(FlowErrorCodes.NodeFaulted);
        error.Message.Should().Be("Payload inspector mapper faulted.");
    }
}
