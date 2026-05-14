using Shouldly;
using FluxMq.Core.Models;

namespace FluxMq.Core.Tests.Models;

public class MqttConnectionProfileTests
{
    [Fact]
    public void DefaultProfile_HasSensibleDefaults()
    {
        var profile = new MqttConnectionProfile();

        profile.Host.ShouldBe("localhost");
        profile.Port.ShouldBe(1883);
        profile.UseTls.ShouldBeFalse();
        profile.CleanStart.ShouldBeTrue();
        profile.KeepAlive.ShouldBe(TimeSpan.FromSeconds(60));
        profile.Username.ShouldBeNull();
        profile.Password.ShouldBeNull();
    }

    [Fact]
    public void DefaultProfile_GeneratesUniqueIds()
    {
        var a = new MqttConnectionProfile();
        var b = new MqttConnectionProfile();

        a.Id.ShouldNotBe(b.Id);
        a.ClientId.ShouldNotBe(b.ClientId);
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

        profile.Name.ShouldBe("Test Broker");
        profile.Host.ShouldBe("broker.example.com");
        profile.Port.ShouldBe(8883);
        profile.UseTls.ShouldBeTrue();
        profile.Username.ShouldBe("user");
        profile.Password.ShouldBe("pass");
        profile.CleanStart.ShouldBeFalse();
    }

    [Fact]
    public void Profile_WithExpression_ProducesNewInstanceWithChange()
    {
        var original = new MqttConnectionProfile { Name = "Original", Port = 1883 };
        var updated = original with { Port = 8883 };

        updated.Port.ShouldBe(8883);
        updated.Name.ShouldBe("Original");
        updated.Id.ShouldBe(original.Id);
    }
}
