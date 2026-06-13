using FluxMq.App.Metrics;
using FluxMq.Core.Metrics;
using FluxMq.UI.Models;

namespace FluxMq.UI.Services;

internal sealed class DashboardMetricValueBridge(FlowDefinitionComposer definitionComposer)
{
    // Fallback rate window for widgets with no bound metric and no inline "window" key
    // (e.g. payload/breakdown/table/latest/topic-activity, which have no window editor).
    // Intentionally 1 minute — the same window those widgets used before the inline rework.
    private static readonly TimeSpan DefaultRateWindow = TimeSpan.FromMinutes(1);
    private readonly DashboardMetricRegistry _dashboardMetricRegistry = new();

    public delegate bool TryGetMetricReading(
        string? metricId,
        IReadOnlyDictionary<string, string>? parameters,
        out FluxMetricReading<double> reading);

    public DashboardWidgetSnapshot ResolveMetricWidget(
        DashboardWidgetSnapshot widget,
        DashboardLayoutSnapshot? layout,
        string definitionJson,
        out TimeSpan rateWindow)
    {
        rateWindow = DefaultRateWindow;
        var resolved = ResolveMetric(widget, layout, definitionJson);
        if (resolved is null)
        {
            // Inline event widgets (charts/topic/payload/breakdowns/table/latest/activity) carry their
            // own window in the widget configuration instead of binding to a metric resource.
            if (widget.ReadString(DashboardWidgetCatalog.WindowKey) is { } inlineWindow &&
                !string.IsNullOrWhiteSpace(inlineWindow))
            {
                rateWindow = MetricWindow.Parse(inlineWindow);
            }

            return widget;
        }

        rateWindow = MetricWindow.Parse(resolved.Snapshot.Window);
        var configuration = new Dictionary<string, string>(widget.Configuration, StringComparer.Ordinal);
        foreach (var filter in resolved.Snapshot.Filters)
        {
            configuration[filter.Key] = filter.Value;
        }

        return widget with { Configuration = configuration };
    }

    public DashboardMetricValue GetMetricValue(
        DashboardWidgetSnapshot widget,
        DashboardLayoutSnapshot? layout,
        string definitionJson,
        Func<DashboardWidgetSnapshot, DashboardEventSnapshot> snapshotFactory,
        TryGetMetricReading tryGetReading)
    {
        var resolved = ResolveMetric(widget, layout, definitionJson);
        if (resolved is not null &&
            tryGetReading(resolved.MetricName, resolved.Parameters, out var reading))
        {
            return _dashboardMetricRegistry.Format(resolved.TypeId, reading.Value);
        }

        var snapshot = snapshotFactory(widget);
        if (resolved is null)
        {
            return DashboardMetricRegistry.EvaluateLegacyMetric(
                DashboardWidgetCatalog.NormalizePrimaryMetric(
                    widget.ReadString(DashboardWidgetCatalog.PrimaryMetricKey)),
                snapshot);
        }

        return _dashboardMetricRegistry.Evaluate(resolved.TypeId, snapshot);
    }

    private ResolvedMetric? ResolveMetric(
        DashboardWidgetSnapshot widget,
        DashboardLayoutSnapshot? layout,
        string definitionJson)
    {
        if (layout is null ||
            !layout.Bindings.TryGetValue(widget.Name, out var binding))
        {
            return null;
        }

        var metricName = PrimaryMetricName(binding);
        if (string.IsNullOrWhiteSpace(metricName))
        {
            return null;
        }

        var resource = definitionComposer.GetMetricResource(definitionJson, metricName);
        if (resource is not null)
        {
            var merged = Merge(resource.Parameters, binding.Options);
            var snapshot = FlowDefinitionComposer.ResourceToDashboardMetricSnapshot(metricName, resource with { Parameters = merged });
            return new ResolvedMetric(metricName, resource.TypeId, merged, snapshot);
        }

        if (layout.Metrics.TryGetValue(metricName, out var legacy))
        {
            return new ResolvedMetric(
                metricName,
                DashboardMetricRegistry.TypeForMeasure(legacy.Aggregation),
                binding.Options,
                legacy);
        }

        return null;
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

    private static string? PrimaryMetricName(DashboardWidgetBindingSnapshot binding)
        => string.IsNullOrWhiteSpace(binding.PrimaryMetric)
            ? binding.Metrics.FirstOrDefault()
            : binding.PrimaryMetric;

    private sealed record ResolvedMetric(
        string MetricName,
        string TypeId,
        IReadOnlyDictionary<string, string> Parameters,
        DashboardMetricSnapshot Snapshot);
}
