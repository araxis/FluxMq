using FluxMq.App.Metrics;
using FluxMq.UI.Models;

namespace FluxMq.UI.Services;

public sealed record DashboardMetricQueryPreview(
    DashboardMetricValue Value,
    bool IsLive,
    int WindowEventCount,
    int TotalMatchCount,
    string EmptyReason,
    IReadOnlyList<string> ActiveFilters,
    DashboardEventSnapshot Snapshot)
{
    public string SourceLabel => IsLive ? "Live" : "Sample";

    public int MatchingEventCount => WindowEventCount;
}

public static class DashboardMetricQueryPreviewFactory
{
    private static readonly FluxMetricPreviewService PreviewService = new();

    public static DashboardMetricQueryPreview Create(
        DashboardMetricQueryDefinition query,
        DashboardWidgetSnapshot widget,
        FlowWorkspaceService project,
        DashboardMetricRegistry metricRegistry,
        DashboardEventFilterCatalog eventFilters,
        bool useSampleFallback = true)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(widget);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(metricRegistry);
        ArgumentNullException.ThrowIfNull(eventFilters);

        _ = metricRegistry;
        _ = eventFilters;

        var definition = DashboardMetricQueryMapper.ToFluxMetricDefinition(widget.Name, query);
        var queryWidget = CreateQueryWidget(widget, query);
        var liveSnapshot = DashboardMetricQueryMapper.ToFluxMetricRuntimeSnapshot(project.GetDashboardEventSnapshot(queryWidget));
        var preview = PreviewService.Create(
            definition,
            liveSnapshot,
            () =>
            {
                var sample = DashboardPreviewSampleFactory.Create(queryWidget);
                return DashboardMetricQueryMapper.ToFluxMetricRuntimeSnapshot(sample.Snapshot);
            },
            useSampleFallback);

        return new DashboardMetricQueryPreview(
            DashboardMetricQueryMapper.ToDashboardMetricValue(preview.Value),
            preview.IsLive,
            preview.WindowEventCount,
            preview.TotalMatchCount,
            preview.EmptyReason,
            preview.ActiveFilters,
            DashboardMetricQueryMapper.ToDashboardEventSnapshot(preview.Snapshot));
    }

    public static DashboardWidgetSnapshot CreateQueryWidget(
        DashboardWidgetSnapshot widget,
        DashboardMetricQueryDefinition query)
    {
        var configuration = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["title"] = widget.ReadString("title") ?? "Metric preview",
            [DashboardEventFilterCatalog.EventTypeKey] = query.EventType ?? string.Empty,
            [DashboardEventFilterCatalog.TopicStartsWithKey] = query.TopicStartsWith ?? string.Empty,
            [DashboardEventFilterCatalog.TopicNotStartsWithKey] = query.TopicNotStartsWith ?? string.Empty,
            [DashboardEventFilterCatalog.StatusKey] = query.Status ?? string.Empty
        };

        foreach (var (key, value) in query.AdditionalFilters)
        {
            configuration[key] = value;
        }

        return widget with
        {
            Name = $"{widget.Name}QueryPreview",
            Configuration = configuration
        };
    }
}
