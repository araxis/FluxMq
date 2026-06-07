using FluxFlow.Engine.Components;
using FluxMq.Core.Metrics;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.App.Metrics;

public interface IFluxMetric<TValue>
{
    string Id { get; }

    FluxMetricReading<TValue>? Latest { get; }

    ISourceBlock<FluxMetricReading<TValue>> Output { get; }

    Task StartAsync(CancellationToken cancellationToken = default);

    void Complete();

    Task Completion { get; }
}

public sealed class FluxMetricRuntimeCatalog
{
    private readonly FluxMetricCatalog _catalog;
    private readonly FluxMetricResolver _resolver;
    private readonly TimeProvider _timeProvider;

    public FluxMetricRuntimeCatalog(
        FluxMetricCatalog? catalog = null,
        FluxMetricResolver? resolver = null,
        TimeProvider? timeProvider = null)
    {
        _catalog = catalog ?? FluxMetricCatalog.Shared;
        _resolver = resolver ?? new FluxMetricResolver();
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public IFluxMetric<double> CreateNumberMetric(
        string id,
        FluxMetricArtifactDefinition artifact,
        IReadOnlyDictionary<string, string>? parameterValues,
        ISourceBlock<FlowEvent> events,
        int boundedCapacity = 1000)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(events);

        var definition = _resolver.Resolve(artifact, parameterValues);
        return _catalog.FindMeasure(definition.Measure).Id switch
        {
            FluxMetricCatalog.MeasureRate => new FluxEventRateMetric(id, definition, events, _catalog, boundedCapacity, _timeProvider),
            FluxMetricCatalog.MeasureTopics => new FluxUniqueTopicCountMetric(id, definition, events, _catalog, boundedCapacity, _timeProvider),
            FluxMetricCatalog.MeasurePayloadBytes => new FluxPayloadBytesMetric(id, definition, events, _catalog, boundedCapacity, _timeProvider),
            FluxMetricCatalog.MeasureAveragePayload => new FluxAveragePayloadMetric(id, definition, events, _catalog, boundedCapacity, _timeProvider),
            FluxMetricCatalog.MeasureRetained => new FluxRetainedCountMetric(id, definition, events, _catalog, boundedCapacity, _timeProvider),
            _ => new FluxEventCountMetric(id, definition, events, _catalog, boundedCapacity, _timeProvider)
        };
    }
}

public sealed class FluxMetricRuntimeHost : IAsyncDisposable
{
    private readonly Lock _sync = new();
    private readonly FluxMetricRuntimeCatalog _catalog;
    private readonly Dictionary<FluxMetricStreamKey, IFluxMetric<double>> _numberMetrics = new();
    private IReadOnlyDictionary<string, FluxMetricArtifactDefinition> _artifacts =
        new Dictionary<string, FluxMetricArtifactDefinition>(StringComparer.Ordinal);
    private ISourceBlock<FlowEvent>? _events;
    private bool _started;
    private bool _completed;

    public FluxMetricRuntimeHost(FluxMetricRuntimeCatalog? catalog = null)
    {
        _catalog = catalog ?? new FluxMetricRuntimeCatalog();
    }

    public void Configure(
        IReadOnlyDictionary<string, FluxMetricArtifactDefinition> artifacts,
        ISourceBlock<FlowEvent> events)
    {
        ArgumentNullException.ThrowIfNull(artifacts);
        ArgumentNullException.ThrowIfNull(events);

        lock (_sync)
        {
            CompleteLocked();
            _artifacts = new Dictionary<string, FluxMetricArtifactDefinition>(artifacts, StringComparer.Ordinal);
            _events = events;
            _completed = false;
            _started = false;
        }
    }

    public ISourceBlock<FluxMetricReading<double>> GetNumberStream(
        string metricId,
        IReadOnlyDictionary<string, string>? parameterValues = null,
        int boundedCapacity = 1000)
        => GetNumberMetric(metricId, parameterValues, boundedCapacity).Output;

