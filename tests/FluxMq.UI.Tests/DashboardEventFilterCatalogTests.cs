using FluxFlow.Engine.Components;
using FluxMq.UI.Models;
using FluxMq.UI.Services;
using Shouldly;

namespace FluxMq.UI.Tests;

public sealed class DashboardEventFilterCatalogTests
{
    [Fact]
    public void Find_ReturnsEventSpecificFieldDescriptors()
    {
        var catalog = new DashboardEventFilterCatalog();

        var any = catalog.Find(string.Empty);
        var fileWritten = catalog.Find(FluxMqEventTypes.FileWritten);
        var schemaValidated = catalog.Find(FluxMqEventTypes.JsonSchemaValidated);
        var assertion = catalog.Find(FluxMqEventTypes.AssertionEvaluated);

        any.Fields.ShouldBeEmpty();

        var mqttReceived = catalog.Find(FluxMqEventTypes.MqttMessageReceived);
        mqttReceived.Fields.Select(static field => field.Key).ShouldBe([
            DashboardEventFilterCatalog.TopicStartsWithKey,
            DashboardEventFilterCatalog.TopicNotStartsWithKey,
            DashboardEventFilterCatalog.AttributeFilterKey("qos"),
            DashboardEventFilterCatalog.AttributeFilterKey("retain")
        ]);

        var fileField = fileWritten.Fields.ShouldHaveSingleItem();
        fileField.Key.ShouldBe(DashboardEventFilterCatalog.SubjectStartsWithKey);
        fileField.Label.ShouldBe("Path prefix");

        schemaValidated.Fields.Select(static field => field.Key).ShouldBe([
            DashboardEventFilterCatalog.TopicStartsWithKey,
            DashboardEventFilterCatalog.AttributeFilterKey("schemaId")
        ]);
        schemaValidated.Fields[1].AttributeName.ShouldBe("schemaId");

        assertion.Fields.Select(static field => field.Key).ShouldBe([
            DashboardEventFilterCatalog.TopicStartsWithKey,
            DashboardEventFilterCatalog.SubjectStartsWithKey
        ]);
        assertion.StatusOptions.Select(static option => option.Value).ShouldBe(["", "passed", "failed"]);
    }

    [Fact]
    public void Matches_UsesSubjectPrefixForFileWrittenEvents()
    {
        var catalog = new DashboardEventFilterCatalog();
        var widget = new DashboardWidgetSnapshot(
            "written",
            DashboardWidgetCatalog.EventCounterType,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [DashboardEventFilterCatalog.EventTypeKey] = FluxMqEventTypes.FileWritten,
                [DashboardEventFilterCatalog.SubjectStartsWithKey] = "logs/",
                [DashboardEventFilterCatalog.TopicStartsWithKey] = string.Empty,
                [DashboardEventFilterCatalog.StatusKey] = "written"
            });

        catalog.Matches(widget, Event(FluxMqEventTypes.FileWritten, subject: "logs/a.json", status: "written")).ShouldBeTrue();
        catalog.Matches(widget, Event(FluxMqEventTypes.FileWritten, subject: "archive/a.json", status: "written")).ShouldBeFalse();
        catalog.Matches(widget, Event(FluxMqEventTypes.FileWritten, topic: "logs/a.json", subject: "archive/a.json", status: "written")).ShouldBeFalse();
    }

    [Fact]
    public void Matches_UsesAttributeFieldForJsonSchemaEvents()
    {
        var catalog = new DashboardEventFilterCatalog();
        var widget = new DashboardWidgetSnapshot(
            "schema",
            DashboardWidgetCatalog.EventCounterType,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [DashboardEventFilterCatalog.EventTypeKey] = FluxMqEventTypes.JsonSchemaValidated,
                [DashboardEventFilterCatalog.TopicStartsWithKey] = "factory/",
                [DashboardEventFilterCatalog.AttributeFilterKey("schemaId")] = "temperature",
                [DashboardEventFilterCatalog.StatusKey] = "valid"
            });

        catalog.Matches(
            widget,
            Event(
                FluxMqEventTypes.JsonSchemaValidated,
                topic: "factory/line-a",
                status: "valid",
                attributes: new Dictionary<string, string> { ["schemaId"] = "temperature" })).ShouldBeTrue();
        catalog.Matches(
            widget,
            Event(
                FluxMqEventTypes.JsonSchemaValidated,
                topic: "factory/line-a",
                status: "valid",
                attributes: new Dictionary<string, string> { ["schemaId"] = "pressure" })).ShouldBeFalse();
    }

    [Fact]
    public void Matches_UsesMqttQosAndRetainAttributes()
    {
        var catalog = new DashboardEventFilterCatalog();
        var widget = new DashboardWidgetSnapshot(
            "published",
            DashboardWidgetCatalog.EventCounterType,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [DashboardEventFilterCatalog.EventTypeKey] = FluxMqEventTypes.MqttMessagePublished,
                [DashboardEventFilterCatalog.TopicStartsWithKey] = "test",
                [DashboardEventFilterCatalog.AttributeFilterKey("qos")] = "1",
                [DashboardEventFilterCatalog.AttributeFilterKey("retain")] = "false",
                [DashboardEventFilterCatalog.StatusKey] = "published"
            });

        catalog.Matches(
            widget,
            Event(
                FluxMqEventTypes.MqttMessagePublished,
                topic: "test",
                status: "published",
                attributes: new Dictionary<string, string>
                {
                    ["qos"] = "1",
                    ["retain"] = "False"
                })).ShouldBeTrue();
        catalog.Matches(
            widget,
            Event(
                FluxMqEventTypes.MqttMessagePublished,
                topic: "test",
                status: "published",
                attributes: new Dictionary<string, string>
                {
                    ["qos"] = "1",
                    ["retain"] = "True"
                })).ShouldBeFalse();
    }

    [Fact]
    public void Matches_ExcludesMqttTopicPrefix()
    {
        var catalog = new DashboardEventFilterCatalog();
        var widget = new DashboardWidgetSnapshot(
            "received",
            DashboardWidgetCatalog.EventCounterType,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [DashboardEventFilterCatalog.EventTypeKey] = FluxMqEventTypes.MqttMessageReceived,
                [DashboardEventFilterCatalog.TopicStartsWithKey] = string.Empty,
                [DashboardEventFilterCatalog.TopicNotStartsWithKey] = "$SYS/",
                [DashboardEventFilterCatalog.StatusKey] = "received"
            });

        catalog.Matches(
            widget,
            Event(
                FluxMqEventTypes.MqttMessageReceived,
                topic: "test",
                status: "received")).ShouldBeTrue();
        catalog.Matches(
            widget,
            Event(
                FluxMqEventTypes.MqttMessageReceived,
                topic: "$SYS/broker/bytes/sent",
                status: "received")).ShouldBeFalse();
    }

    private static FlowEvent Event(
        string type,
        string? topic = null,
        string? subject = null,
        string? status = null,
        IReadOnlyDictionary<string, string>? attributes = null)
        => new()
        {
            Timestamp = DateTimeOffset.Parse("2026-05-25T10:00:00Z"),
            Type = type,
            Source = "test",
            Channel = topic,
            Subject = subject,
            Status = status,
            Attributes = attributes ?? new Dictionary<string, string>(StringComparer.Ordinal)
        };
}
