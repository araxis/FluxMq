using FluxFlow.Engine.Components;
using FluxMq.Core.Metrics;
using System.Text;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.App.Metrics;

/// <summary>
/// Runs the app's metric resources against the live runtime event stream. For a given metric id (plus optional
/// per-consumer parameter overrides) it builds the matching <see cref="IFluxMetricSource"/> from the catalog once,
/// keeps it running, and exposes the latest numeric reading. This is the flat-framework replacement for the old
/// stream host: it owns no metric logic, only the lifecycle and a small cache.
/// </summary>
public sealed class FluxMetricStreamCoordinator
{
    private const int DefaultBoundedCapacity = 256;

    private readonly IFluxMetricCatalog _catalog;
    private readonly object _sync = new();

    // One running source per distinct (metric id + effective parameters) key. The key space is bounded by the
    // configured resources times the parameter-override combinations consumers actually request (stable in practice
    // — a tile/node binds a fixed set), and the whole map is completed and cleared on every Configure/Complete.
    // It is intentionally lifetime-scoped to the current configuration, not an LRU cache.
    private readonly Dictionary<string, RunningMetric> _streams = new(StringComparer.Ordinal);

    private IReadOnlyDictionary<string, FluxMetricResourceDefinition> _resources =
        new Dictionary<string, FluxMetricResourceDefinition>(StringComparer.Ordinal);
    private ISourceBlock<FlowEvent>? _events;

    public FluxMetricStreamCoordinator(IFluxMetricCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _catalog = catalog;
    }

    /// <summary>Binds the coordinator to a set of metric resources and the runtime event stream (called on build).</summary>
    public void Configure(
        IReadOnlyDictionary<string, FluxMetricResourceDefinition> resources,
        ISourceBlock<FlowEvent> events)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(events);

        lock (_sync)
        {
            CompleteStreams();
            _resources = resources;
            _events = events;
        }
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    /// <summary>Returns the running metric's readings as a numeric stream, creating it on first use.</summary>
    public ISourceBlock<FluxMetricReading<double>> GetNumberStream(
        string metricId,
        IReadOnlyDictionary<string, string>? overrides)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(metricId);
        lock (_sync)
        {
            if (!TryGetStream(metricId.Trim(), overrides, out var running) || running is null)
            {
                throw new InvalidOperationException($"Metric '{metricId}' is not available.");
            }

            // Build and link the adapter while still holding the lock so a concurrent Configure/Complete
            // cannot complete the source between resolving it and linking the adapter to its output.
            return running.NumberStream;
        }
    }

    /// <summary>Reads the latest numeric reading for a metric, creating and starting its stream on first use.</summary>
    public bool TryGetLatestNumber(
        string? metricId,
        IReadOnlyDictionary<string, string>? overrides,
        out FluxMetricReading<double> reading)
    {
        reading = default!;
        if (string.IsNullOrWhiteSpace(metricId))
        {
            return false;
        }

        if (TryGetStream(metricId.Trim(), overrides, out var running) && running is not null &&
            ReadLatest(running) is { } value)
        {
            reading = value;
            return true;
        }

        return false;
    }

    public void Complete()
    {
        lock (_sync)
        {
            CompleteStreams();
            _resources = new Dictionary<string, FluxMetricResourceDefinition>(StringComparer.Ordinal);
            _events = null;
        }
    }

    private bool TryGetStream(string metricId, IReadOnlyDictionary<string, string>? overrides, out RunningMetric? running)
    {
        lock (_sync)
        {
            running = null;
            if (_events is null ||
                !_resources.TryGetValue(metricId, out var resource) ||
                _catalog.Describe(resource.TypeId) is not { } descriptor)
            {
                return false;
            }

            var parameters = Merge(resource.Parameters, overrides);
            var key = StreamKey(metricId, parameters);
            if (!_streams.TryGetValue(key, out running))
            {
                var source = _catalog.Create(
                    resource.TypeId,
                    new MetricSourceContext(metricId, parameters, _events, DefaultBoundedCapacity));
                source.StartAsync();
                running = new RunningMetric(
                    source,
                    descriptor.ValueKind,
                    () => CreateNumberStream(source, descriptor.ValueKind));
                _streams[key] = running;
            }

            return true;
        }
    }

    // Exposes a metric source as a double-valued stream so consumers (e.g. the metric.source pipeline node)
    // do not need to know whether the metric is int- or double-valued.
    private static ISourceBlock<FluxMetricReading<double>> CreateNumberStream(IFluxMetricSource source, MetricValueKind valueKind)
    {
        if (valueKind == MetricValueKind.Double)
        {
            return ((IFluxMetricSource<double>)source).Output;
        }

        var broadcast = new BroadcastBlock<FluxMetricReading<double>>(
            static reading => reading,
            new DataflowBlockOptions { BoundedCapacity = DefaultBoundedCapacity });
        var transform = new TransformBlock<FluxMetricReading<int>, FluxMetricReading<double>>(
            static reading => new FluxMetricReading<double>
            {
                MetricId = reading.MetricId,
                Timestamp = reading.Timestamp,
                Value = reading.Value
            },
            new ExecutionDataflowBlockOptions { BoundedCapacity = DefaultBoundedCapacity });
        transform.LinkTo(broadcast, new DataflowLinkOptions { PropagateCompletion = true });
        ((IFluxMetricSource<int>)source).Output.LinkTo(transform, new DataflowLinkOptions { PropagateCompletion = true });
        return broadcast;
    }

    private static FluxMetricReading<double>? ReadLatest(RunningMetric running)
        => running.ValueKind switch
        {
            MetricValueKind.Int when (running.Source as IFluxMetricSource<int>)?.Latest is { } intReading
                => new FluxMetricReading<double>
                {
                    MetricId = intReading.MetricId,
                    Timestamp = intReading.Timestamp,
                    Value = intReading.Value
                },
            MetricValueKind.Double => (running.Source as IFluxMetricSource<double>)?.Latest,
            _ => null
        };

    private void CompleteStreams()
    {
        foreach (var running in _streams.Values)
        {
            running.Source.Complete();
        }

        _streams.Clear();
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

    // Length-prefixed so no topic/parameter value can be mistaken for a separator.
    private static string StreamKey(string metricId, IReadOnlyDictionary<string, string> parameters)
    {
        var builder = new StringBuilder().Append(metricId.Length).Append(':').Append(metricId);
        foreach (var (key, value) in parameters.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            builder.Append('|').Append(key.Length).Append(':').Append(key)
                   .Append('=').Append(value.Length).Append(':').Append(value);
        }

        return builder.ToString();
    }

    private sealed class RunningMetric(
        IFluxMetricSource source,
        MetricValueKind valueKind,
        Func<ISourceBlock<FluxMetricReading<double>>> numberStreamFactory)
    {
        private readonly Lazy<ISourceBlock<FluxMetricReading<double>>> _numberStream = new(numberStreamFactory);

        public IFluxMetricSource Source { get; } = source;

        public MetricValueKind ValueKind { get; } = valueKind;

        // Only built (and only then linked to the source) when a consumer actually wants the number stream;
        // a metric used purely via TryGetLatestNumber never allocates the adapter.
        public ISourceBlock<FluxMetricReading<double>> NumberStream => _numberStream.Value;
    }
}
