using FluxFlow.Engine.Components;
using FluxMq.Core.Metrics;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.App.Metrics;

/// <summary>
/// A small runtime coordinator for metric streams. It only configures the resource set and event stream, starts
/// and stops sources, caches one source per (resource id + resolved parameters), and exposes the reading streams.
/// It never calculates metrics, parses filters, knows event-specific fields, or switches on metric behavior — the
/// metric types own all of that. Sources are resolved from <see cref="IFluxMetricTypeRegistry"/> by their type id.
/// </summary>
public sealed class FluxMetricStreamHost : IAsyncDisposable, IDisposable
{
    private readonly Lock _sync = new();
    private readonly IFluxMetricTypeRegistry _types;
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<MetricStreamKey, IFluxMetricSource<double>> _sources = new();
    private IReadOnlyDictionary<string, FluxMetricResourceDefinition> _resources =
        new Dictionary<string, FluxMetricResourceDefinition>(StringComparer.Ordinal);
    private ISourceBlock<FlowEvent>? _events;
    private bool _started;
    private bool _completed;

    public FluxMetricStreamHost(IFluxMetricTypeRegistry? types = null, TimeProvider? timeProvider = null)
    {
        _types = types ?? FluxMetricTypeRegistry.CreateDefault();
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public void Configure(
        IReadOnlyDictionary<string, FluxMetricResourceDefinition> resources,
        ISourceBlock<FlowEvent> events)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(events);

        lock (_sync)
        {
            CompleteLocked();
            _resources = new Dictionary<string, FluxMetricResourceDefinition>(resources, StringComparer.Ordinal);
            _events = events;
            _completed = false;
            _started = false;
        }
    }

    public ISourceBlock<FluxMetricReading<double>> GetNumberStream(
        string metricId,
        IReadOnlyDictionary<string, string>? parameterOverrides = null,
        int boundedCapacity = 1000)
        => GetNumberSource(metricId, parameterOverrides, boundedCapacity).Output;

    public bool TryGetLatestNumber(
        string metricId,
        IReadOnlyDictionary<string, string>? parameterOverrides,
        out FluxMetricReading<double> reading)
    {
        lock (_sync)
        {
            if (_events is not null &&
                !string.IsNullOrWhiteSpace(metricId) &&
                _resources.TryGetValue(metricId.Trim(), out var resource))
            {
                var key = MetricStreamKey.Create(metricId, MergeParameters(resource, parameterOverrides));
                if (_sources.TryGetValue(key, out var source) && source.Latest is { } latest)
                {
                    reading = latest;
                    return true;
                }
            }
        }

        reading = default!;
        return false;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        IFluxMetricSource<double>[] sources;
        lock (_sync)
        {
            if (_completed)
            {
                return;
            }

            _started = true;
            sources = _sources.Values.ToArray();
        }

        foreach (var source in sources)
        {
            await source.StartAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public void Complete()
    {
        lock (_sync)
        {
            CompleteLocked();
        }
    }

    public void Dispose() => Complete();

    public async ValueTask DisposeAsync()
    {
        IFluxMetricSource<double>[] sources;
        lock (_sync)
        {
            sources = _sources.Values.ToArray();
            _sources.Clear();
            _completed = true;
            _started = false;
            _events = null;
        }

        foreach (var source in sources)
        {
            source.Complete();
        }

        try
        {
            await Task.WhenAll(sources.Select(static source => source.Completion)).ConfigureAwait(false);
        }
        catch
        {
            // Runtime shutdown should continue even if one stream faults.
        }
    }

    private IFluxMetricSource<double> GetNumberSource(
        string metricId,
        IReadOnlyDictionary<string, string>? parameterOverrides,
        int boundedCapacity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(metricId);

        IFluxMetricSource<double> source;
        var start = false;
        lock (_sync)
        {
            if (_events is null)
            {
                throw new InvalidOperationException("Metric stream host is not configured for this app run.");
            }

            if (!_resources.TryGetValue(metricId.Trim(), out var resource))
            {
                throw new InvalidOperationException($"Metric '{metricId}' does not exist.");
            }

            var key = MetricStreamKey.Create(metricId, MergeParameters(resource, parameterOverrides));
            if (_sources.TryGetValue(key, out var existing))
            {
                return existing;
            }

            var type = _types.GetNumberType(resource.TypeId);
            source = type.CreateSource(new FluxMetricSourceContext(
                key.MetricId,
                key.Parameters,
                _events,
                boundedCapacity,
                _timeProvider));
            _sources[key] = source;
            start = _started && !_completed;
        }

        if (start)
        {
            source.StartAsync().GetAwaiter().GetResult();
        }

        return source;
    }

    private static IReadOnlyDictionary<string, string> MergeParameters(
        FluxMetricResourceDefinition resource,
        IReadOnlyDictionary<string, string>? overrides)
    {
        var merged = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in resource.Parameters)
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

    private void CompleteLocked()
    {
        foreach (var source in _sources.Values)
        {
            source.Complete();
        }

        _sources.Clear();
        _completed = true;
        _started = false;
        _events = null;
    }
}
