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
            .ShouldBe([ScenarioStepTypes.MqttPublish, ScenarioStepTypes.ExpectEvent]);

        var publish = catalog.Find(ScenarioStepTypes.MqttPublish).ShouldNotBeNull();
        publish.DisplayName.ShouldBe("MQTT publish");
        publish.Category.ShouldBe("Action");
        publish.NamePrefix.ShouldBe("publishMessage");
        publish.EditorKind.ShouldBe(ScenarioStepEditorKind.MqttPublish);

        var expect = catalog.Find(ScenarioStepTypes.ExpectEvent).ShouldNotBeNull();
        expect.DisplayName.ShouldBe("Expect event");
        expect.Category.ShouldBe("Expectation");
        expect.NamePrefix.ShouldBe("expectEvent");
        expect.EditorKind.ShouldBe(ScenarioStepEditorKind.ExpectEvent);
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
