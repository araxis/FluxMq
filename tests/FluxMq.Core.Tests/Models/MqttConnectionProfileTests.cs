using FluentAssertions;
using FluxMq.Core.Models;

namespace FluxMq.Core.Tests.Models;

public class MqttConnectionProfileTests
{
    [Fact]
    public void DefaultProfile_HasSensibleDefaults()
    {
        var profile = new MqttConnectionProfile();

        profile.Host.Should().Be("localhost");
        profile.Port.Should().Be(1883);
        profile.UseTls.Should().BeFalse();
        profile.CleanStart.Should().BeTrue();
        profile.KeepAlive.Should().Be(TimeSpan.FromSeconds(60));
        profile.Username.Should().BeNull();
        profile.Password.Should().BeNull();
    }

    [Fact]
    public void DefaultProfile_GeneratesUniqueIds()
    {
        var a = new MqttConnectionProfile();
        var b = new MqttConnectionProfile();

        a.Id.Should().NotBe(b.Id);
        a.ClientId.Should().NotBe(b.ClientId);
    }

    [Fact]
    public void Profile_CanBeCustomised_WithInitProperties()
    {
        var profile = new MqttConnectionProfile
        {
            Name = "Test Broker",
            Host = "broker.example.com",
            Port = 8883,
            UseTls = true,
            Username = "user",
            Password = "pass",
            CleanStart = false
        };

        profile.Name.Should().Be("Test Broker");
        profile.Host.Should().Be("broker.example.com");
        profile.Port.Should().Be(8883);
        profile.UseTls.Should().BeTrue();
        profile.Username.Should().Be("user");
        profile.Password.Should().Be("pass");
        profile.CleanStart.Should().BeFalse();
    }

    [Fact]
    public void Profile_WithExpression_ProducesNewInstanceWithChange()
    {
        var original = new MqttConnectionProfile { Name = "Original", Port = 1883 };
        var updated = original with { Port = 8883 };

        updated.Port.Should().Be(8883);
        updated.Name.Should().Be("Original");
        updated.Id.Should().Be(original.Id);
    }
}
