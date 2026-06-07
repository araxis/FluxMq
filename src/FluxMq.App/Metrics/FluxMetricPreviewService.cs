namespace FluxMq.App.Metrics;

public sealed record FluxMetricPreview(
    FluxMetricValue Value,
    bool IsLive,
    int WindowEventCount,
    int TotalMatchCount,
    string EmptyReason,
    IReadOnlyList<string> ActiveFilters,
    FluxMetricRuntimeSnapshot Snapshot)
{
    public string SourceLabel => IsLive ? "Live" : "Sample";

    public int MatchingEventCount => WindowEventCount;
}

public sealed class FluxMetricPreviewService
{
    private readonly FluxMetricEvaluationEngine _engine;
    private readonly FluxMetricCatalog _catalog;

    public FluxMetricPreviewService(
        FluxMetricEvaluationEngine? engine = null,
        FluxMetricCatalog? catalog = null)
    {
        _catalog = catalog ?? FluxMetricCatalog.Shared;
        _engine = engine ?? new FluxMetricEvaluationEngine(_catalog);
    }

    public FluxMetricPreview Create(
        FluxMetricDefinition definition,
        FluxMetricRuntimeSnapshot liveSnapshot,
        Func<FluxMetricRuntimeSnapshot> sampleSnapshotFactory,
        bool useSampleFallback = true)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(liveSnapshot);
        ArgumentNullException.ThrowIfNull(sampleSnapshotFactory);

        var filters = FluxMetricQuerySummary.ActiveFilters(definition, _catalog);
        if (liveSnapshot.RecentCount > 0 || !useSampleFallback)
        {
            return new FluxMetricPreview(
                _engine.Evaluate(definition, liveSnapshot),
                IsLive: true,
                liveSnapshot.RecentCount,
                liveSnapshot.Count,
                liveSnapshot.RecentCount == 0 ? "No live events match this query in the selected window." : string.Empty,
                filters,
                liveSnapshot);
        }

        var sampleSnapshot = sampleSnapshotFactory();
        return new FluxMetricPreview(
            _engine.Evaluate(definition, sampleSnapshot),
            IsLive: false,
            sampleSnapshot.RecentCount,
            sampleSnapshot.Count,
            "No live events match this query in the selected window. Showing generated sample data.",
            filters,
            sampleSnapshot);
    }
}
