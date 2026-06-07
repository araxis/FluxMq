using FluxMq.App.Metrics;
using FluxMq.UI.Models;

namespace FluxMq.UI.Services;

public sealed record DashboardMetricQueryFilterChip(string Key, string Label);

public static class DashboardMetricQuerySummary
{
    public static string Describe(
        DashboardMetricQueryDefinition query,
        DashboardEventFilterCatalog eventFilters,
        DashboardMetricRegistry metricRegistry)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(eventFilters);
        ArgumentNullException.ThrowIfNull(metricRegistry);

        var sentence = DescribeSentence(query, eventFilters, metricRegistry);
        return sentence.StartsWith("Show ", StringComparison.Ordinal)
            ? sentence["Show ".Length..]
            : sentence;
    }

    public static string DescribeSentence(
        DashboardMetricQueryDefinition query,
        DashboardEventFilterCatalog eventFilters,
        DashboardMetricRegistry metricRegistry)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(eventFilters);
        ArgumentNullException.ThrowIfNull(metricRegistry);

        _ = eventFilters;
        _ = metricRegistry;
        return FluxMetricQuerySummary.DescribeSentence(
            DashboardMetricQueryMapper.ToFluxMetricDefinition("metric", query));
    }

    public static IReadOnlyList<string> ActiveFilters(
        DashboardMetricQueryDefinition query,
        DashboardEventFilterCatalog eventFilters)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(eventFilters);

        return [.. ActiveFilterChips(query, eventFilters).Select(static filter => filter.Label)];
    }

    public static IReadOnlyList<DashboardMetricQueryFilterChip> ActiveFilterChips(
        DashboardMetricQueryDefinition query,
        DashboardEventFilterCatalog eventFilters)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(eventFilters);

        _ = eventFilters;
        return
        [
            .. FluxMetricQuerySummary
                .ActiveFilterChips(DashboardMetricQueryMapper.ToFluxMetricDefinition("metric", query))
                .Select(static chip => new DashboardMetricQueryFilterChip(chip.Key, chip.Label))
        ];
    }

    public static string WindowLabel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "1m";
        }

        return FluxMetricQuerySummary.WindowLabel(value);
    }
}
