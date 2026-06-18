using FluxMq.UI.Models;
using Shouldly;

namespace FluxMq.UI.Tests;

public sealed class WorkspaceLogFilterTests
{
    [Fact]
    public void Apply_FiltersByScope()
    {
        var logs = new[]
        {
            Log("app", scope: WorkspaceLogScopes.App),
            Log("test", scope: WorkspaceLogScopes.TestRunner),
            Log("system", scope: WorkspaceLogScopes.System)
        };

        var filtered = WorkspaceLogFilter.Apply(
            logs,
            new WorkspaceLogQuery(Scope: WorkspaceLogScopes.TestRunner));

        filtered.ShouldHaveSingleItem().Message.ShouldBe("test");
    }

    [Fact]
    public void Apply_FiltersBySeverity()
    {
        var logs = new[]
        {
            Log("info", severity: "Info"),
            Log("warn", severity: "Warn"),
            Log("error", severity: "Error")
        };

        var filtered = WorkspaceLogFilter.Apply(logs, new WorkspaceLogQuery(Severity: "Warning"));

        filtered.ShouldHaveSingleItem().Message.ShouldBe("warn");
    }

    [Fact]
    public void Apply_FiltersByProblems()
    {
        var logs = new[]
        {
            Log("info", severity: "Info"),
            Log("warn", severity: "Warn"),
            Log("critical", severity: "Critical"),
            Log("error", severity: "Error"),
            Log("trace", severity: "Trace")
        };

        var filtered = WorkspaceLogFilter.Apply(logs, new WorkspaceLogQuery(Severity: WorkspaceLogFilter.Problems));

        filtered.Select(log => log.Message).ShouldBe(["error", "critical", "warn"]);
    }

    [Fact]
    public void Apply_SearchesMetadataAndKeepsNewestFirst()
    {
        var logs = new[]
        {
            Log("old", context: "topic=alpha"),
            Log("middle", artifactName: "pip2"),
            Log("new", source: "MqttPublisher", context: "topic=beta")
        };

        var filtered = WorkspaceLogFilter.Apply(logs, new WorkspaceLogQuery(Search: "topic"));

        filtered.Select(log => log.Message).ShouldBe(["new", "old"]);
    }

    private static WorkspaceLogEntry Log(
        string message,
        string severity = "Info",
        string source = "Source",
        string scope = WorkspaceLogScopes.App,
        string? context = null,
        string? artifactName = null)
        => new(
            DateTimeOffset.UtcNow,
            severity,
            source,
            "Code",
            message,
            artifactName,
            null,
            null,
            context,
            scope,
            artifactName is null ? null : WorkspaceLogArtifactKinds.Pipeline,
            artifactName);
}