    public bool TryGetLatestNumber(
        string metricId,
        IReadOnlyDictionary<string, string>? parameterValues,
        out FluxMetricReading<double> reading)
    {
        lock (_sync)
        {
            var key = FluxMetricStreamKey.Create(metricId, parameterValues);
            if (_numberMetrics.TryGetValue(key, out var metric) &&
                metric.Latest is { } latest)
            {
                reading = latest;
                return true;
            }
        }

        reading = default!;
        return false;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        IFluxMetric<double>[] metrics;
        lock (_sync)
        {
            if (_completed)
            {
                return;
            }

            _started = true;
            metrics = _numberMetrics.Values.ToArray();
        }

        foreach (var metric in metrics)
        {
            await metric.StartAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public void Complete()
    {
        lock (_sync)
        {
            CompleteLocked();
        }
    }

    public async ValueTask DisposeAsync()
    {
        IFluxMetric<double>[] metrics;
        lock (_sync)
        {
            metrics = _numberMetrics.Values.ToArray();
            _numberMetrics.Clear();
            _completed = true;
            _started = false;
            _events = null;
        }

        foreach (var metric in metrics)
        {
            metric.Complete();
        }

        try
        {
            await Task.WhenAll(metrics.Select(static metric => metric.Completion)).ConfigureAwait(false);
        }
        catch
        {
            // Runtime shutdown should continue even if one stream faults.
        }
    }

    internal IFluxMetric<double> GetNumberMetric(
        string metricId,
        IReadOnlyDictionary<string, string>? parameterValues = null,
        int boundedCapacity = 1000)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(metricId);

        IFluxMetric<double>? metric;
        var start = false;
        lock (_sync)
        {
            if (_events is null)
            {
                throw new InvalidOperationException("Metric runtime is not configured for this app run.");
            }

            var key = FluxMetricStreamKey.Create(metricId, parameterValues);
            if (_numberMetrics.TryGetValue(key, out metric))
            {
                return metric;
            }

            if (!_artifacts.TryGetValue(metricId.Trim(), out var artifact))
            {
                throw new InvalidOperationException($"Metric '{metricId}' does not exist.");
            }

            metric = _catalog.CreateNumberMetric(
                key.MetricId,
                artifact,
                key.Parameters,
                _events,
                boundedCapacity);
            _numberMetrics[key] = metric;
            start = _started && !_completed;
        }

        if (start)
        {
            metric.StartAsync().GetAwaiter().GetResult();
        }

        return metric;
    }

    private void CompleteLocked()
    {
        foreach (var metric in _numberMetrics.Values)
        {
            metric.Complete();
        }

        _numberMetrics.Clear();
        _completed = true;
        _started = false;
        _events = null;
    }
}

public sealed class FluxEventCountMetric(
    string id,
    FluxMetricDefinition definition,
    ISourceBlock<FlowEvent> events,
    FluxMetricCatalog? catalog = null,
    int boundedCapacity = 1000,
    TimeProvider? timeProvider = null)
    : FluxRuntimeEventNumberMetric(id, definition, events, catalog, boundedCapacity, timeProvider)
{
    protected override double Calculate(DateTimeOffset now)
        => RecentEvents(now).Count;
}

public sealed class FluxEventRateMetric(
    string id,
    FluxMetricDefinition definition,
    ISourceBlock<FlowEvent> events,
    FluxMetricCatalog? catalog = null,
    int boundedCapacity = 1000,
    TimeProvider? timeProvider = null)
    : FluxRuntimeEventNumberMetric(id, definition, events, catalog, boundedCapacity, timeProvider)
{
    protected override double Calculate(DateTimeOffset now)
        => RecentEvents(now).Count / Window.TotalSeconds;
}

public sealed class FluxUniqueTopicCountMetric(
    string id,
    FluxMetricDefinition definition,
    ISourceBlock<FlowEvent> events,
    FluxMetricCatalog? catalog = null,
    int boundedCapacity = 1000,
    TimeProvider? timeProvider = null)
    : FluxRuntimeEventNumberMetric(id, definition, events, catalog, boundedCapacity, timeProvider)
{
    protected override double Calculate(DateTimeOffset now)
        => RecentEvents(now)
            .Where(static flowEvent => !string.IsNullOrWhiteSpace(flowEvent.Channel))
            .Select(static flowEvent => flowEvent.Channel!)
            .Distinct(StringComparer.Ordinal)
            .Count();
}

public sealed class FluxPayloadBytesMetric(
    string id,
    FluxMetricDefinition definition,
    ISourceBlock<FlowEvent> events,
    FluxMetricCatalog? catalog = null,
    int boundedCapacity = 1000,
    TimeProvider? timeProvider = null)
    : FluxRuntimeEventNumberMetric(id, definition, events, catalog, boundedCapacity, timeProvider)
{
    protected override double Calculate(DateTimeOffset now)
        => RecentEvents(now).Sum(static flowEvent => (double)(flowEvent.PayloadBytes ?? 0));
}

public sealed class FluxAveragePayloadMetric(
    string id,
    FluxMetricDefinition definition,
    ISourceBlock<FlowEvent> events,
    FluxMetricCatalog? catalog = null,
    int boundedCapacity = 1000,
    TimeProvider? timeProvider = null)
    : FluxRuntimeEventNumberMetric(id, definition, events, catalog, boundedCapacity, timeProvider)
{
    protected override double Calculate(DateTimeOffset now)
    {
        var recent = RecentEvents(now);
        return recent.Count == 0
            ? 0
            : recent.Sum(static flowEvent => (double)(flowEvent.PayloadBytes ?? 0)) / recent.Count;
    }
}

public sealed class FluxRetainedCountMetric(
    string id,
    FluxMetricDefinition definition,
    ISourceBlock<FlowEvent> events,
    FluxMetricCatalog? catalog = null,
    int boundedCapacity = 1000,
    TimeProvider? timeProvider = null)
    : FluxRuntimeEventNumberMetric(id, definition, events, catalog, boundedCapacity, timeProvider)
{
    protected override double Calculate(DateTimeOffset now)
        => RecentEvents(now).Count(static flowEvent =>
            bool.TryParse(flowEvent.GetAttribute("retain"), out var retained) && retained);
}

public sealed class FluxMetricStreamKey : IEquatable<FluxMetricStreamKey>
{
    private readonly string _identity;

