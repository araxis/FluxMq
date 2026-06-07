using FluxMq.App.Metrics;
using FluxMq.UI.Models;

namespace FluxMq.UI.Services;

public static class DashboardMetricReferenceResolver
{
    public static FluxMetricDefinition? ResolveAppMetricDefinition(
        FlowWorkspaceService project,
        string? metricName,
        IReadOnlyDictionary<string, string>? parameterValues = null)
    {
        ArgumentNullException.ThrowIfNull(project);

        if (string.IsNullOrWhiteSpace(metricName))
        {
            return null;
        }

        var normalized = metricName.Trim();
        var artifact = project.GetMetricArtifact(normalized);
        return artifact is null
            ? null
            : new FluxMetricResolver().Resolve(artifact, parameterValues);
    }

    public static DashboardMetricSnapshot? ResolveAppMetricSnapshot(
        FlowWorkspaceService project,
        string? metricName,
        IReadOnlyDictionary<string, string>? parameterValues = null)
    {
        var definition = ResolveAppMetricDefinition(project, metricName, parameterValues);
        return definition is null || string.IsNullOrWhiteSpace(metricName)
            ? null
            : DashboardMetricQueryMapper.ToDashboardMetricSnapshot(
                metricName.Trim(),
                DashboardMetricQueryMapper.ToDashboardQuery(definition));
    }
}
