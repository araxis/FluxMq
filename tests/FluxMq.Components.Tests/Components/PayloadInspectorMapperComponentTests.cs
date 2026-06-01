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

    [Theory]
    [InlineData("1", PayloadFormat.Number, "Number")]
    [InlineData("true", PayloadFormat.Boolean, "Boolean")]
    [InlineData("null", PayloadFormat.Null, "Null")]
    [InlineData("\"hello\"", PayloadFormat.String, "String")]
    [InlineData("[1,2]", PayloadFormat.Array, "Array")]
    public async Task Output_PreservesJsonValueFormats(
        string payload,
        PayloadFormat expectedFormat,
        string expectedLabel)
    {
        var message = await InspectAsync(Encoding.UTF8.GetBytes(payload));

        message.Payload.Format.ShouldBe(expectedFormat);
        message.Payload.ContentTypeLabel.ShouldBe(expectedLabel);
        message.Payload.DisplayTypeLabel.ShouldBe(expectedLabel);
    }

    [Fact]
    public async Task Output_PreservesBase64Summary()
    {
        var message = await InspectAsync(Encoding.UTF8.GetBytes("SGVsbG8gTVFUVCE="));

        message.Payload.Format.ShouldBe(PayloadFormat.Base64);
        message.Payload.FormattedText.ShouldContain("Decoded bytes: 11");
    }

    [Fact]
    public async Task Output_PreservesPlainText()
    {
        var message = await InspectAsync(Encoding.UTF8.GetBytes("temperature=21.4"));

        message.Payload.Format.ShouldBe(PayloadFormat.Text);
        message.Payload.RawText.ShouldBe("temperature=21.4");
        message.Payload.FormattedText.ShouldBe("temperature=21.4");
    }

    [Fact]
    public async Task Output_PreservesBinaryHexDump()
    {
        var message = await InspectAsync([0xFF, 0x00, 0x10, 0x80]);

        message.Payload.Format.ShouldBe(PayloadFormat.Binary);
        message.Payload.IsText.ShouldBeFalse();
        message.Payload.HexDump.ShouldStartWith("00000000  FF 00 10 80");
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

    private static async Task<InspectedMqttMessage> InspectAsync(byte[] payload)
    {
        var component = new PayloadInspectorMapperComponent();
        var received = new List<InspectedMqttMessage>();
        var sink = new ActionBlock<InspectedMqttMessage>(received.Add);

        component.Output.LinkTo(sink, new DataflowLinkOptions { PropagateCompletion = true });
        component.Input.Post(new MqttEnvelope
        {
            Topic = "factory/status",
            Payload = payload
        });
        component.Input.Complete();

        await sink.Completion;
        return received.ShouldHaveSingleItem();
    }
}
