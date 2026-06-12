using FluxMq.UI.Models;

namespace FluxMq.UI.Services;

public interface IDashboardMetricVisualizationModuleProvider
{
    string Id { get; }

    DashboardMetricVisualizationModule CreateModule();
}

public static class DashboardMetricVisualizationCatalog
{
    private static readonly IReadOnlyList<IDashboardMetricVisualizationModuleProvider> Providers =
        Array.AsReadOnly<IDashboardMetricVisualizationModuleProvider>(
        [
            new DashboardMetricValueVisualizationModuleProvider(),
            new DashboardMetricDigitalVisualizationModuleProvider(),
            new DashboardMetricGaugeVisualizationModuleProvider()
        ]);

    private static readonly IReadOnlyList<DashboardMetricVisualizationModule> Modules =
        Array.AsReadOnly(Providers.Select(static provider => provider.CreateModule()).ToArray());

    public static IReadOnlyList<IDashboardMetricVisualizationModuleProvider> CreateProviders()
        => Providers;

    public static IReadOnlyList<DashboardMetricVisualizationModule> CreateModules()
        => Modules;

    public static DashboardMetricVisualizationModule? Find(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var normalized = id.Trim();
        return CreateModules().FirstOrDefault(module =>
            string.Equals(module.Id, normalized, StringComparison.Ordinal));
    }
}
