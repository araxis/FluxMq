using FluxFlow.Engine.Components;
using FluxMq.Core.Metrics;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.App.Metrics;

/// <summary>
/// Shared mechanics for event-driven numeric metric sources: it links the shared <see cref="FlowEvent"/> stream
/// into a single-threaded <see cref="ActionBlock{T}"/>, keeps a sliding window of matching events, and emits one
/// reading per match through a <see cref="BroadcastBlock{T}"/>. Concrete sources supply only the match predicate
/// and the per-window calculation; this base owns no metric-specific behavior.
/// </summary>
public abstract class EventWindowMetricSource : IFluxMetricSource<double>
{
    private readonly Lock _sync = new();
    private readonly Func<FlowEvent, bool> _matches;
    private readonly ISourceBlock<FlowEvent> _events;
    private readonly ActionBlock<FlowEvent> _input;
    private readonly BroadcastBlock<FluxMetricReading<double>> _output;
    private readonly List<FlowEvent> _window = [];
    private readonly TimeProvider _timeProvider;
    private IDisposable? _link;
    private int _started;
    private bool _completed;

    protected EventWindowMetricSource(
        string metricId,
        Func<FlowEvent, bool> matches,
        TimeSpan window,
        ISourceBlock<FlowEvent> events,
        int boundedCapacity,
        TimeProvider? timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(metricId);
        ArgumentNullException.ThrowIfNull(matches);
        ArgumentNullException.ThrowIfNull(events);
        if (boundedCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(boundedCapacity), boundedCapacity, "Metric bounded capacity must be positive.");
        }

        MetricId = metricId.Trim();
        _matches = matches;
        Window = window <= TimeSpan.Zero ? TimeSpan.FromSeconds(60) : window;
        _events = events;
        _timeProvider = timeProvider ?? TimeProvider.System;
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
            new DataflowBlockOptions { BoundedCapacity = boundedCapacity });
        _input.Completion.ContinueWith(
            CompleteOutput,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    public string MetricId { get; }

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

    /// <summary>Computes the metric value from the current pruned window of matching events.</summary>
    protected abstract double Calculate(IReadOnlyList<FlowEvent> window, DateTimeOffset now);

    private void Observe(FlowEvent flowEvent)
    {
        var now = _timeProvider.GetUtcNow();
        lock (_sync)
        {
            if (_completed)
            {
                return;
            }

            if (!_matches(flowEvent))
            {
                Prune(now);
                return;
            }

            _window.Add(flowEvent);
            Prune(now);

            var reading = new FluxMetricReading<double>
            {
                MetricId = MetricId,
                Timestamp = now,
                Value = Calculate(_window, now)
            };
            Latest = reading;
            _output.Post(reading);
        }
    }

    private void Prune(DateTimeOffset now)
    {
        var threshold = now.Subtract(Window);
        _window.RemoveAll(flowEvent => flowEvent.Timestamp < threshold);
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
