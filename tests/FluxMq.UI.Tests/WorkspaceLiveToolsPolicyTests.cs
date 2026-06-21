using FluxMq.UI.Models;
using FluxMq.UI.Services;
using Shouldly;

namespace FluxMq.UI.Tests;

public sealed class WorkspaceLiveToolsPolicyTests
{
    [Fact]
    public void CanUseLiveTools_ReturnsFalseWithoutProject()
    {
        WorkspaceLiveToolsPolicy.CanUseLiveTools(null).ShouldBeFalse();
    }

    [Theory]
    [InlineData(WorkspaceArtifactKind.Pipeline)]
    [InlineData(WorkspaceArtifactKind.Dashboard)]
    [InlineData(WorkspaceArtifactKind.Test)]
    [InlineData(WorkspaceArtifactKind.Topics)]
    [InlineData(WorkspaceArtifactKind.Logs)]
    public void CanUseLiveTools_ReturnsTrueForAnyActiveProjectArtifact(WorkspaceArtifactKind artifactKind)
    {
        var project = new FlowWorkspaceService(new FlowDefinitionComposer());
        project.AddWorkflow("pipe");
        project.AddDashboard("dash");
        project.AddTest("test");

        switch (artifactKind)
        {
            case WorkspaceArtifactKind.Pipeline:
                project.SetActiveWorkflow("pipe");
                break;
            case WorkspaceArtifactKind.Dashboard:
                project.SetActiveDashboard("dash");
                break;
            case WorkspaceArtifactKind.Test:
                project.SetActiveTest("test");
                break;
            case WorkspaceArtifactKind.Topics:
                project.SetActiveTopics();
                break;
            case WorkspaceArtifactKind.Logs:
                project.SetActiveLogs();
                break;
        }

        WorkspaceLiveToolsPolicy.CanUseLiveTools(project).ShouldBeTrue();
    }
}
