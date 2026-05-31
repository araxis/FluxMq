using FluxMq.App;
using FluxMq.Pipeline.Scenarios;
using FluxMq.UI.Models;
using FluxMq.UI.Services;
using Shouldly;

namespace FluxMq.UI.Tests;

public sealed class ScenarioStepCatalogTests
{
    [Fact]
    public void Steps_ExposeKnownScenarioStepDescriptors()
    {
        var catalog = new ScenarioStepCatalog();

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
        publish.EditorKind.ShouldBe(ScenarioStepEditorKind.MqttPublish);
        publish.Fields.Select(field => field.Key).ShouldBe(
        [
            ScenarioStepCatalog.ConnectionKey,
            ScenarioStepCatalog.TopicKey,
            ScenarioStepCatalog.PayloadKey,
            ScenarioStepCatalog.PayloadEncodingKey,
            ScenarioStepCatalog.QosKey,
            ScenarioStepCatalog.RetainKey
        ]);
        publish.Fields.First(field => field.Key == ScenarioStepCatalog.PayloadEncodingKey)
            .Options.Select(option => option.Value)
            .ShouldBe(["json", "text", "base64", "bytes"]);
        publish.Fields.First(field => field.Key == ScenarioStepCatalog.QosKey)
            .Options.Select(option => option.Value)
            .ShouldBe(["0", "1", "2"]);

        var trigger = catalog.Find(ScenarioStepTypes.MqttTrigger).ShouldNotBeNull();
        trigger.DisplayName.ShouldBe("MQTT trigger");
        trigger.Category.ShouldBe("Action");
        trigger.NamePrefix.ShouldBe("triggerMqtt");
        trigger.EditorKind.ShouldBe(ScenarioStepEditorKind.MqttTrigger);
        trigger.Fields.Select(field => field.Key).ShouldBe(
        [
            ScenarioStepCatalog.ConnectionKey,
            ScenarioStepCatalog.SubscriptionsKey,
            ScenarioStepCatalog.QosKey,
            ScenarioStepCatalog.ReceiveRetainedKey,
            ScenarioStepCatalog.RetainAsPublishedKey
        ]);

        var when = catalog.Find(ScenarioStepTypes.WhenEvent).ShouldNotBeNull();
        when.DisplayName.ShouldBe("When event");
        when.Category.ShouldBe("Condition");
        when.Description.ShouldBe("Continue only when a scenario or app event matches configured filters.");
        when.NamePrefix.ShouldBe("whenEvent");
        when.EditorKind.ShouldBe(ScenarioStepEditorKind.ExpectEvent);
        when.Fields.ShouldBeEmpty();

        var expect = catalog.Find(ScenarioStepTypes.ExpectEvent).ShouldNotBeNull();
        expect.DisplayName.ShouldBe("Expect event");
        expect.Category.ShouldBe("Expectation");
        expect.Description.ShouldBe("Wait for a scenario or app event that matches configured filters.");
        expect.NamePrefix.ShouldBe("expectEvent");
        expect.EditorKind.ShouldBe(ScenarioStepEditorKind.ExpectEvent);
        expect.Fields.ShouldBeEmpty();
    }

    [Fact]
    public void Steps_AllKnownStepsHavePaletteAndApplicationScenarioRunner()
    {
        var paletteTypes = new ScenarioStepCatalog()
            .Steps
            .Select(step => step.Type)
            .ToArray();

        var runnerTypes = FlowApplicationHost
            .CreateDefaultScenarioStepRunnerRegistry()
            .Runners
            .Keys
            .ToHashSet(StringComparer.Ordinal);

        paletteTypes.ShouldBe(ScenarioStepTypes.All, ignoreOrder: true);
        runnerTypes.ShouldBe(ScenarioStepTypes.All, ignoreOrder: true);

        paletteTypes
            .Where(type => !runnerTypes.Contains(type))
            .ShouldBeEmpty();
    }

    [Fact]
    public void CreateDefaultConfiguration_UsesPublishFieldDefaults()
    {
        var catalog = new ScenarioStepCatalog();

        var defaults = catalog.CreateDefaultConfiguration(ScenarioStepTypes.MqttPublisher, "local-broker");

        defaults[ScenarioStepCatalog.ConnectionKey].ShouldBe("local-broker");
        defaults[ScenarioStepCatalog.TopicKey].ShouldBe("fluxmq/test");
        defaults[ScenarioStepCatalog.PayloadKey].ShouldBe("""{"hello":"fluxmq"}""");
        defaults[ScenarioStepCatalog.PayloadEncodingKey].ShouldBe("json");
        defaults[ScenarioStepCatalog.QosKey].ShouldBe("0");
        defaults[ScenarioStepCatalog.RetainKey].ShouldBe("false");
    }

    [Fact]
    public void CreateDefaultConfiguration_UsesTriggerFieldDefaults()
    {
        var catalog = new ScenarioStepCatalog();

        var defaults = catalog.CreateDefaultConfiguration(ScenarioStepTypes.MqttTrigger, "local-broker");

        defaults[ScenarioStepCatalog.ConnectionKey].ShouldBe("local-broker");
        defaults[ScenarioStepCatalog.SubscriptionsKey].ShouldBe("fluxmq/test/#");
        defaults[ScenarioStepCatalog.QosKey].ShouldBe("1");
        defaults[ScenarioStepCatalog.ReceiveRetainedKey].ShouldBe("false");
        defaults[ScenarioStepCatalog.RetainAsPublishedKey].ShouldBe("true");
    }

    [Fact]
    public void ScenarioStepFieldDescriptor_NormalizesMissingOptions()
    {
        var descriptor = new ScenarioStepFieldDescriptor(
            "field",
            "Field",
            ScenarioStepFieldKind.Select,
            string.Empty,
            null!);

        descriptor.Options.ShouldBeEmpty();
    }

    [Fact]
    public void Describe_ReturnsFallbackDescriptorForUnknownStepType()
    {
        var catalog = new ScenarioStepCatalog();

        var descriptor = catalog.Describe("custom.step");

        descriptor.Type.ShouldBe("custom.step");
        descriptor.DisplayName.ShouldBe("custom.step");
        descriptor.Category.ShouldBe("Custom");
        descriptor.NamePrefix.ShouldBe("step");
    }
}
