using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Pipeline.Components;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Components.MqttMetrics;

public sealed class MqttMetricsComponent : IFlowNode, IDisposable
{
    public static readonly TimeSpan DefaultRateWindow = MqttMetricsSnapshot.DefaultRateWindow;
    public static readonly TimeSpan DefaultRateRefreshInterval = TimeSpan.FromSeconds(1);

    private readonly Lock _sync = new();
    private readonly Dictionary<string, long> _topicCounts = new(StringComparer.Ordinal);
    private readonly Queue<MqttMetricObservation> _recentObservations = new();
    private readonly Dictionary<string, long> _recentTopicCounts = new(StringComparer.Ordinal);
    private readonly TimeSpan _rateWindow;
    private readonly TimeSpan _rateRefreshInterval;
    private readonly TimeProvider _timeProvider;
    private readonly DateTimeOffset _startedAt;
    private readonly ActionBlock<MqttEnvelope> _block;
    private readonly BroadcastBlock<FlowError> _errors;
    private readonly BroadcastBlock<MqttMetricsSnapshot> _snapshots;
    private Timer? _rateRefreshTimer;
    private long _messageCount;
    private long _totalPayloadBytes;
    private long _minPayloadBytes;
    private long _maxPayloadBytes;
    private long _retainedMessageCount;
    private string? _lastTopic;
    private DateTimeOffset? _lastReceivedAt;
    private bool _completed;

    public MqttMetricsComponent(
        FlowNodeId? id = null,
        int boundedCapacity = 1000,
        TimeSpan? rateWindow = null,
        TimeSpan? rateRefreshInterval = null,
        TimeProvider? timeProvider = null)
    {
        Id = id ?? FlowNodeId.New();
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
        _errors = new BroadcastBlock<FlowError>(static error => error);
        _snapshots = new BroadcastBlock<MqttMetricsSnapshot>(static snapshot => snapshot);
        _block = new ActionBlock<MqttEnvelope>(
            Observe,
            new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = boundedCapacity,
                EnsureOrdered = true,
                MaxDegreeOfParallelism = 1
            });

        _block.Completion.ContinueWith(
            _ =>
            {
                lock (_sync)
                {
                    _completed = true;
                    DisposeRateRefreshTimer();
                }

                _snapshots.Complete();
                _errors.Complete();
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    public FlowNodeId Id { get; }
    public ISourceBlock<FlowError> Errors => _errors;
    public Task Completion => _block.Completion;
    public ITargetBlock<MqttEnvelope> Input => _block;
    public ISourceBlock<MqttMetricsSnapshot> Snapshots => _snapshots;
    public MqttMetricsSnapshot Current
    {
        get
        {
            lock (_sync)
            {
                PruneRecentObservations(_timeProvider.GetUtcNow());
                return CreateSnapshot();
            }
        }
    }

    public void Complete()
    {
        lock (_sync)
        {
            _completed = true;
            DisposeRateRefreshTimer();
        }

        _block.Complete();
    }

    public void Fault(Exception exception)
    {
        lock (_sync)
        {
            _completed = true;
            DisposeRateRefreshTimer();
        }

        PublishError(FlowErrorCodes.NodeFaulted, "MQTT metrics observer faulted.", exception);
        ((IDataflowBlock)_block).Fault(exception);
    }

    public void Dispose() => Complete();

    private void Observe(MqttEnvelope envelope)
    {
        try
        {
            var payloadLength = envelope.Payload.Length;
            var observedAt = _timeProvider.GetUtcNow();
            MqttMetricsSnapshot snapshot;

            lock (_sync)
            {
                TrackRecentObservation(observedAt, envelope.Topic);
                EnsureRateRefreshTimer();
                _messageCount++;
                _totalPayloadBytes += payloadLength;
                _minPayloadBytes = _messageCount == 1 ? payloadLength : Math.Min(_minPayloadBytes, payloadLength);
                _maxPayloadBytes = Math.Max(_maxPayloadBytes, payloadLength);
                _lastTopic = envelope.Topic;
                _lastReceivedAt = envelope.ReceivedAt;
                _topicCounts[envelope.Topic] = _topicCounts.TryGetValue(envelope.Topic, out var count)
                    ? count + 1
                    : 1;

                if (envelope.Retain)
                {
                    _retainedMessageCount++;
                }

                snapshot = CreateSnapshot();
            }

            _snapshots.Post(snapshot);
        }
        catch (Exception exception)
        {
            PublishError(FlowErrorCodes.ProcessingFailed, "MQTT metrics update failed.", exception, envelope.Topic);
        }
    }

    private void RefreshRateSnapshot()
    {
        MqttMetricsSnapshot? snapshot;

        lock (_sync)
        {
            if (_completed)
            {
                return;
            }

            if (_messageCount == 0)
            {
                DisposeRateRefreshTimer();
                return;
            }

            PruneRecentObservations(_timeProvider.GetUtcNow());
            snapshot = CreateSnapshot();
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

    private MqttMetricsSnapshot CreateSnapshot()
    {
        var sinceStartDuration = _messageCount == 0
            ? TimeSpan.Zero
            : EffectiveRateDuration(_timeProvider.GetUtcNow() - _startedAt);

        return new MqttMetricsSnapshot
        {
            MessageCount = _messageCount,
            RollingMessageCount = _recentObservations.Count,
            TotalPayloadBytes = _totalPayloadBytes,
            MinPayloadBytes = _messageCount == 0 ? 0 : _minPayloadBytes,
            MaxPayloadBytes = _maxPayloadBytes,
            RetainedMessageCount = _retainedMessageCount,
            UniqueTopicCount = _topicCounts.Count,
            LastTopic = _lastTopic,
            LastReceivedAt = _lastReceivedAt,
            RateWindow = _rateWindow,
            SinceStartDuration = sinceStartDuration,
            TopicCounts = _topicCounts
                .OrderByDescending(static pair => pair.Value)
                .ThenBy(static pair => pair.Key, StringComparer.Ordinal)
                .Select(static pair => new MqttTopicMetric(pair.Key, pair.Value))
                .ToArray(),
            TopicRates = _recentTopicCounts
                .OrderByDescending(static pair => pair.Value)
                .ThenBy(static pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new MqttTopicRateMetric(pair.Key, pair.Value, pair.Value / _rateWindow.TotalSeconds))
                .ToArray()
        };
    }

    private static TimeSpan EffectiveRateDuration(TimeSpan duration)
        => duration < TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : duration;

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

    private readonly record struct MqttMetricObservation(DateTimeOffset ObservedAt, string Topic);
}
