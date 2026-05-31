using FluxMq.Scenarios;
using Shouldly;

namespace FluxMq.Scenarios.Tests;

public sealed class ScenarioEventSourceRequirementsTests
{
    [Fact]
    public void RequiresAttachedEventStream_ReturnsFalseForEmptyScenario()
    {
        var scenario = new ScenarioDefinition();

        ScenarioEventSourceRequirements.RequiresAttachedEventStream(scenario)
            .ShouldBeFalse();
    }

    [Theory]
    [InlineData(ScenarioStepTypes.ExpectEvent)]
    [InlineData(ScenarioStepTypes.WhenEvent)]
    public void RequiresAttachedEventStream_ReturnsTrueWhenEventObserverStartsScenario(string observerType)
    {
        var scenario = Scenario(
            ("observe", observerType));

        ScenarioEventSourceRequirements.RequiresAttachedEventStream(scenario)
            .ShouldBeTrue();
    }

    [Theory]
    [InlineData(ScenarioStepTypes.MqttPublisher, ScenarioStepTypes.ExpectEvent)]
    [InlineData(ScenarioStepTypes.MqttPublisher, ScenarioStepTypes.WhenEvent)]
    [InlineData(ScenarioStepTypes.MqttTrigger, ScenarioStepTypes.ExpectEvent)]
    [InlineData(ScenarioStepTypes.MqttTrigger, ScenarioStepTypes.WhenEvent)]
    public void RequiresAttachedEventStream_ReturnsFalseWhenRunnerOwnedEventSourceComesFirst(
        string eventSourceType,
        string observerType)
    {
        var scenario = Scenario(
            ("source", eventSourceType),
            ("observe", observerType));

        ScenarioEventSourceRequirements.RequiresAttachedEventStream(scenario)
            .ShouldBeFalse();
    }

    [Fact]
    public void RequiresAttachedEventStream_ReturnsTrueWhenObserverComesBeforeRunnerOwnedEventSource()
    {
        var scenario = Scenario(
            ("observe", ScenarioStepTypes.ExpectEvent),
            ("source", ScenarioStepTypes.MqttPublisher));

        ScenarioEventSourceRequirements.RequiresAttachedEventStream(scenario)
            .ShouldBeTrue();
    }

    [Fact]
    public void DescribeMissingEventStream_NamesScenarioAndEventSourceOptions()
    {
        var message = ScenarioEventSourceRequirements.DescribeMissingEventStream("smoke");

        message.ShouldContain("Scenario 'smoke'");
        message.ShouldContain("mqtt.publisher");
        message.ShouldContain("mqtt.trigger");
        message.ShouldContain("expect.event");
        message.ShouldContain("when.event");
    }

    private static ScenarioDefinition Scenario(params (string Name, string Type)[] steps)
    {
        var scenario = new ScenarioDefinition();
        foreach (var (name, type) in steps)
        {
            scenario.Steps[name] = new ScenarioStepDefinition { Type = type };
        }

        return scenario;
    }
}
