using FluxMq.Scenarios;
using Shouldly;

namespace FluxMq.Scenarios.Tests.Scenarios;

public sealed class ScenarioStepDefinitionCatalogTests
{
    [Fact]
    public void Steps_ExposeKnownScenarioStepDefinitions()
    {
        var catalog = new ScenarioStepDefinitionCatalog();

        catalog.Steps.Select(step => step.Type)
            .ShouldBe([
                ScenarioStepTypes.MqttPublisher,
                ScenarioStepTypes.MqttTrigger,
                ScenarioStepTypes.WhenEvent,
                ScenarioStepTypes.ExpectEvent
            ]);

        var publish = catalog.Find(ScenarioStepTypes.MqttPublisher).ShouldNotBeNull();
        publish.DisplayName.ShouldBe("MQTT publisher");
        publish.Category.ShouldBe("Action");
        publish.NamePrefix.ShouldBe("publishMessage");
        publish.Fields.Select(field => field.Key).ShouldBe(
        [
            ScenarioStepDefinitionCatalog.ConnectionKey,
            ScenarioStepDefinitionCatalog.TopicKey,
            ScenarioStepDefinitionCatalog.PayloadKey,
            ScenarioStepDefinitionCatalog.PayloadEncodingKey,
            ScenarioStepDefinitionCatalog.QosKey,
            ScenarioStepDefinitionCatalog.RetainKey
        ]);
        publish.Fields.First(field => field.Key == ScenarioStepDefinitionCatalog.PayloadEncodingKey)
            .Options.Select(option => option.Value)
            .ShouldBe(["json", "text", "base64", "bytes"]);
        publish.Fields.First(field => field.Key == ScenarioStepDefinitionCatalog.QosKey)
            .Options.Select(option => option.Value)
            .ShouldBe(["0", "1", "2"]);

        var trigger = catalog.Find(ScenarioStepTypes.MqttTrigger).ShouldNotBeNull();
        trigger.DisplayName.ShouldBe("MQTT trigger");
        trigger.Category.ShouldBe("Action");
        trigger.NamePrefix.ShouldBe("triggerMqtt");
        trigger.Fields.Select(field => field.Key).ShouldBe(
        [
            ScenarioStepDefinitionCatalog.ConnectionKey,
            ScenarioStepDefinitionCatalog.SubscriptionsKey,
            ScenarioStepDefinitionCatalog.QosKey,
            ScenarioStepDefinitionCatalog.ReceiveRetainedKey,
            ScenarioStepDefinitionCatalog.RetainAsPublishedKey
        ]);

        var when = catalog.Find(ScenarioStepTypes.WhenEvent).ShouldNotBeNull();
        when.DisplayName.ShouldBe("When event");
        when.Category.ShouldBe("Condition");
        when.Description.ShouldBe("Continue only when a scenario or app event matches configured filters.");
        when.NamePrefix.ShouldBe("whenEvent");
        when.Fields.Select(field => field.Key).ShouldBe(EventStepFieldKeys);
        when.Fields.First(field => field.Key == ScenarioStepDefinitionCatalog.QosAttributeKey)
            .Options.Select(option => option.Value)
            .ShouldBe(["", "0", "1", "2"]);
        when.Fields.First(field => field.Key == ScenarioStepDefinitionCatalog.RetainAttributeKey)
            .Options.Select(option => option.Value)
            .ShouldBe(["", "true", "false"]);

        var expect = catalog.Find(ScenarioStepTypes.ExpectEvent).ShouldNotBeNull();
        expect.DisplayName.ShouldBe("Expect event");
        expect.Category.ShouldBe("Expectation");
        expect.Description.ShouldBe("Wait for a scenario or app event that matches configured filters.");
        expect.NamePrefix.ShouldBe("expectEvent");
        expect.Fields.Select(field => field.Key).ShouldBe(EventStepFieldKeys);
    }

    [Fact]
    public void CreateDefaultConfiguration_UsesPublishFieldDefaults()
    {
        var catalog = new ScenarioStepDefinitionCatalog();

        var defaults = catalog.CreateDefaultConfiguration(ScenarioStepTypes.MqttPublisher, "local-broker");

        defaults[ScenarioStepDefinitionCatalog.ConnectionKey].ShouldBe("local-broker");
        defaults[ScenarioStepDefinitionCatalog.TopicKey].ShouldBe("fluxmq/test");
        defaults[ScenarioStepDefinitionCatalog.PayloadKey].ShouldBe("""{"hello":"fluxmq"}""");
        defaults[ScenarioStepDefinitionCatalog.PayloadEncodingKey].ShouldBe("json");
        defaults[ScenarioStepDefinitionCatalog.QosKey].ShouldBe("0");
        defaults[ScenarioStepDefinitionCatalog.RetainKey].ShouldBe("false");
    }

