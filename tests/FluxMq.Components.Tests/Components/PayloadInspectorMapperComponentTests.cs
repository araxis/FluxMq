using System.Text;
using Shouldly;
using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Core.Payloads;
using FluxMq.Components.MqttPayloadInspector;
using FluxFlow.Engine.Components;
using MQTTnet.Protocol;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Components.Tests.Components;

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

        var message = received.ShouldHaveSingleItem();
        message.Envelope.Topic.ShouldBe("factory/status");
        message.Payload.Format.ShouldBe(PayloadFormat.Json);
        component.Id.ShouldBe(nodeId);
        component.Completion.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Fact]
    public async Task Complete_CompletesInputAndOutput()
    {
        var component = new PayloadInspectorMapperComponent();
        var sink = new ActionBlock<InspectedMqttMessage>(_ => { });
        component.Output.LinkTo(sink, new DataflowLinkOptions { PropagateCompletion = true });

        component.Complete();
        await Task.WhenAll(component.Completion, sink.Completion);

        component.Completion.IsCompletedSuccessfully.ShouldBeTrue();
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
        var ex = await Should.ThrowAsync<InvalidOperationException>(act);
        ex.Message.ShouldBe("inspect failed");
        await errorSink.Completion;

        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(FlowErrorCodes.NodeFaulted);
        error.Message.ShouldBe("Payload inspector mapper faulted.");
    }
}
