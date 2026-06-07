using FluxMq.App.Metrics;
using FluxMq.Core.Metrics;
using FluxMq.UI.Models;

namespace FluxMq.UI.Services;

internal sealed class DashboardMetricValueBridge(FlowDefinitionComposer definitionComposer)
{
    private static readonly TimeSpan DefaultRateWindow = TimeSpan.FromMinutes(1);
    private readonly DashboardMetricRegistry _dashboardMetricRegistry = new();
    private readonly FluxMetricResolver _metricResolver = new();

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
        var metric = ResolveMetric(widget, layout, definitionJson);
        if (metric is null)
        {
            return widget;
        }

        rateWindow = ParseWindow(metric.Window);
        var configuration = new Dictionary<string, string>(widget.Configuration, StringComparer.Ordinal);
        foreach (var filter in metric.Filters)
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
        if (TryGetStreamValue(widget, layout, definitionJson, tryGetReading, out var readingValue))
        {
            return readingValue;
        }

        var snapshot = snapshotFactory(widget);
        var metric = ResolveMetric(widget, layout, definitionJson);
        if (metric is null)
        {
            return DashboardMetricRegistry.EvaluateLegacyMetric(
                DashboardWidgetCatalog.NormalizePrimaryMetric(
                    widget.ReadString(DashboardWidgetCatalog.PrimaryMetricKey)),
                snapshot);
        }

        return _dashboardMetricRegistry.Evaluate(ToDashboardMetricQuery(metric), snapshot);
    }

    private DashboardMetricSnapshot? ResolveMetric(
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

        var appMetric = definitionComposer.GetMetricArtifact(definitionJson, metricName);
        if (appMetric is not null)
        {
            var validation = _metricResolver.ValidateReference(appMetric, binding.Options);
            if (!validation.IsValid)
            {
                return null;
            }

            return ToDashboardMetricSnapshot(metricName, _metricResolver.Resolve(appMetric, binding.Options));
        }

        return layout.Metrics.TryGetValue(metricName, out var metric)
            ? metric
            : null;
    }

    private bool TryGetStreamValue(
        DashboardWidgetSnapshot widget,
        DashboardLayoutSnapshot? layout,
        string definitionJson,
        TryGetMetricReading tryGetReading,
        out DashboardMetricValue value)
    {
        value = default!;
        if (layout is null ||
            !layout.Bindings.TryGetValue(widget.Name, out var binding))
        {
            return false;
        }

        var metricName = PrimaryMetricName(binding);
        if (string.IsNullOrWhiteSpace(metricName))
        {
            return false;
        }

        var appMetric = definitionComposer.GetMetricArtifact(definitionJson, metricName);
        if (appMetric is null ||
            !tryGetReading(metricName, binding.Options, out var reading))
        {
            return false;
        }

        var definition = _metricResolver.Resolve(appMetric, binding.Options);
        var measure = FluxMetricCatalog.Shared.FindMeasure(definition.Measure);
        value = new DashboardMetricValue(
            measure.Label,
            reading.Value,
            measure.Unit,
            FluxMetricEvaluationEngine.FormatValue(reading.Value, measure.Unit, definition.Format));
        return true;
    }

    private static string? PrimaryMetricName(DashboardWidgetBindingSnapshot binding)
        => string.IsNullOrWhiteSpace(binding.PrimaryMetric)
            ? binding.Metrics.FirstOrDefault()
            : binding.PrimaryMetric;

    private static DashboardMetricSnapshot ToDashboardMetricSnapshot(string metricName, FluxMetricDefinition definition)
    {
        var filters = new Dictionary<string, string>(definition.AdditionalFilters, StringComparer.Ordinal);
        AddIfPresent(filters, DashboardEventFilterCatalog.EventTypeKey, definition.EventType);
        AddIfPresent(filters, DashboardEventFilterCatalog.TopicStartsWithKey, definition.TopicStartsWith);
        AddIfPresent(filters, DashboardEventFilterCatalog.TopicNotStartsWithKey, definition.TopicNotStartsWith);
        AddIfPresent(filters, DashboardEventFilterCatalog.StatusKey, definition.Status);

        return new DashboardMetricSnapshot(
            metricName,
            definition.Source,
            definition.Measure,
            definition.Window,
            definition.GroupBy,
            filters,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["unit"] = definition.Format
            });
    }

    private static void AddIfPresent(IDictionary<string, string> target, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            target[key] = value.Trim();
        }
    }

    private static DashboardMetricQueryDefinition ToDashboardMetricQuery(DashboardMetricSnapshot metric)
        => new(
            metric.Source,
            metric.Aggregation,
            metric.Window,
            EventType: metric.ReadFilter(DashboardEventFilterCatalog.EventTypeKey),
            TopicStartsWith: metric.ReadFilter(DashboardEventFilterCatalog.TopicStartsWithKey),
            TopicNotStartsWith: metric.ReadFilter(DashboardEventFilterCatalog.TopicNotStartsWithKey),
            Status: metric.ReadFilter(DashboardEventFilterCatalog.StatusKey),
            GroupBy: metric.GroupBy,
            Format: metric.ReadFormat("unit") ?? "number",
            AdditionalFilters: metric.Filters
                .Where(static filter =>
                    !string.Equals(filter.Key, DashboardEventFilterCatalog.EventTypeKey, StringComparison.Ordinal) &&
                    !string.Equals(filter.Key, DashboardEventFilterCatalog.TopicStartsWithKey, StringComparison.Ordinal) &&
                    !string.Equals(filter.Key, DashboardEventFilterCatalog.TopicNotStartsWithKey, StringComparison.Ordinal) &&
                    !string.Equals(filter.Key, DashboardEventFilterCatalog.StatusKey, StringComparison.Ordinal))
                .ToDictionary(static filter => filter.Key, static filter => filter.Value, StringComparer.Ordinal));

    private static TimeSpan ParseWindow(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DefaultRateWindow;
        }

        var normalized = value.Trim();
        var suffix = normalized[^1];
        var numberText = char.IsLetter(suffix)
            ? normalized[..^1]
            : normalized;
        if (!double.TryParse(numberText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var amount) ||
            amount <= 0)
        {
            return DefaultRateWindow;
        }

        var window = char.ToLowerInvariant(suffix) switch
        {
            'h' => TimeSpan.FromHours(amount),
            'm' => TimeSpan.FromMinutes(amount),
            _ => TimeSpan.FromSeconds(amount)
        };

        return TimeSpan.FromSeconds(Math.Clamp(window.TotalSeconds, 1, 86_400));
    }
}
