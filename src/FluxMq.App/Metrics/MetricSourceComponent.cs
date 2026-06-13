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
    private readonly BufferBlock<FlowError> _errors = new();
    private IDisposable? _link;
    private int _started;
    private bool _completed;

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
        _relay = new ActionBlock<FluxMetricReading<double>>(
            reading => _output.Post(reading),
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
            var stream = _coordinator.GetNumberStream(_metricId, _parameters);
            _link = stream.LinkTo(_relay, new DataflowLinkOptions { PropagateCompletion = true });
            if (_emitLatestOnStart &&
                _coordinator.TryGetLatestNumber(_metricId, _parameters, out var latest))
            {
                _output.Post(latest);
            }
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
        if (_completed)
        {
            return;
        }

        _completed = true;
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
            _completed = true;
            _link = null;
        }
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
