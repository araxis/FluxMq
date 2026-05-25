using FluxMq.Pipeline.Components;
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
        var fileWritten = catalog.Find(FlowEventTypes.FileWritten);
        var assertion = catalog.Find(FlowEventTypes.AssertionEvaluated);

        any.Fields.ShouldBeEmpty();

        var fileField = fileWritten.Fields.ShouldHaveSingleItem();
        fileField.Key.ShouldBe(DashboardEventFilterCatalog.SubjectStartsWithKey);
        fileField.Label.ShouldBe("Path prefix");

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
                [DashboardEventFilterCatalog.EventTypeKey] = FlowEventTypes.FileWritten,
                [DashboardEventFilterCatalog.SubjectStartsWithKey] = "logs/",
                [DashboardEventFilterCatalog.TopicStartsWithKey] = string.Empty,
                [DashboardEventFilterCatalog.StatusKey] = "written"
            });

        catalog.Matches(widget, Event(FlowEventTypes.FileWritten, subject: "logs/a.json", status: "written")).ShouldBeTrue();
        catalog.Matches(widget, Event(FlowEventTypes.FileWritten, subject: "archive/a.json", status: "written")).ShouldBeFalse();
        catalog.Matches(widget, Event(FlowEventTypes.FileWritten, topic: "logs/a.json", subject: "archive/a.json", status: "written")).ShouldBeFalse();
    }

    private static FlowEvent Event(string type, string? topic = null, string? subject = null, string? status = null)
        => new()
        {
            Timestamp = DateTimeOffset.Parse("2026-05-25T10:00:00Z"),
            Type = type,
            Source = "test",
            Topic = topic,
            Subject = subject,
            Status = status
        };
}
