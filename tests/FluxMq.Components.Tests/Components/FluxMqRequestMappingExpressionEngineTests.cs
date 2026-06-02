using FluxMq.Components.FileWriter;
using FluxMq.Components.Mapping;
using FluxMq.Components.MqttPublisher;
using FluxMq.Components.Replay;
using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxFlow.Engine.Mapping;
using MQTTnet.Protocol;
using Shouldly;

namespace FluxMq.Components.Tests.Components;

public sealed class FluxMqRequestMappingExpressionEngineTests
{
    [Fact]
    public void JsonataMapper_MapsObjectExpressionWithQosAlias()
    {
        var envelope = Envelope(
            "factory/line-1",
            """{"hello":"fluxmq"}"""u8.ToArray(),
            MqttQualityOfServiceLevel.AtLeastOnce,
            retain: true);
        var engine = new FluxMqRequestMappingExpressionEngine(new FluxMqJsonataExpressionEngine());

        var request = (MqttPublishRequest)engine.Evaluate(
            """
            {
              "topic": 'test',
              "payload": payloadText,
              "qos": qos,
              "retain": retain
            }
            """,
            MqttEnvelopeExpressionContextFactory.Create(envelope),
            typeof(MqttPublishRequest))!;

        request.Topic.ShouldBe("test");
        request.Payload.ShouldBe("""{"hello":"fluxmq"}"""u8.ToArray());
        request.QualityOfService.ShouldBe(MqttQualityOfServiceLevel.AtLeastOnce);
        request.Retain.ShouldBeTrue();
    }

    [Fact]
    public void DynamicExpressoMapper_MapsTypedPublishRequest()
    {
        var envelope = Envelope("factory/line-1", "hello"u8.ToArray());
        var engine = new FluxMqRequestMappingExpressionEngine(new FluxMqDynamicExpressionEngine());

        var request = (MqttPublishRequest)engine.Evaluate(
            """
            new MqttPublishRequest {
              Topic = "mirror/" + topic,
              Payload = Encoding.UTF8.GetBytes("mapped:" + payloadText),
              QualityOfService = MqttQualityOfServiceLevel.AtLeastOnce,
              Retain = false
            }
            """,
            MqttEnvelopeExpressionContextFactory.Create(envelope),
            typeof(MqttPublishRequest))!;

        request.Topic.ShouldBe("mirror/factory/line-1");
        request.Payload.ShouldBe("mapped:hello"u8.ToArray());
        request.QualityOfService.ShouldBe(MqttQualityOfServiceLevel.AtLeastOnce);
        request.Retain.ShouldBeFalse();
    }

    [Fact]
    public void JsonataMapper_MapsFileWriteRequest()
    {
        var envelope = Envelope("factory/line-1", "hello"u8.ToArray());
        var engine = new FluxMqRequestMappingExpressionEngine(new FluxMqJsonataExpressionEngine());

        var request = (FileWriteRequest)engine.Evaluate(
            """
            {
              "path": "factory-line-1.txt",
              "content": "payload:" & payloadText,
              "mode": "Append",
              "createDirectory": false
            }
            """,
            MqttEnvelopeExpressionContextFactory.Create(envelope),
            typeof(FileWriteRequest))!;

        request.Path.ShouldBe("factory-line-1.txt");
        request.Content.ShouldBe("payload:hello"u8.ToArray());
        request.Mode.ShouldBe(FileWriteMode.Append);
        request.CreateDirectory.ShouldBeFalse();
    }

    [Fact]
    public void JsonataMapper_MapsRecordingRequest()
    {
        var sessionId = SessionId.New();
        var envelope = Envelope("factory/line-1", "hello"u8.ToArray());
        var engine = new FluxMqRequestMappingExpressionEngine(new FluxMqJsonataExpressionEngine());

        var request = (MqttRecordingRequest)engine.Evaluate(
            $$"""
            {
              "sessionId": "{{sessionId}}"
            }
            """,
            MqttEnvelopeExpressionContextFactory.Create(envelope),
            typeof(MqttRecordingRequest))!;

        request.SessionId.ShouldBe(sessionId);
        request.Envelope.ShouldBeSameAs(envelope);
    }

    private static MqttEnvelope Envelope(
        string topic,
        byte[] payload,
        MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce,
        bool retain = false)
        => new()
        {
            Topic = topic,
            Payload = payload,
            QualityOfService = qos,
            Retain = retain
        };
}