    private FluxMetricStreamKey(string metricId, IReadOnlyDictionary<string, string> parameters)
    {
        MetricId = metricId;
        Parameters = parameters;
        _identity = string.Join(
            "\u001f",
            Parameters.Select(static pair => $"{pair.Key}\u001e{pair.Value}"));
    }

    public string MetricId { get; }

    public IReadOnlyDictionary<string, string> Parameters { get; }

    public static FluxMetricStreamKey Create(
        string metricId,
        IReadOnlyDictionary<string, string>? parameters)
    {
        var id = metricId.Trim();
        var values = parameters is null || parameters.Count == 0
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : parameters
                .Where(static pair => !string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
                .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(
                    static pair => pair.Key.Trim(),
                    static pair => pair.Value.Trim(),
                    StringComparer.Ordinal);

        return new FluxMetricStreamKey(id, values);
    }

    public bool Equals(FluxMetricStreamKey? other)
        => other is not null &&
           string.Equals(MetricId, other.MetricId, StringComparison.Ordinal) &&
           string.Equals(_identity, other._identity, StringComparison.Ordinal);

    public override bool Equals(object? obj)
        => obj is FluxMetricStreamKey other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(StringComparer.Ordinal.GetHashCode(MetricId), StringComparer.Ordinal.GetHashCode(_identity));
}

public abstract class FluxRuntimeEventNumberMetric : IFluxMetric<double>
{
    private readonly Lock _sync = new();
    private readonly FluxMetricCatalog _catalog;
    private readonly ISourceBlock<FlowEvent> _events;
    private readonly ActionBlock<FlowEvent> _input;
    private readonly BroadcastBlock<FluxMetricReading<double>> _output;
    private readonly List<FlowEvent> _matchingEvents = [];
    private readonly TimeProvider _timeProvider;
    private IDisposable? _link;
    private int _started;
    private bool _completed;

    protected FluxRuntimeEventNumberMetric(
        string id,
        FluxMetricDefinition definition,
        ISourceBlock<FlowEvent> events,
        FluxMetricCatalog? catalog,
        int boundedCapacity,
        TimeProvider? timeProvider)
    {
        if (boundedCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(boundedCapacity), boundedCapacity, "Metric bounded capacity must be positive.");
        }

        Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("Metric id cannot be empty.", nameof(id)) : id.Trim();
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _catalog = catalog ?? FluxMetricCatalog.Shared;
        _timeProvider = timeProvider ?? TimeProvider.System;
        Window = FluxMetricEvaluationEngine.ParseWindow(definition.Window);
        _input = new ActionBlock<FlowEvent>(
            Observe,
            new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = boundedCapacity,
                EnsureOrdered = true,
                MaxDegreeOfParallelism = 1
            });
        _output = new BroadcastBlock<FluxMetricReading<double>>(
            static reading => reading,
            new DataflowBlockOptions
            {
                BoundedCapacity = boundedCapacity
            });
        _input.Completion.ContinueWith(
            CompleteOutput,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    public string Id { get; }

    public FluxMetricDefinition Definition { get; }

    public FluxMetricReading<double>? Latest { get; private set; }

    public ISourceBlock<FluxMetricReading<double>> Output => _output;

    public Task Completion => Task.WhenAll(_input.Completion, _output.Completion);

    protected TimeSpan Window { get; }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            return Task.CompletedTask;
        }

        lock (_sync)
        {
            if (_completed)
            {
                return Task.CompletedTask;
            }

            _link = _events.LinkTo(_input, new DataflowLinkOptions { PropagateCompletion = true });
        }

        return Task.CompletedTask;
    }

    public void Complete()
    {
        lock (_sync)
        {
            if (_completed)
            {
                return;
            }

            _completed = true;
            _link?.Dispose();
            _link = null;
            _input.Complete();
        }
    }

    protected abstract double Calculate(DateTimeOffset now);

    protected IReadOnlyList<FlowEvent> RecentEvents(DateTimeOffset now)
    {
        Prune(now);
        return _matchingEvents;
    }

    private void Observe(FlowEvent flowEvent)
    {
        var now = _timeProvider.GetUtcNow();
        lock (_sync)
        {
            if (_completed)
            {
                return;
            }

            if (_catalog.Matches(Definition, flowEvent))
            {
                _matchingEvents.Add(flowEvent);
            }

            Prune(now);
            if (!_catalog.Matches(Definition, flowEvent))
            {
                return;
            }

            var reading = new FluxMetricReading<double>
            {
                MetricId = Id,
                Timestamp = now,
                Value = Calculate(now)
            };
            Latest = reading;
            _output.Post(reading);
        }
    }

    private void Prune(DateTimeOffset now)
    {
        var threshold = now.Subtract(Window);
        _matchingEvents.RemoveAll(flowEvent => flowEvent.Timestamp < threshold);
    }

    private void CompleteOutput(Task completion)
    {
        if (completion.IsFaulted && completion.Exception is { } exception)
        {
            ((IDataflowBlock)_output).Fault(exception);
            return;
        }

        _output.Complete();
    }
}
