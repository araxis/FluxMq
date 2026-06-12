using FluxMq.UI.Models;

namespace FluxMq.UI.Services;

public interface IDashboardWidgetModuleProvider
{
    string Id { get; }

    IReadOnlyList<DashboardWidgetModule> CreateModules();
}

public static class DashboardWidgetModuleCatalog
{
    private static readonly IReadOnlyList<IDashboardWidgetModuleProvider> Providers =
        Array.AsReadOnly<IDashboardWidgetModuleProvider>(
        [
            new DashboardMetricWidgetModuleProvider(),
            new DashboardEventWidgetModuleProvider(),
            new DashboardChartWidgetModuleProvider(),
            new DashboardMqttOpsWidgetModuleProvider(),
            new DashboardTopicWidgetModuleProvider()
        ]);

    private static readonly IReadOnlyList<DashboardWidgetModule> Modules =
        Array.AsReadOnly(Providers.SelectMany(static provider => provider.CreateModules()).ToArray());

    public static IReadOnlyList<DashboardWidgetModule> CreateModules()
        => Modules;

    public static IReadOnlyList<IDashboardWidgetModuleProvider> CreateProviders()
        => Providers;

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

    public static string InstanceNamePrefixFor(string? type)
        => Find(DashboardWidgetCatalog.NormalizeWidgetTypeForAdd(type))?.InstanceNamePrefix ?? "widget";
}
