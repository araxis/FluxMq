using FluentAssertions;
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

        envelope.Topic.Should().Be("factory/line-01/telemetry");
        envelope.Payload.Should().Equal(payload);
        envelope.QualityOfService.Should().Be(MqttQualityOfServiceLevel.AtLeastOnce);
        envelope.Retain.Should().BeTrue();
        envelope.ReceivedAt.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void Envelope_DefaultQos_IsAtMostOnce()
    {
        var envelope = new MqttEnvelope { Topic = "t", Payload = [] };

        envelope.QualityOfService.Should().Be(MqttQualityOfServiceLevel.AtMostOnce);
        envelope.Retain.Should().BeFalse();
    }

    [Fact]
    public void Envelope_ReceivedAt_DefaultsToUtcNow()
    {
        var before = DateTimeOffset.UtcNow;
        var envelope = new MqttEnvelope { Topic = "t", Payload = [] };
        var after = DateTimeOffset.UtcNow;

        envelope.ReceivedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }
}
