using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Core.Session;
using FluxMq.Pipeline.Components;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Components.MessageSource;

/// <summary>
/// Trigger node: bound to a connection (session + broadcast stream),
/// owns its own subscription list, and emits envelopes that match its filters.
///
/// On start it installs its subscriptions on the broker and links a topic-filter
/// block to the connection's broadcast. Multiple triggers may share one connection;
/// each gets every envelope but only forwards the ones matching its own filters.
/// </summary>
public sealed class MqttTriggerComponent : IFlowNode, IAsyncDisposable
{
    private readonly IMqttSession _session;
    private readonly ISourceBlock<MqttEnvelope> _connectionStream;
    private readonly IReadOnlyList<MqttSubscription> _subscriptions;
    private readonly string[] _topicFilters;
    private readonly BufferBlock<MqttEnvelope> _output;
    private readonly BroadcastBlock<FlowError> _errors;
    private readonly CancellationTokenSource _cts = new();
    private IDisposable? _link;
    private int _started;

    public MqttTriggerComponent(
        IMqttSession session,
        ISourceBlock<MqttEnvelope> connectionStream,
        IEnumerable<MqttSubscription> subscriptions,
        FlowNodeId? id = null,
        int boundedCapacity = 1000)
    {
        Id = id ?? FlowNodeId.New();
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _connectionStream = connectionStream ?? throw new ArgumentNullException(nameof(connectionStream));
        _subscriptions = subscriptions?.ToArray() ?? throw new ArgumentNullException(nameof(subscriptions));
        if (_subscriptions.Count == 0)
        {
            throw new ArgumentException("A trigger must declare at least one subscription.", nameof(subscriptions));
        }
        _topicFilters = _subscriptions.Select(static s => s.TopicFilter).ToArray();
        _errors = new BroadcastBlock<FlowError>(static error => error);
        _output = new BufferBlock<MqttEnvelope>(new DataflowBlockOptions
        {
            BoundedCapacity = boundedCapacity
        });

        _output.Completion.ContinueWith(
            _ => _errors.Complete(),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    /// <summary>Convenience constructor when you have a connection component.</summary>
    public MqttTriggerComponent(
        MqttConnectionComponent connection,
        IEnumerable<MqttSubscription> subscriptions,
        FlowNodeId? id = null,
        int boundedCapacity = 1000)
        : this(
            (connection ?? throw new ArgumentNullException(nameof(connection))).Session,
            connection.Messages,
            subscriptions,
            id,
            boundedCapacity)
    {
    }

    public FlowNodeId Id { get; }
    public ISourceBlock<FlowError> Errors => _errors;
    public Task Completion => _output.Completion;
    public ISourceBlock<MqttEnvelope> Output => _output;
    public IReadOnlyList<MqttSubscription> Subscriptions => _subscriptions;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
        {
            return;
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
        var ct = linkedCts.Token;

        try
        {
            foreach (var subscription in _subscriptions)
            {
                await _session.SubscribeAsync(
                    subscription.TopicFilter,
                    subscription.QualityOfService,
                    subscription.ReceiveRetainedMessages,
                    subscription.RetainAsPublished,
                    ct).ConfigureAwait(false);
            }

            var pump = new ActionBlock<MqttEnvelope>(async envelope =>
            {
                if (MqttTopicFilterMatcher.MatchesAny(_topicFilters, envelope.Topic))
                {
                    await _output.SendAsync(envelope, _cts.Token).ConfigureAwait(false);
                }
            }, new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = DataflowBlockOptions.Unbounded,
                CancellationToken = _cts.Token
            });

            _link = _connectionStream.LinkTo(pump, new DataflowLinkOptions { PropagateCompletion = true });

            _ = pump.Completion.ContinueWith(
                _ => _output.Complete(),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            PublishError(FlowErrorCodes.NodeFaulted, "MQTT trigger failed to start.", exception);
            _output.Complete();
            throw;
        }
    }

    public void Complete()
    {
        Interlocked.Exchange(ref _started, 1);
        _link?.Dispose();
        _cts.Cancel();
        _output.Complete();
    }

    public void Fault(Exception exception)
    {
        Interlocked.Exchange(ref _started, 1);
        _link?.Dispose();
        _cts.Cancel();
        PublishError(FlowErrorCodes.NodeFaulted, "MQTT trigger faulted.", exception);
        ((IDataflowBlock)_output).Fault(exception);
    }

    public async ValueTask DisposeAsync()
    {
        Complete();
        await Completion.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        _cts.Dispose();
    }

    private void PublishError(int code, string message, Exception exception)
    {
        _errors.Post(new FlowError
        {
            NodeId = Id,
            Code = code,
            Message = message,
            Exception = exception,
            Context = _session.Profile.Name
        });
    }
}
