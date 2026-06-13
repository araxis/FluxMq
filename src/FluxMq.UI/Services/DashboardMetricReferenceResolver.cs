using FluxMq.App.Metrics;
using FluxMq.UI.Models;

namespace FluxMq.UI.Services;

public static class DashboardMetricReferenceResolver
{
    public static FluxMetricResourceDefinition? ResolveAppMetric(
        FlowWorkspaceService project,
        string? metricName,
        IReadOnlyDictionary<string, string>? parameterValues = null)
    {
        ArgumentNullException.ThrowIfNull(project);

        if (string.IsNullOrWhiteSpace(metricName))
        {
            return null;
        }

        var resource = project.GetMetricResource(metricName.Trim());
        if (resource is null)
        {
            return null;
        }

        return resource with { Parameters = Merge(resource.Parameters, parameterValues) };
    }

    public static DashboardMetricSnapshot? ResolveAppMetricSnapshot(
        FlowWorkspaceService project,
        string? metricName,
        IReadOnlyDictionary<string, string>? parameterValues = null)
    {
        var resource = ResolveAppMetric(project, metricName, parameterValues);
        return resource is null || string.IsNullOrWhiteSpace(metricName)
            ? null
            : FlowDefinitionComposer.ResourceToDashboardMetricSnapshot(metricName.Trim(), resource);
    }

    private static Dictionary<string, string> Merge(
        IReadOnlyDictionary<string, string> parameters,
        IReadOnlyDictionary<string, string>? overrides)
    {
        var merged = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in parameters)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                merged[key] = value;
            }
        }

        if (overrides is not null)
        {
            foreach (var (key, value) in overrides)
            {
                if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
                {
                    merged[key.Trim()] = value.Trim();
                }
            }
        }

        return merged;
    }
}
