using FluxMq.UI.Models;

namespace FluxMq.UI.Services;

public sealed class DashboardWidgetRegistry(DashboardWidgetCatalog catalog)
{
    private readonly DashboardWidgetCatalog _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    private readonly IReadOnlyList<DashboardWidgetModule> _modules = DashboardWidgetModuleCatalog.CreateModules();

    public IReadOnlyList<DashboardWidgetModule> Modules => _modules;

    public IReadOnlyList<DashboardWidgetDescriptor> Widgets
        => _modules.Select(static module => module.Descriptor).ToArray();

    public DashboardWidgetDescriptor? Find(string? type)
        => FindModule(type)?.Descriptor ??
           (string.IsNullOrWhiteSpace(type) ? null : _catalog.Find(type));

    public DashboardWidgetModule? FindModule(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return null;
        }

        var normalized = type.Trim();
        return _modules.FirstOrDefault(module =>
            string.Equals(module.Type, normalized, StringComparison.Ordinal) ||
            module.CompatibilityTypeIds.Contains(normalized, StringComparer.Ordinal));
    }

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
    {
        var normalized = DashboardWidgetCatalog.NormalizeWidgetTypeForAdd(type);
        var module = FindModule(normalized);
        if (module is not null)
        {
            return new Dictionary<string, string>(module.DefaultConfiguration, StringComparer.Ordinal);
        }

        return FlowDashboardDefinitionFactory
            .CreateWidgetConfiguration(normalized)
            .ToDictionary(
                static item => item.Key,
                static item => item.Value?.ToJsonString() ?? string.Empty,
                StringComparer.Ordinal);
    }
}
