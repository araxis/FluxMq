namespace FluxMq.UI.Components.Workspace;

public sealed record DashboardQueryBuilderOption(
    string Value,
    string Label,
    string? Description = null,
    bool Disabled = false);
