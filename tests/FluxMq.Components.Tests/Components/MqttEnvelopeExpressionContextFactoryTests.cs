using FluxMq.Components.Mapping;
using FluxMq.Core.Models;
using MQTTnet.Protocol;
using Shouldly;
using System.Text;
using System.Text.Json;

namespace FluxMq.Components.Tests.Components;

public sealed class MqttEnvelopeExpressionContextFactoryTests
{
    [Fact]
    public void Create_AddsParsedPayloadJsonForJsonPayloads()
    {
        var envelope = CreateEnvelope("""{"value":42,"status":"ok"}""");

        var context = MqttEnvelopeExpressionContextFactory.Create(envelope);

        var payloadJson = context.Variables["payloadJson"].ShouldBeOfType<JsonElement>();
        payloadJson.GetProperty("value").GetInt32().ShouldBe(42);
        payloadJson.GetProperty("status").GetString().ShouldBe("ok");
    }

    [Fact]
    public void Create_SetsPayloadJsonToNullForNonJsonPayloads()
    {
        var envelope = CreateEnvelope("not-json");

        var context = MqttEnvelopeExpressionContextFactory.Create(envelope);

        context.Variables["payloadJson"].ShouldBeNull();
    }

    private static MqttEnvelope CreateEnvelope(string payload)
        => new()
        {
            Topic = "factory/line-a/status",
            Payload = Encoding.UTF8.GetBytes(payload),
            QualityOfService = MqttQualityOfServiceLevel.AtLeastOnce,
            Retain = false,
            ReceivedAt = DateTimeOffset.Parse("2026-05-23T10:00:00Z")
        };
}
