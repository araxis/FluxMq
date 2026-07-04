using FluxMq.Scenarios;
using FluxMq.UI.Models;
using FluxMq.UI.Services;
using Shouldly;

namespace FluxMq.UI.Tests;

public sealed class ScenarioStepDisplayTests
{
    private static readonly ScenarioStepCatalog Catalog = new();

    [Fact]
    public void StepTypeClass_DistinguishesScenarioStepKinds()
    {
        ScenarioStepDisplay.StepTypeClass(Step(ScenarioStepTypes.MqttPublisher), Catalog)
            .ShouldBe("test-step-type publish");
        ScenarioStepDisplay.StepTypeClass(Step(ScenarioStepTypes.MqttTrigger), Catalog)
            .ShouldBe("test-step-type trigger");
        ScenarioStepDisplay.StepTypeClass(Step(ScenarioStepTypes.WhenEvent), Catalog)
            .ShouldBe("test-step-type when");
        ScenarioStepDisplay.StepTypeClass(Step(ScenarioStepTypes.ExpectEvent), Catalog)
            .ShouldBe("test-step-type expect");
    }

    [Fact]
    public void StepResultMarkerClass_HandlesSkippedGuardStatus()
    {
        ScenarioStepDisplay.StepCardClass(ScenarioStepRunStatus.Skipped)
            .ShouldBe("test-step-card skipped");
        ScenarioStepDisplay.StepResultMarkerClass(ScenarioStepRunStatus.Skipped)
            .ShouldBe("test-step-result-marker skipped");
    }

    [Fact]
    public void BuildStepSummary_FormatsMqttTriggerConfiguration()
    {
        var step = Step(
            ScenarioStepTypes.MqttTrigger,
            (ScenarioStepCatalog.ConnectionKey, "local-broker"),
            (ScenarioStepCatalog.SubscriptionsKey, "factory/response/+"),
            (ScenarioStepCatalog.QosKey, "2"),
            (ScenarioStepCatalog.ReceiveRetainedKey, "true"),
            (ScenarioStepCatalog.RetainAsPublishedKey, "false"));

        ScenarioStepDisplay.BuildStepSummary(step, Catalog)
            .ShouldBe("local-broker / topic: factory/response/+ / QoS 2 / receive retained / clear retain flag");
    }

    [Fact]
    public void BuildStepSummary_FormatsWhenEventLikeEventFilter()
    {
        var step = Step(
            ScenarioStepTypes.WhenEvent,
            (ScenarioStepConfigurationKeys.EventType, "mqtt.message.published"),
            (ScenarioStepConfigurationKeys.TopicStartsWith, "test"),
            (DashboardEventFilterCatalog.AttributeFilterKey("retain"), "false"),
            (ScenarioStepConfigurationKeys.Status, "published"),
            (ScenarioStepConfigurationKeys.TimeoutMs, "250"));

        ScenarioStepDisplay.BuildStepSummary(step, Catalog)
            .ShouldBe("mqtt.message.published / topic: test / no retain / status: published / 250 ms");
    }

    [Theory]
    [InlineData(ScenarioStepConfigurationKeys.Subscriptions, "Topic filter")]
    [InlineData(ScenarioStepConfigurationKeys.ReceiveRetained, "Receive retained")]
    [InlineData(ScenarioStepConfigurationKeys.RetainAsPublished, "Retain as published")]
    [InlineData(ScenarioStepConfigurationKeys.Qos, "QoS")]
    public void FormatConfigurationKey_UsesCatalogFieldLabels(string key, string expected)
    {
        var step = Step(ScenarioStepTypes.MqttTrigger);

        ScenarioStepDisplay.FormatConfigurationKey(step, key, Catalog)
            .ShouldBe(expected);
    }

    [Fact]
    public void FormatConfigurationKey_UsesCatalogAttributeLabels()
    {
        var step = Step(ScenarioStepTypes.ExpectEvent);

        ScenarioStepDisplay.FormatConfigurationKey(step, ScenarioStepCatalog.RetainAttributeKey, Catalog)
            .ShouldBe("Retain");
        ScenarioStepDisplay.FormatConfigurationKey(step, ScenarioStepCatalog.SchemaIdAttributeKey, Catalog)
            .ShouldBe("Schema id");
    }

    [Fact]
    public void FormatConfigurationKey_FallsBackForUnknownAttributeFields()
    {
        var step = Step("custom.step");

        ScenarioStepDisplay.FormatConfigurationKey(step, DashboardEventFilterCatalog.AttributeFilterKey("custom"), Catalog)
            .ShouldBe("custom");
    }

    [Fact]
    public void VisibleConfiguration_UsesCatalogFieldOrder()
    {
        var step = Step(
            ScenarioStepTypes.MqttPublisher,
            (ScenarioStepCatalog.QosKey, "1"),
            ("custom", "value"),
            (ScenarioStepCatalog.TopicKey, "test"),
            (ScenarioStepCatalog.PayloadKey, "payload"),
            (ScenarioStepCatalog.ConnectionKey, "local-broker"),
            (ScenarioStepCatalog.RetainKey, string.Empty));

        var configuration = ScenarioStepDisplay.VisibleConfiguration(step, Catalog).ToArray();

        configuration.Select(item => item.Key)
            .ShouldBe([
                ScenarioStepCatalog.ConnectionKey,
                ScenarioStepCatalog.TopicKey,
                ScenarioStepCatalog.PayloadKey,
                ScenarioStepCatalog.QosKey,
                "custom"
            ]);
    }

    private static ScenarioStepSnapshot Step(
        string type,
        params (string Key, string Value)[] configuration)
        => new(
            "step",
            type,
            configuration.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal));
}
