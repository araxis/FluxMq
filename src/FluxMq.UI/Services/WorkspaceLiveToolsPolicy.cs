using FluxMq.UI.Models;

namespace FluxMq.UI.Services;

public static class WorkspaceLiveToolsPolicy
{
    public static bool CanUseLiveTools(FlowWorkspaceService? project)
        => project is not null;
}
