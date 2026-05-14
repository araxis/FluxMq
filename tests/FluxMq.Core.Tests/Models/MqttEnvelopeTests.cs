using Shouldly;
using FluxMq.Core.Models;
using MQTTnet.Protocol;

namespace FluxMq.Core.Tests.Models;

public class MqttEnvelopeTests
{
    [Fact]
    public void Envelope_StoresAllFields()
    {
        var payload = """{"rpm":1420}"""u8.ToArray();
        var before = DateTimeOffset.UtcNow;

        var envelope = new MqttEnvelope
        {
            Topic = "factory/line-01/telemetry",
            Payload = payload,
            QualityOfService = MqttQualityOfServiceLevel.AtLeastOnce,
            Retain = true
        };

        envelope.Topic.ShouldBe("factory/line-01/telemetry");
        envelope.Payload.ShouldBe(payload);
        envelope.QualityOfService.ShouldBe(MqttQualityOfServiceLevel.AtLeastOnce);
        envelope.Retain.ShouldBeTrue();
        envelope.ReceivedAt.ShouldBeGreaterThanOrEqualTo(before);
    }

    [Fact]
    public void Envelope_DefaultQos_IsAtMostOnce()
    {
        var envelope = new MqttEnvelope { Topic = "t", Payload = [] };

        envelope.QualityOfService.ShouldBe(MqttQualityOfServiceLevel.AtMostOnce);
        envelope.Retain.ShouldBeFalse();
    }

    [Fact]
    public void Envelope_ReceivedAt_DefaultsToUtcNow()
    {
        var before = DateTimeOffset.UtcNow;
        var envelope = new MqttEnvelope { Topic = "t", Payload = [] };
        var after = DateTimeOffset.UtcNow;

        envelope.ReceivedAt.ShouldBeGreaterThanOrEqualTo(before);
        envelope.ReceivedAt.ShouldBeLessThanOrEqualTo(after);
    }
}
