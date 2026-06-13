namespace FluxMq.App.Metrics;

/// <summary>
/// Naming helpers for metric resource ids and dashboard-scoped ids.
/// </summary>
public static class FluxMetricNaming
{
    public static string ToArtifactId(string value)
    {
        var normalized = new string((value ?? string.Empty)
            .Trim()
            .Select(static c => char.IsLetterOrDigit(c) || c is '.' or '_' or '-' ? c : '-')
            .ToArray())
            .Trim('.', '_', '-');
        return string.IsNullOrWhiteSpace(normalized) ? "metric" : normalized;
    }

    public static string ToDashboardScopedId(string dashboardName, string metricName)
    {
        var dashboardId = ToArtifactId(dashboardName);
        var metricId = ToArtifactId(metricName);
        var prefix = $"{dashboardId}.";
        return metricId.StartsWith(prefix, StringComparison.Ordinal)
            ? metricId
            : ToArtifactId($"{dashboardId}.{metricId}");
    }

    public static string RemoveDashboardScope(string dashboardName, string metricName)
    {
        var dashboardId = ToArtifactId(dashboardName);
        var metricId = ToArtifactId(metricName);
        var prefix = $"{dashboardId}.";
        return metricId.StartsWith(prefix, StringComparison.Ordinal) && metricId.Length > prefix.Length
            ? metricId[prefix.Length..]
            : metricId;
    }

    public static bool HasDashboardScope(string dashboardName, string metricName)
    {
        var dashboardId = ToArtifactId(dashboardName);
        var metricId = ToArtifactId(metricName);
        return metricId.StartsWith($"{dashboardId}.", StringComparison.Ordinal);
    }

    public static string ToDisplayName(string value)
    {
        var text = (value ?? string.Empty)
            .Replace('.', ' ')
            .Replace('_', ' ')
            .Replace('-', ' ')
            .Trim();
        return string.IsNullOrWhiteSpace(text) ? "Metric" : text;
    }
}
