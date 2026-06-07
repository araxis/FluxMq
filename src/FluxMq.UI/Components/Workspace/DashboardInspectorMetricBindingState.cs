namespace FluxMq.UI.Components.Workspace;

public static class DashboardInspectorMetricBindingState
{
    public static string? NormalizeSelection(string? value, string? fallback)
    {
        var normalized = Normalize(value);
        return string.IsNullOrWhiteSpace(normalized) ? Normalize(fallback) : normalized;
    }

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

    public static List<string> EnsurePrimary(
        IEnumerable<string> metrics,
        string? primaryMetric,
        bool supportsSlots)
    {
        var primary = Normalize(primaryMetric);
        if (!supportsSlots)
        {
            return string.IsNullOrWhiteSpace(primary) ? [] : [primary];
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

        return normalized;
    }

    public static bool TryAdd(List<string> metrics, string? metricToAdd)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        var normalized = Normalize(metricToAdd);
        if (string.IsNullOrWhiteSpace(normalized) ||
            metrics.Contains(normalized, StringComparer.Ordinal))
        {
            return false;
        }

        metrics.Add(normalized);
        return true;
    }

    public static string? Remove(
        List<string> metrics,
        string? primaryMetric,
        string metricName)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        if (metrics.Count <= 1)
        {
            return primaryMetric;
        }

        metrics.RemoveAll(metric => string.Equals(metric, metricName, StringComparison.Ordinal));
        return string.Equals(primaryMetric, metricName, StringComparison.Ordinal)
            ? metrics.FirstOrDefault()
            : primaryMetric;
    }

    public static bool TryMove(List<string> metrics, string metricName, int offset)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        var index = metrics.FindIndex(metric => string.Equals(metric, metricName, StringComparison.Ordinal));
        if (index < 0)
        {
            return false;
        }

        var next = Math.Clamp(index + offset, 0, metrics.Count - 1);
        if (next == index)
        {
            return false;
        }

        metrics.RemoveAt(index);
        metrics.Insert(next, metricName);
        return true;
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
