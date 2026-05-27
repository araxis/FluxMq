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
            .ShouldBe([ScenarioStepTypes.MqttPublisher, ScenarioStepTypes.ExpectEvent]);

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

        var expect = catalog.Find(ScenarioStepTypes.ExpectEvent).ShouldNotBeNull();
        expect.DisplayName.ShouldBe("Expect event");
        expect.Category.ShouldBe("Expectation");
        expect.NamePrefix.ShouldBe("expectEvent");
        expect.EditorKind.ShouldBe(ScenarioStepEditorKind.ExpectEvent);
        expect.Fields.ShouldBeEmpty();
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
    public void Find_AcceptsLegacyMqttPublishStepType()
    {
        var catalog = new ScenarioStepCatalog();

        var descriptor = catalog.Find(ScenarioStepTypes.MqttPublish).ShouldNotBeNull();
        var defaults = catalog.CreateDefaultConfiguration(ScenarioStepTypes.MqttPublish, "local-broker");

        descriptor.Type.ShouldBe(ScenarioStepTypes.MqttPublisher);
        defaults[ScenarioStepCatalog.ConnectionKey].ShouldBe("local-broker");
        defaults[ScenarioStepCatalog.TopicKey].ShouldBe("fluxmq/test");
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
