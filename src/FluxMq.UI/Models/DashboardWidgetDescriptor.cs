namespace FluxMq.UI.Models;

public sealed record DashboardWidgetDescriptor(
    string Type,
    string DisplayName,
    string Category,
    string Description,
    string Icon);
