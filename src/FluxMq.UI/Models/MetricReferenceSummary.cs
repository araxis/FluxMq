namespace FluxMq.UI.Models;

public sealed record MetricReferenceSummary(
    string DashboardName,
    string WidgetName,
    string WidgetType,
    bool IsPrimary,
    string? CellName = null,
    string? CellLabel = null);
