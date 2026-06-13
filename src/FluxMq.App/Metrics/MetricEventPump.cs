using FluxFlow.Engine.Components;
using FluxMq.Core.Metrics;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.App.Metrics;

/// <summary>The outcome of observing one event: whether to emit a reading, and the value if so.</summary>
public readonly record struct MetricSample<TValue>(bool Emit, TValue Value)
{
    public static MetricSample<TValue> None => new(false, default!);

    public static MetricSample<TValue> Of(TValue value) => new(true, value);
}

/// <summary>
/// The small, shared Dataflow plumbing every event metric composes (has-a, not is-a): it links the shared
/// <see cref="FlowEvent"/> stream into a serial <see cref="ActionBlock{T}"/>, hands each event to the metric's
/// observer, and broadcasts whatever readings the observer chooses to emit. It owns no metric-specific logic —
/// the filter, state, and calculation all live in the metric that supplies <paramref name="observe"/>.
/// </summary>
/// <remarks>
/// An optional <paramref name="recompute"/> + interval lets windowed metrics decay on idle: a timer posts a
/// null tick into the same serial ActionBlock, the metric prunes and recomputes, and a changed value is emitted.
/// Routing the tick through the ActionBlock keeps all metric state on one thread, so no locking is needed.
/// </remarks>
public sealed class MetricEventPump<TValue> : IFluxMetricSource<TValue>
{
    private readonly Lock _sync = new();
    private readonly ISourceBlock<FlowEvent> _events;
    private readonly Func<FlowEvent, DateTimeOffset, MetricSample<TValue>> _observe;
    private readonly Func<DateTimeOffset, MetricSample<TValue>>? _recompute;
    private readonly ActionBlock<FlowEvent> _input;
    private readonly BroadcastBlock<FluxMetricReading<TValue>> _output;
    private readonly TimeProvider _timeProvider;
    private readonly ITimer? _decayTimer;
    private IDisposable? _link;
    private int _started;
    private bool _completed;

    public MetricEventPump(
        string metricId,
        ISourceBlock<FlowEvent> events,
        Func<FlowEvent, DateTimeOffset, MetricSample<TValue>> observe,
        int boundedCapacity = 256,
        TimeProvider? timeProvider = null,
        Func<DateTimeOffset, MetricSample<TValue>>? recompute = null,
        TimeSpan? recomputeInterval = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(metricId);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(observe);
        if (boundedCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(boundedCapacity), boundedCapacity, "Metric bounded capacity must be positive.");
        }

        MetricId = metricId.Trim();
        _events = events;
        _observe = observe;
        _recompute = recompute;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _input = new ActionBlock<FlowEvent>(
            Observe,
            new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = boundedCapacity,
                EnsureOrdered = true,
                MaxDegreeOfParallelism = 1
            });
        _output = new BroadcastBlock<FluxMetricReading<TValue>>(
            static reading => reading,
            new DataflowBlockOptions { BoundedCapacity = boundedCapacity });
        _input.Completion.ContinueWith(
            CompleteOutput,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        if (recompute is not null)
        {
            var interval = recomputeInterval is { } value && value > TimeSpan.Zero ? value : TimeSpan.FromSeconds(1);
            _decayTimer = _timeProvider.CreateTimer(static state => ((ActionBlock<FlowEvent>)state!).Post(null!), _input, interval, interval);
        }
    }

    public string MetricId { get; }

    public FluxMetricReading<TValue>? Latest { get; private set; }

    public ISourceBlock<FluxMetricReading<TValue>> Output => _output;

    public Task Completion => Task.WhenAll(_input.Completion, _output.Completion);

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            return Task.CompletedTask;
        }

        lock (_sync)
        {
            if (!_completed)
            {
                _link = _events.LinkTo(_input, new DataflowLinkOptions { PropagateCompletion = true });
            }
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
            _decayTimer?.Dispose();
            _link?.Dispose();
            _link = null;
            _input.Complete();
        }
    }

    // A null event is a decay tick: the metric recomputes from its (now pruned) state and we emit only when the
    // value actually changed, so an idle metric settles toward zero without spamming identical readings.
    private void Observe(FlowEvent? flowEvent)
    {
        var now = _timeProvider.GetUtcNow();
        var isTick = flowEvent is null;
        var sample = isTick
            ? (_recompute?.Invoke(now) ?? MetricSample<TValue>.None)
            : _observe(flowEvent!, now);
        if (!sample.Emit)
        {
            return;
        }

        if (isTick && Latest is { } latest && EqualityComparer<TValue>.Default.Equals(latest.Value, sample.Value))
        {
            return;
        }

        var reading = new FluxMetricReading<TValue>
        {
            MetricId = MetricId,
            Timestamp = now,
            Value = sample.Value
        };
        Latest = reading;
        _output.Post(reading);
    }

    private void CompleteOutput(Task completion)
    {
        _decayTimer?.Dispose();
        if (completion.IsFaulted && completion.Exception is { } exception)
        {
            ((IDataflowBlock)_output).Fault(exception);
            return;
        }

        _output.Complete();
    }
}
