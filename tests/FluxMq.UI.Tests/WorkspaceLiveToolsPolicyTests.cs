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
    [InlineData(WorkspaceArtifactKind.Pipeline, true)]
    [InlineData(WorkspaceArtifactKind.Dashboard, true)]
    [InlineData(WorkspaceArtifactKind.Test, false)]
    [InlineData(WorkspaceArtifactKind.Topics, false)]
    [InlineData(WorkspaceArtifactKind.Logs, false)]
    public void CanUseLiveTools_FollowsArtifactKind(WorkspaceArtifactKind artifactKind, bool expected)
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

        WorkspaceLiveToolsPolicy.CanUseLiveTools(project).ShouldBe(expected);
    }
}
