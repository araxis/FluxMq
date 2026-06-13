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
public sealed class MetricEventPump<TValue> : IFluxMetricSource<TValue>
{
    private readonly Lock _sync = new();
    private readonly ISourceBlock<FlowEvent> _events;
    private readonly Func<FlowEvent, DateTimeOffset, MetricSample<TValue>> _observe;
    private readonly ActionBlock<FlowEvent> _input;
    private readonly BroadcastBlock<FluxMetricReading<TValue>> _output;
    private readonly TimeProvider _timeProvider;
    private IDisposable? _link;
    private int _started;
    private bool _completed;

    public MetricEventPump(
        string metricId,
        ISourceBlock<FlowEvent> events,
        Func<FlowEvent, DateTimeOffset, MetricSample<TValue>> observe,
        int boundedCapacity = 256,
        TimeProvider? timeProvider = null)
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
            _link?.Dispose();
            _link = null;
            _input.Complete();
        }
    }

    private void Observe(FlowEvent flowEvent)
    {
        var now = _timeProvider.GetUtcNow();
        var sample = _observe(flowEvent, now);
        if (!sample.Emit)
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
        if (completion.IsFaulted && completion.Exception is { } exception)
        {
            ((IDataflowBlock)_output).Fault(exception);
            return;
        }

        _output.Complete();
    }
}
