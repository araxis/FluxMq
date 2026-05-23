using FluxMq.Core.Models;
using FluxMq.Components.Mapping;
using FluxMq.Pipeline.Mapping;
using FluxMq.Components.MqttPublisher;
using MQTTnet.Protocol;
using Shouldly;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Components.Tests.Components;

public sealed class MqttPublishRequestMapperComponentTests
{
    [Fact]
    public async Task Input_MapsEnvelopeToPublishRequest()
    {
        var component = new MqttPublishRequestMapperComponent(MqttPublishRequestMapperComponent.PreserveEnvelope);
        var output = new BufferBlock<MqttPublishRequest>();

        component.Output.LinkTo(output, new DataflowLinkOptions { PropagateCompletion = true });
        component.Input.Post(new MqttEnvelope
        {
            Topic = "factory/command",
            Payload = [1, 2, 3],
            QualityOfService = MqttQualityOfServiceLevel.AtLeastOnce,
            Retain = true
        });
        component.Complete();

        var request = await output.ReceiveAsync();
        await component.Completion;

        request.Topic.ShouldBe("factory/command");
        request.Payload.ShouldBe(new byte[] { 1, 2, 3 });
        request.QualityOfService.ShouldBe(MqttQualityOfServiceLevel.AtLeastOnce);
        request.Retain.ShouldBeTrue();
    }

    [Fact]
    public async Task DynamicExpressoMapper_MapsEnvelopeToPublishRequest()
    {
        var mapper = new MqttPublishRequestExpressionMapper(
            new DynamicExpressoFlowExpressionEngine(),
            new MqttPublishRequestMapDefinition
            {
                Expression = """
                new MqttPublishRequest {
                  Topic = "mirror/" + topic,
                  Payload = Encoding.UTF8.GetBytes("mapped:" + payloadText),
                  QualityOfService = MqttQualityOfServiceLevel.AtLeastOnce,
                  Retain = false
                }
                """
            });
        var component = new MqttPublishRequestMapperComponent(mapper);
        var output = new BufferBlock<MqttPublishRequest>();

        component.Output.LinkTo(output, new DataflowLinkOptions { PropagateCompletion = true });
        component.Input.Post(new MqttEnvelope
        {
            Topic = "factory/line-1",
            Payload = "hello"u8.ToArray(),
            QualityOfService = MqttQualityOfServiceLevel.AtMostOnce,
            Retain = true
        });
        component.Complete();

        var request = await output.ReceiveAsync();
        await component.Completion;

        request.Topic.ShouldBe("mirror/factory/line-1");
        request.Payload.ShouldBe("mapped:hello"u8.ToArray());
        request.QualityOfService.ShouldBe(MqttQualityOfServiceLevel.AtLeastOnce);
        request.Retain.ShouldBeFalse();
    }

    [Fact]
    public async Task JsonataMapper_MapsObjectExpressionWithQosAlias()
    {
        var mapper = new MqttPublishRequestExpressionMapper(
            new JsonataFlowExpressionEngine(),
            new MqttPublishRequestMapDefinition
            {
                Expression = """
                {
                  "topic": 'test',
                  "payload": payloadText,
                  "qos": qos,
                  "retain": retain
                }
                """
            });
        var component = new MqttPublishRequestMapperComponent(mapper);
        var output = new BufferBlock<MqttPublishRequest>();

        component.Output.LinkTo(output, new DataflowLinkOptions { PropagateCompletion = true });
        component.Input.Post(new MqttEnvelope
        {
            Topic = "factory/line-1",
            Payload = """{"hello":"fluxmq"}"""u8.ToArray(),
            QualityOfService = MqttQualityOfServiceLevel.AtLeastOnce,
            Retain = true
        });
        component.Complete();

        var request = await output.ReceiveAsync();
        await component.Completion;

        request.Topic.ShouldBe("test");
        request.Payload.ShouldBe("""{"hello":"fluxmq"}"""u8.ToArray());
        request.QualityOfService.ShouldBe(MqttQualityOfServiceLevel.AtLeastOnce);
        request.Retain.ShouldBeTrue();
    }
}
