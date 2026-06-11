using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxFlow.Components.Metrics;
using FluxFlow.Components.Metrics.Contracts;
using FluxFlow.Engine.Components;
using FluxFlow.Engine.Definitions;
using FluxFlow.Engine.Runtime;
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Components.MqttMetrics;

public sealed class MqttMetricsComponent : FlowNodeBase, IDisposable
{
    public static readonly TimeSpan DefaultRateWindow = MqttMetricsSnapshot.DefaultRateWindow;
    public static readonly TimeSpan DefaultRateRefreshInterval = TimeSpan.FromSeconds(1);

    private static readonly PortName InputPort = new("Input");
    private const string MetricName = "mqtt.message";
    private const string PayloadBytesUnit = "bytes";
    private const string TopicTag = "topic";
    private const string RetainTag = "retain";
    private const string ReceivedAtTag = "receivedAt";

    private readonly Lock _sync = new();
    private readonly Queue<MqttMetricObservation> _recentObservations = new();
    private readonly Dictionary<string, long> _recentTopicCounts = new(StringComparer.Ordinal);
    private readonly TimeSpan _rateWindow;
    private readonly TimeSpan _rateRefreshInterval;
    private readonly TimeProvider _timeProvider;
    private readonly DateTimeOffset _startedAt;
    private readonly RuntimeNode _innerNode;
    private readonly ITargetBlock<MetricSampleInput> _innerInput;
    private readonly ActionBlock<MqttEnvelope> _input;
    private readonly ActionBlock<MetricSnapshotOutput> _snapshotRelay;
    private readonly ActionBlock<FlowError> _packageErrors;
    private readonly BufferBlock<MqttMetricsSnapshot> _snapshots;
    private readonly List<IDisposable> _links = [];
    private Timer? _rateRefreshTimer;
    private MetricSnapshotOutput? _latestPackageSnapshot;
    private MqttMetricsSnapshot _latestSnapshot = new();
    private long _retainedMessageCount;
    private string? _lastTopic;
    private DateTimeOffset? _lastReceivedAt;
    private bool _completed;
    private int _started;

    public MqttMetricsComponent(
        FlowNodeId? id = null,
        int boundedCapacity = 1000,
        TimeSpan? rateWindow = null,
        TimeSpan? rateRefreshInterval = null,
        TimeProvider? timeProvider = null)
        : base(id ?? FlowNodeId.New())
    {
        if (boundedCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(boundedCapacity), boundedCapacity, "Bounded capacity must be positive.");
        }

