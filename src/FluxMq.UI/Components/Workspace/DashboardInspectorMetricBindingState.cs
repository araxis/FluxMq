namespace FluxMq.UI.Components.Workspace;

public static class DashboardInspectorMetricBindingState
{
    public static List<string> Initialize(
        IReadOnlyList<string>? metrics,
        string? primaryMetric,
        bool supportsSlots)
    {
        var primary = Normalize(primaryMetric);
        if (!supportsSlots)
        {
            return string.IsNullOrWhiteSpace(primary) ? [] : [primary];
        }

        var normalized = (metrics ?? [])
            .Where(static metric => !string.IsNullOrWhiteSpace(metric))
            .Select(static metric => metric.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (!string.IsNullOrWhiteSpace(primary) &&
            !normalized.Contains(primary, StringComparer.Ordinal))
        {
            normalized.Insert(0, primary);
        }

        if (normalized.Count == 0 && !string.IsNullOrWhiteSpace(primary))
        {
            normalized.Add(primary);
        }

        return normalized;
    }

    public static IReadOnlyList<string> Current(
        bool supportsSlots,
        IEnumerable<string> metrics,
        string? primaryMetric,
        string? fallbackMetric)
    {
        var primary = Normalize(primaryMetric);
        var fallback = Normalize(fallbackMetric);
        if (!supportsSlots)
        {
            var metric = string.IsNullOrWhiteSpace(primary) ? fallback : primary;
            return string.IsNullOrWhiteSpace(metric) ? [] : [metric];
        }

        var normalized = metrics
            .Where(static metric => !string.IsNullOrWhiteSpace(metric))
            .Select(static metric => metric.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (!string.IsNullOrWhiteSpace(primary) &&
            !normalized.Contains(primary, StringComparer.Ordinal))
        {
            normalized.Insert(0, primary);
        }

        if (normalized.Count == 0 && !string.IsNullOrWhiteSpace(fallback))
        {
            normalized.Add(fallback);
        }

        return normalized;
    }

    private static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}
