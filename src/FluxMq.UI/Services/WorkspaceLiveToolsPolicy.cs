using FluxMq.UI.Models;

namespace FluxMq.UI.Services;

public static class WorkspaceLiveToolsPolicy
{
    public static bool CanUseLiveTools(FlowWorkspaceService? project)
        => project?.ActiveArtifactKind is WorkspaceArtifactKind.Pipeline or WorkspaceArtifactKind.Dashboard;
}