        _rateWindow = rateWindow ?? DefaultRateWindow;
        if (_rateWindow <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(rateWindow), "Rate window must be greater than zero.");
        }

        _rateRefreshInterval = rateRefreshInterval ?? DefaultRateRefreshInterval;
        if (_rateRefreshInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(rateRefreshInterval), "Rate refresh interval must be greater than zero.");
        }

        _timeProvider = timeProvider ?? TimeProvider.System;
        _startedAt = _timeProvider.GetUtcNow();
        _snapshots = new BufferBlock<MqttMetricsSnapshot>();
        _innerNode = CreateInnerNode(boundedCapacity, _rateWindow);
        _innerInput = GetInput<MetricSampleInput>(_innerNode, MetricsComponentPorts.Input);
        _input = new ActionBlock<MqttEnvelope>(
            ObserveAsync,
            new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = boundedCapacity,
                EnsureOrdered = true,
                MaxDegreeOfParallelism = 1
            });
        _snapshotRelay = new ActionBlock<MetricSnapshotOutput>(
            PublishSnapshot,
            new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = boundedCapacity,
                EnsureOrdered = true,
                MaxDegreeOfParallelism = 1
            });
        _packageErrors = new ActionBlock<FlowError>(
            error => TryReportError(error.Code, error.Message, error.Exception, error.Context),
            new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = boundedCapacity,
                EnsureOrdered = true,
                MaxDegreeOfParallelism = 1
            });

        _links.Add(LinkOutput(_innerNode, MetricsComponentPorts.Output, _snapshotRelay));
        _links.Add(_innerNode.Node.Errors.LinkTo(
            _packageErrors,
            new DataflowLinkOptions { PropagateCompletion = true }));

        CompleteWhen(CompleteAsync());
    }

    public ITargetBlock<MqttEnvelope> Input => _input;
    public ISourceBlock<MqttMetricsSnapshot> Snapshots => _snapshots;

    public MqttMetricsSnapshot Current
    {
        get
        {
            lock (_sync)
            {
                if (_latestPackageSnapshot is null)
                {
                    return _latestSnapshot;
                }

                PruneRecentObservations(_timeProvider.GetUtcNow());
                _latestSnapshot = CreateSnapshot(_latestPackageSnapshot);
                return _latestSnapshot;
            }
        }
    }

    public override Task StartAsync(CancellationToken cancellationToken = default)
        => Interlocked.Exchange(ref _started, 1) == 0
            ? _innerNode.Node.StartAsync(cancellationToken)
            : Task.CompletedTask;

    public override void Complete()
    {
        lock (_sync)
        {
            _completed = true;
            DisposeRateRefreshTimer();
        }

        _input.Complete();
    }

    public override void Fault(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        lock (_sync)
        {
            _completed = true;
            DisposeRateRefreshTimer();
        }

        TryReportError(FlowErrorCodes.NodeFaulted, "MQTT metrics observer faulted.", exception);
        try
        {
            ((IDataflowBlock)_input).Fault(exception);
            _innerNode.Node.Fault(exception);
            ((IDataflowBlock)_snapshotRelay).Fault(exception);
            ((IDataflowBlock)_packageErrors).Fault(exception);
        }
        finally
        {
            FaultNode(exception);
        }
    }

    public void Dispose() => Complete();

    protected override void OnNodeCompleted()
    {
        DisposeLinks();
        _snapshots.Complete();
        base.OnNodeCompleted();
    }

    protected override void OnNodeFaulted(Exception exception)
    {
        DisposeLinks();
        ((IDataflowBlock)_snapshots).Fault(exception);
        base.OnNodeFaulted(exception);
    }

    private async Task ObserveAsync(MqttEnvelope envelope)
    {
        try
        {
            var payloadLength = envelope.Payload.Length;
            var observedAt = _timeProvider.GetUtcNow();

            lock (_sync)
            {
                TrackRecentObservation(observedAt, envelope.Topic);
                EnsureRateRefreshTimer();
                _lastTopic = envelope.Topic;
                _lastReceivedAt = envelope.ReceivedAt;

                if (envelope.Retain)
                {
                    _retainedMessageCount++;
                }
            }

            var accepted = await _innerInput.SendAsync(
                    CreateSample(envelope, observedAt, payloadLength))
                .ConfigureAwait(false);

            if (!accepted)
            {
                TryReportError(
                    FlowErrorCodes.ProcessingFailed,
                    "MQTT metrics update failed.",
                    new InvalidOperationException("Metrics aggregate input is closed."),
                    envelope.Topic);
            }
        }
        catch (Exception exception)
        {
            TryReportError(FlowErrorCodes.ProcessingFailed, "MQTT metrics update failed.", exception, envelope.Topic);
        }
    }

    private void PublishSnapshot(MetricSnapshotOutput packageSnapshot)
    {
        MqttMetricsSnapshot snapshot;
        lock (_sync)
        {
            PruneRecentObservations(_timeProvider.GetUtcNow());
            _latestPackageSnapshot = packageSnapshot;
            _latestSnapshot = CreateSnapshot(packageSnapshot);
            snapshot = _latestSnapshot;
        }

        _snapshots.Post(snapshot);
    }

    private async Task CompleteAsync()
    {
        try
        {
            await _input.Completion.ConfigureAwait(false);
            _innerNode.Node.Complete();
            await Task.WhenAll(
                    _innerNode.Node.Completion,
                    _snapshotRelay.Completion,
                    _packageErrors.Completion)
                .ConfigureAwait(false);
        }
        finally
        {
            lock (_sync)
            {
                _completed = true;
                DisposeRateRefreshTimer();
            }
        }
    }

    private void RefreshRateSnapshot()
    {
        MqttMetricsSnapshot? snapshot = null;

        lock (_sync)
        {
            if (_completed)
            {
                return;
            }

            if (_latestPackageSnapshot is null)
            {
                return;
            }

            if (_latestPackageSnapshot.SampleCount == 0)
            {
                DisposeRateRefreshTimer();
                return;
            }

            PruneRecentObservations(_timeProvider.GetUtcNow());
            _latestSnapshot = CreateSnapshot(_latestPackageSnapshot);
            snapshot = _latestSnapshot;
        }

        _snapshots.Post(snapshot);
    }

    private void EnsureRateRefreshTimer()
    {
        if (_completed || _rateRefreshTimer is not null)
        {
            return;
        }

        _rateRefreshTimer = new Timer(
            _ => RefreshRateSnapshot(),
            null,
            _rateRefreshInterval,
            _rateRefreshInterval);
    }

    private void DisposeRateRefreshTimer()
    {
        _rateRefreshTimer?.Dispose();
        _rateRefreshTimer = null;
    }

    private void TrackRecentObservation(DateTimeOffset observedAt, string topic)
    {
        _recentObservations.Enqueue(new MqttMetricObservation(observedAt, topic));
        _recentTopicCounts[topic] = _recentTopicCounts.TryGetValue(topic, out var count)
            ? count + 1
            : 1;

        PruneRecentObservations(observedAt);
    }

    private void PruneRecentObservations(DateTimeOffset observedAt)
    {
        var cutoff = observedAt - _rateWindow;
        while (_recentObservations.TryPeek(out var oldest) && oldest.ObservedAt < cutoff)
        {
            _recentObservations.Dequeue();
            var nextCount = _recentTopicCounts[oldest.Topic] - 1;
            if (nextCount <= 0)
            {
                _recentTopicCounts.Remove(oldest.Topic);
            }
            else
            {
                _recentTopicCounts[oldest.Topic] = nextCount;
            }
        }
    }

    private MqttMetricsSnapshot CreateSnapshot(MetricSnapshotOutput packageSnapshot)
    {
        var messageCount = packageSnapshot.SampleCount;
        var sinceStartDuration = messageCount == 0
            ? TimeSpan.Zero
            : EffectiveRateDuration(_timeProvider.GetUtcNow() - _startedAt);
        var topicCounts = packageSnapshot.Groups.Values
            .OrderByDescending(static group => group.Count)
            .ThenBy(static group => group.Group, StringComparer.Ordinal)
            .Select(static group => new MqttTopicMetric(group.Group, group.Count))
            .ToArray();
        var topicRates = _recentTopicCounts
            .OrderByDescending(static pair => pair.Value)
            .ThenBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new MqttTopicRateMetric(pair.Key, pair.Value, pair.Value / _rateWindow.TotalSeconds))
            .ToArray();

        return new MqttMetricsSnapshot
        {
            MessageCount = messageCount,
            RollingMessageCount = _recentObservations.Count,
            TotalPayloadBytes = ToLong(packageSnapshot.TotalValue),
            MinPayloadBytes = ToLong(packageSnapshot.MinValue),
            MaxPayloadBytes = ToLong(packageSnapshot.MaxValue),
            RetainedMessageCount = _retainedMessageCount,
            UniqueTopicCount = packageSnapshot.Groups.Count,
            LastTopic = _lastTopic,
            LastReceivedAt = _lastReceivedAt,
            RateWindow = _rateWindow,
            SinceStartDuration = sinceStartDuration,
            TopicCounts = topicCounts,
            TopicRates = topicRates
        };
    }

    private static MetricSampleInput CreateSample(
        MqttEnvelope envelope,
        DateTimeOffset observedAt,
        long payloadLength)
        => new()
        {
            Timestamp = observedAt,
            Name = MetricName,
            Group = envelope.Topic,
            Value = payloadLength,
            Unit = PayloadBytesUnit,
            Size = payloadLength,
            Tags = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [TopicTag] = envelope.Topic,
                [RetainTag] = envelope.Retain.ToString(),
                [ReceivedAtTag] = envelope.ReceivedAt.ToString("O", CultureInfo.InvariantCulture)
            }
        };

    private static RuntimeNode CreateInnerNode(int boundedCapacity, TimeSpan rateWindow)
    {
        var registry = new RuntimeNodeFactoryRegistry()
            .RegisterMetricsComponents();

        if (!registry.TryGetFactory(MetricsComponentTypes.Aggregate, out var factory))
        {
            throw new InvalidOperationException("Metrics aggregate package factory was not registered.");
        }

        return factory(new RuntimeNodeFactoryContext(
            new NodeName("mqttMetricsAggregate"),
            CreateInnerDefinition(boundedCapacity, rateWindow),
            "mqttMetricsAggregate",
            new Dictionary<NodeName, RuntimeNode>()));
    }

    private static NodeDefinition CreateInnerDefinition(int boundedCapacity, TimeSpan rateWindow)
    {
        var definition = new NodeDefinition
        {
            Type = MetricsComponentTypes.Aggregate
        };

        definition.Configuration["boundedCapacity"] = JsonSerializer.SerializeToElement(boundedCapacity);
        definition.Configuration["rateWindowSeconds"] = JsonSerializer.SerializeToElement(rateWindow.TotalSeconds);
        definition.Configuration["groupByTag"] = JsonSerializer.SerializeToElement(TopicTag);
        definition.Configuration["emitEverySample"] = JsonSerializer.SerializeToElement(true);
        definition.Configuration["trackLatest"] = JsonSerializer.SerializeToElement(true);
        definition.Configuration["trackMinMax"] = JsonSerializer.SerializeToElement(true);
        definition.Configuration["trackSize"] = JsonSerializer.SerializeToElement(true);

        return definition;
    }

    private static ITargetBlock<T> GetInput<T>(RuntimeNode node, string portName)
    {
        var input = node.FindInput(new PortName(portName))
            ?? throw new InvalidOperationException($"Inner metrics input '{portName}' was not found.");

        return input is InputPort<T> typedInput
            ? typedInput.Target
            : throw new InvalidOperationException(
                $"Inner metrics input '{portName}' has unsupported type '{input.ValueType.Name}'.");
    }

    private static IDisposable LinkOutput<T>(
        RuntimeNode node,
        string portName,
        ITargetBlock<T> target)
    {
        var output = node.FindOutput(new PortName(portName))
            ?? throw new InvalidOperationException($"Inner metrics output '{portName}' was not found.");

        var link = output.TryLinkTo(
            new InputPort<T>(
                new PortAddress(
                    "mqttMetricsAggregate",
                    new NodeName(portName),
                    InputPort),
                target),
            propagateCompletion: true,
            out var error);

        return link ?? throw new InvalidOperationException(
            error?.Message ?? $"Could not link inner metrics output '{portName}'.");
    }

    private void DisposeLinks()
    {
        foreach (var link in _links)
        {
            link.Dispose();
        }

        _links.Clear();
    }

    private static long ToLong(double? value)
        => value.HasValue ? (long)Math.Round(value.Value) : 0;

    private static TimeSpan EffectiveRateDuration(TimeSpan duration)
        => duration < TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : duration;

    private readonly record struct MqttMetricObservation(DateTimeOffset ObservedAt, string Topic);
}
