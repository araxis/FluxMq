using FluxFlow.Engine.Components;
using FluxMq.Core.Ids;
using FluxMq.Core.Metrics;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.App.Metrics;

/// <summary>
/// Pipeline source node that emits a running app metric's readings as <see cref="FluxMetricReading{Double}"/>.
/// It resolves the metric's number stream from the <see cref="FluxMetricStreamCoordinator"/> on start and relays
/// it through a broadcast output (int metrics are surfaced as doubles by the coordinator).
/// </summary>
public sealed class MetricSourceComponent : IFlowNode
{
    private readonly FlowNodeId _id;
    private readonly FluxMetricStreamCoordinator _coordinator;
    private readonly string _metricId;
    private readonly IReadOnlyDictionary<string, string> _parameters;
    private readonly bool _emitLatestOnStart;
    private readonly int _boundedCapacity;
    private readonly ActionBlock<FluxMetricReading<double>> _relay;
    private readonly BroadcastBlock<FluxMetricReading<double>> _output;
    private readonly BufferBlock<FlowError> _errors;
    private IDisposable? _link;
    private int _started;
    private int _completed;
    // Written on StartAsync, read on the relay's ActionBlock thread — volatile for cross-thread visibility.
    private volatile bool _skipReplayedLatest;

    public MetricSourceComponent(
        FluxMetricStreamCoordinator coordinator,
        string metricId,
        IReadOnlyDictionary<string, string>? parameters = null,
        bool emitLatestOnStart = true,
        int boundedCapacity = 1000,
        FlowNodeId? id = null)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        ArgumentException.ThrowIfNullOrWhiteSpace(metricId);
        if (boundedCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(boundedCapacity), boundedCapacity, "Metric source bounded capacity must be positive.");
        }

        _id = id ?? FlowNodeId.New();
        _coordinator = coordinator;
        _metricId = metricId.Trim();
        _parameters = NormalizeParameters(parameters);
        _emitLatestOnStart = emitLatestOnStart;
        _boundedCapacity = boundedCapacity;
        _output = new BroadcastBlock<FluxMetricReading<double>>(
            static reading => reading,
            new DataflowBlockOptions { BoundedCapacity = boundedCapacity });
        _errors = new BufferBlock<FlowError>(new DataflowBlockOptions { BoundedCapacity = boundedCapacity });
        _relay = new ActionBlock<FluxMetricReading<double>>(
            RelayReading,
            new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = boundedCapacity,
                EnsureOrdered = true,
                MaxDegreeOfParallelism = 1
            });
        _relay.Completion.ContinueWith(
            CompleteOutput,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    public FlowNodeId Id => _id;

    public ISourceBlock<FluxMetricReading<double>> Output => _output;

    public ISourceBlock<FlowError> Errors => _errors;

    public Task Completion => Task.WhenAll(_relay.Completion, _output.Completion, _errors.Completion);

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            return Task.CompletedTask;
        }

        try
        {
            // The coordinator stream is a BroadcastBlock: linking replays its current latest reading to the
            // relay, so a started source already surfaces its latest value — no extra explicit post (which
            // would duplicate it). The link is gated on _emitLatestOnStart only by discarding that first replay.
            var stream = _coordinator.GetNumberStream(_metricId, _parameters);
            _skipReplayedLatest = !_emitLatestOnStart &&
                _coordinator.TryGetLatestNumber(_metricId, _parameters, out _);
            _link = stream.LinkTo(_relay, new DataflowLinkOptions { PropagateCompletion = true });
        }
        catch (Exception exception)
        {
            PublishError(FlowErrorCodes.NodeFaulted, "Metric source failed to start.", exception, _metricId);
            throw;
        }

        return Task.CompletedTask;
    }

    public void Complete()
    {
        if (Interlocked.Exchange(ref _completed, 1) != 0)
        {
            return;
        }

        _link?.Dispose();
        _link = null;
        _relay.Complete();
    }

    public void Fault(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        try
        {
            _link?.Dispose();
            ((IDataflowBlock)_relay).Fault(exception);
            ((IDataflowBlock)_output).Fault(exception);
            ((IDataflowBlock)_errors).Fault(exception);
        }
        finally
        {
            Interlocked.Exchange(ref _completed, 1);
            _link = null;
        }
    }

    // Drops the single latest reading the broadcast replays on link when the node opted out of emit-on-start.
    private void RelayReading(FluxMetricReading<double> reading)
    {
        if (_skipReplayedLatest)
        {
            _skipReplayedLatest = false;
            return;
        }

        _output.Post(reading);
    }

    private void CompleteOutput(Task completion)
    {
        if (completion.IsFaulted && completion.Exception is { } exception)
        {
            ((IDataflowBlock)_output).Fault(exception);
            ((IDataflowBlock)_errors).Fault(exception);
            return;
        }

        _output.Complete();
        _errors.Complete();
    }

    private void PublishError(int code, string message, Exception exception, string? context = null)
    {
        _errors.Post(new FlowError
        {
            NodeId = Id,
            Code = code,
            Message = message,
            Exception = exception,
            Context = context
        });
    }

    private static IReadOnlyDictionary<string, string> NormalizeParameters(IReadOnlyDictionary<string, string>? parameters)
        => parameters is null || parameters.Count == 0
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : parameters
                .Where(static pair => !string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
                .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(
                    static pair => pair.Key.Trim(),
                    static pair => pair.Value.Trim(),
                    StringComparer.Ordinal);
}
