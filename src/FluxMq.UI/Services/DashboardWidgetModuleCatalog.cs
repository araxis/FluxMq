using FluxMq.UI.Models;

namespace FluxMq.UI.Services;

public interface IDashboardWidgetModuleProvider
{
    string Id { get; }

    IReadOnlyList<DashboardWidgetModule> CreateModules();
}

public static class DashboardWidgetModuleCatalog
{
    public static IReadOnlyList<DashboardWidgetModule> CreateModules()
        => [.. CreateProviders().SelectMany(static provider => provider.CreateModules())];

    public static IReadOnlyList<IDashboardWidgetModuleProvider> CreateProviders()
        =>
        [
            new DashboardMetricWidgetModuleProvider(),
            new DashboardEventWidgetModuleProvider(),
            new DashboardChartWidgetModuleProvider(),
            new DashboardMqttOpsWidgetModuleProvider(),
            new DashboardTopicWidgetModuleProvider()
        ];

    public static DashboardWidgetModule? Find(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return null;
        }

        var normalized = type.Trim();
        return CreateModules().FirstOrDefault(module =>
            string.Equals(module.Type, normalized, StringComparison.Ordinal) ||
            module.CompatibilityTypeIds.Contains(normalized, StringComparer.Ordinal));
    }
}