    [Fact]
    public void CreateDefaultConfiguration_UsesTriggerFieldDefaults()
    {
        var catalog = new ScenarioStepDefinitionCatalog();

        var defaults = catalog.CreateDefaultConfiguration(ScenarioStepTypes.MqttTrigger, "local-broker");

        defaults[ScenarioStepDefinitionCatalog.ConnectionKey].ShouldBe("local-broker");
        defaults[ScenarioStepDefinitionCatalog.SubscriptionsKey].ShouldBe("fluxmq/test/#");
        defaults[ScenarioStepDefinitionCatalog.QosKey].ShouldBe("1");
        defaults[ScenarioStepDefinitionCatalog.ReceiveRetainedKey].ShouldBe("false");
        defaults[ScenarioStepDefinitionCatalog.RetainAsPublishedKey].ShouldBe("true");
    }

    [Theory]
    [InlineData(ScenarioStepTypes.WhenEvent)]
    [InlineData(ScenarioStepTypes.ExpectEvent)]
    public void CreateDefaultConfiguration_UsesEventFieldDefaults(string stepType)
    {
        var catalog = new ScenarioStepDefinitionCatalog();

        var defaults = catalog.CreateDefaultConfiguration(stepType, "local-broker");

        defaults[ScenarioStepDefinitionCatalog.EventTypeKey].ShouldBe(FlowEventTypes.MqttMessagePublished);
        defaults[ScenarioStepDefinitionCatalog.TopicStartsWithKey].ShouldBeEmpty();
        defaults[ScenarioStepDefinitionCatalog.TopicNotStartsWithKey].ShouldBeEmpty();
        defaults[ScenarioStepDefinitionCatalog.SubjectStartsWithKey].ShouldBeEmpty();
        defaults[ScenarioStepDefinitionCatalog.StatusKey].ShouldBe("published");
        defaults[ScenarioStepDefinitionCatalog.SourceKey].ShouldBeEmpty();
        defaults[ScenarioStepDefinitionCatalog.PayloadContainsKey].ShouldBeEmpty();
        defaults[ScenarioStepDefinitionCatalog.QosAttributeKey].ShouldBeEmpty();
        defaults[ScenarioStepDefinitionCatalog.RetainAttributeKey].ShouldBeEmpty();
        defaults[ScenarioStepDefinitionCatalog.SchemaIdAttributeKey].ShouldBeEmpty();
        defaults[ScenarioStepDefinitionCatalog.TimeoutMsKey].ShouldBe("5000");
    }

    [Fact]
    public void Describe_ReturnsFallbackDefinitionForUnknownStepType()
    {
        var catalog = new ScenarioStepDefinitionCatalog();

        var definition = catalog.Describe("custom.step");

        definition.Type.ShouldBe("custom.step");
        definition.DisplayName.ShouldBe("custom.step");
        definition.Category.ShouldBe("Custom");
        definition.NamePrefix.ShouldBe("step");
        definition.Fields.ShouldBeEmpty();
    }

    [Fact]
    public void AttributeFilterKeys_AreSharedWithScenarioConfigurationKeys()
    {
        ScenarioStepDefinitionCatalog.QosAttributeKey
            .ShouldBe(ScenarioStepConfigurationKeys.AttributeFilterKey("qos"));
        ScenarioStepConfigurationKeys.TryGetAttributeName(
                ScenarioStepDefinitionCatalog.SchemaIdAttributeKey,
                out var attributeName)
            .ShouldBeTrue();
        attributeName.ShouldBe("schemaId");
    }

    private static readonly string[] EventStepFieldKeys =
    [
        ScenarioStepDefinitionCatalog.EventTypeKey,
        ScenarioStepDefinitionCatalog.TopicStartsWithKey,
        ScenarioStepDefinitionCatalog.TopicNotStartsWithKey,
        ScenarioStepDefinitionCatalog.SubjectStartsWithKey,
        ScenarioStepDefinitionCatalog.StatusKey,
        ScenarioStepDefinitionCatalog.SourceKey,
        ScenarioStepDefinitionCatalog.PayloadContainsKey,
        ScenarioStepDefinitionCatalog.QosAttributeKey,
        ScenarioStepDefinitionCatalog.RetainAttributeKey,
        ScenarioStepDefinitionCatalog.SchemaIdAttributeKey,
        ScenarioStepDefinitionCatalog.TimeoutMsKey
    ];
}
