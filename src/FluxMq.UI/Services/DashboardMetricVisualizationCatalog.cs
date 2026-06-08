using FluxMq.UI.Models;

namespace FluxMq.UI.Services;

public interface IDashboardMetricVisualizationModuleProvider
{
    string Id { get; }

    DashboardMetricVisualizationModule CreateModule();
}

public static class DashboardMetricVisualizationCatalog
{
    public static IReadOnlyList<IDashboardMetricVisualizationModuleProvider> CreateProviders()
        =>
        [
            new DashboardMetricValueVisualizationModuleProvider(),
            new DashboardMetricDigitalVisualizationModuleProvider()
        ];

    public static IReadOnlyList<DashboardMetricVisualizationModule> CreateModules()
        => [.. CreateProviders().Select(static provider => provider.CreateModule())];

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
