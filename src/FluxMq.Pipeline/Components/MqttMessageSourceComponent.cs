using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Core.Session;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Pipeline.Components;

public sealed class MqttMessageSourceComponent : IFlowNode, IFlowStartable, IAsyncDisposable
{
    private readonly IMqttSession _session;
    private readonly BufferBlock<MqttEnvelope> _output;
    private readonly BroadcastBlock<FlowError> _errors;
    private readonly CancellationTokenSource _cts = new();
    private readonly IReadOnlyList<MqttSubscription> _subscriptions;
    private readonly bool _connectOnStart;
    private readonly bool _disposeSessionOnDispose;
    private Task? _producerTask;
    private int _started;

    public MqttMessageSourceComponent(
        IMqttSession session,
        IEnumerable<MqttSubscription>? subscriptions = null,
        bool connectOnStart = false,
        bool disposeSessionOnDispose = false,
        FlowNodeId? id = null,
        int boundedCapacity = 1000)
    {
        Id = id ?? FlowNodeId.New();
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _subscriptions = subscriptions?.ToArray() ?? [];
        _connectOnStart = connectOnStart;
        _disposeSessionOnDispose = disposeSessionOnDispose;
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

    public FlowNodeId Id { get; }
    public ISourceBlock<FlowError> Errors => _errors;
    public Task Completion => _output.Completion;
    public ISourceBlock<MqttEnvelope> Output => _output;

    public Task StartAsync(CancellationToken ct = default)
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
        {
            return Task.CompletedTask;
        }

        _producerTask = RunAsync(ct);
        return Task.CompletedTask;
    }

    public void Complete()
    {
        if (Interlocked.Exchange(ref _started, 1) == 0)
        {
            _output.Complete();
            return;
        }

        _cts.Cancel();
        _output.Complete();
    }

    public void Fault(Exception exception)
    {
        Interlocked.Exchange(ref _started, 1);
        _cts.Cancel();
        PublishError(FlowErrorCodes.NodeFaulted, "MQTT message source faulted.", exception);
        ((IDataflowBlock)_output).Fault(exception);
    }

    public async ValueTask DisposeAsync()
    {
        Complete();
        if (_producerTask is not null)
        {
            await _producerTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }

        await Completion.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        if (_disposeSessionOnDispose)
        {
            await _session.DisposeAsync().ConfigureAwait(false);
        }

        _cts.Dispose();
    }

    private async Task RunAsync(CancellationToken externalToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, externalToken);
        var ct = linkedCts.Token;

        try
        {
            if (_connectOnStart)
            {
                await ConnectAndSubscribeAsync(ct).ConfigureAwait(false);
            }

            await foreach (var message in _session.Messages.ReadAllAsync(ct).ConfigureAwait(false))
            {
                if (!await _output.SendAsync(message, ct).ConfigureAwait(false))
                {
                    break;
                }
            }

            _output.Complete();
        }
        catch (OperationCanceledException)
        {
            _output.Complete();
        }
        catch (Exception exception)
        {
            PublishError(FlowErrorCodes.ProcessingFailed, "MQTT message source failed.", exception, _session.Profile.Name);
            _output.Complete();
        }
    }

    private async Task ConnectAndSubscribeAsync(CancellationToken ct)
    {
        if (_session.State is not MqttSessionState.Connected and not MqttSessionState.Connecting)
        {
            await _session.ConnectAsync(ct).ConfigureAwait(false);
        }

        foreach (var subscription in _subscriptions)
        {
            await _session.SubscribeAsync(subscription.TopicFilter, subscription.QualityOfService, ct).ConfigureAwait(false);
        }
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
}
