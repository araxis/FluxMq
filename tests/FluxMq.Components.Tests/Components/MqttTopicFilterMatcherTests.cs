using Shouldly;
using FluxMq.Components.MessageSource;

namespace FluxMq.Components.Tests.Components;

public sealed class MqttTopicFilterMatcherTests
{
    [Theory]
    [InlineData("sensors/temp", "sensors/temp", true)]
    [InlineData("sensors/temp", "sensors/humidity", false)]
    [InlineData("sensors/+", "sensors/temp", true)]
    [InlineData("sensors/+", "sensors/temp/room1", false)]
    [InlineData("sensors/#", "sensors/temp", true)]
    [InlineData("sensors/#", "sensors/temp/room1", true)]
    // MQTT 5 §4.7.1.2: '#' includes the parent level, so 'sensors/#' matches 'sensors'.
    [InlineData("sensors/#", "sensors", true)]
    [InlineData("sensors/#", "lights", false)]
    [InlineData("#", "anything/at/all", true)]
    [InlineData("+/+", "a/b", true)]
    [InlineData("+/+", "a/b/c", false)]
    public void IsMatch_HandlesStandardWildcards(string filter, string topic, bool expected)
    {
        MqttTopicFilterMatcher.IsMatch(filter, topic).ShouldBe(expected);
    }

    [Theory]
    [InlineData("#", "$SYS/broker/uptime", false)]
    [InlineData("+/broker/uptime", "$SYS/broker/uptime", false)]
    [InlineData("$SYS/#", "$SYS/broker/uptime", true)]
    [InlineData("$SYS/broker/+", "$SYS/broker/uptime", true)]
    public void IsMatch_DoesNotMatchDollarTopicsAgainstWildcardFilters(string filter, string topic, bool expected)
    {
        MqttTopicFilterMatcher.IsMatch(filter, topic).ShouldBe(expected);
    }

    [Fact]
    public void MatchesAny_ReturnsTrue_WhenAnyFilterMatches()
    {
        string[] filters = ["sensors/+", "$SYS/#"];

        MqttTopicFilterMatcher.MatchesAny(filters, "sensors/temp").ShouldBeTrue();
        MqttTopicFilterMatcher.MatchesAny(filters, "$SYS/broker/uptime").ShouldBeTrue();
        MqttTopicFilterMatcher.MatchesAny(filters, "lights/kitchen/state").ShouldBeFalse();
    }
}
