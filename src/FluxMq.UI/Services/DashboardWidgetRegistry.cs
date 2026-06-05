using FluxMq.UI.Models;

namespace FluxMq.UI.Services;

public sealed class DashboardWidgetRegistry(DashboardWidgetCatalog catalog)
{
    private readonly DashboardWidgetCatalog _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));

    public IReadOnlyList<DashboardWidgetDescriptor> Widgets => _catalog.Widgets;

    public DashboardWidgetDescriptor? Find(string? type)
        => string.IsNullOrWhiteSpace(type) ? null : _catalog.Find(type);

    public DashboardWidgetDescriptor Describe(string? type)
        => Find(type) ?? new DashboardWidgetDescriptor(
            type ?? string.Empty,
            string.IsNullOrWhiteSpace(type) ? "Unknown Widget" : type,
            "Custom",
            "Custom dashboard widget.",
            MudBlazor.Icons.Material.Filled.Extension,
            "Custom",
            DashboardWidgetRendererKind.Unknown,
            DashboardWidgetEditorKind.Basic,
            []);

    public IReadOnlyDictionary<string, string> CreateDefaultConfiguration(string type)
        => FlowDashboardDefinitionFactory
            .CreateWidgetConfiguration(type)
            .ToDictionary(
                static item => item.Key,
                static item => item.Value?.ToJsonString() ?? string.Empty,
                StringComparer.Ordinal);
}
