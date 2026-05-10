using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Core.Session;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Pipeline.Components.MessageSource;

/// <summary>
/// Resource node that owns an <see cref="IMqttSession"/>: connection settings,
/// connect / disconnect lifecycle, reconnect (handled by the session itself).
///
/// On start, pumps the session's single-consumer message channel into a
/// <see cref="BroadcastBlock{T}"/> so multiple triggers can fan out from the
/// same connection without racing for the channel.
///
/// Topic subscriptions are NOT this node's concern. <see cref="MqttTriggerComponent"/>
/// instances reference a connection by name, install their own filters against the
/// session, and link their inputs to <see cref="Messages"/>.
/// </summary>
public sealed class MqttConnectionComponent : IFlowNode, IFlowStartable, IAsyncDisposable
{
    private readonly IMqttSession _session;
    private readonly BroadcastBlock<MqttEnvelope> _broadcast;
    private readonly BroadcastBlock<FlowError> _errors;
    private readonly CancellationTokenSource _cts = new();
    private readonly bool _disposeSessionOnDispose;
    private Task? _pumpTask;
    private int _started;

    public MqttConnectionComponent(
        IMqttSession session,
        bool disposeSessionOnDispose = true,
        FlowNodeId? id = null)
    {
        Id = id ?? FlowNodeId.New();
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _disposeSessionOnDispose = disposeSessionOnDispose;
        _errors = new BroadcastBlock<FlowError>(static error => error);
        _broadcast = new BroadcastBlock<MqttEnvelope>(static envelope => envelope);
    }

    public FlowNodeId Id { get; }
    public ISourceBlock<FlowError> Errors => _errors;
    public Task Completion => _broadcast.Completion;

    /// <summary>The live session this connection owns. Triggers subscribe through it.</summary>
    public IMqttSession Session => _session;

    /// <summary>Broadcast of every envelope received by the session. Triggers link their filters here.</summary>
    public ISourceBlock<MqttEnvelope> Messages => _broadcast;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
        {
            return Task.CompletedTask;
        }

        _pumpTask = RunAsync(cancellationToken);
        return Task.CompletedTask;
    }

    public void Complete()
    {
        if (Interlocked.Exchange(ref _started, 1) == 0)
        {
            _broadcast.Complete();
            _errors.Complete();
            return;
        }

        _cts.Cancel();
        _broadcast.Complete();
        _errors.Complete();
    }

    public void Fault(Exception exception)
    {
        Interlocked.Exchange(ref _started, 1);
        _cts.Cancel();
        PublishError(FlowErrorCodes.NodeFaulted, "MQTT connection faulted.", exception);
        _errors.Complete();
        ((IDataflowBlock)_broadcast).Fault(exception);
    }

    public async ValueTask DisposeAsync()
    {
        Complete();
        if (_pumpTask is not null)
        {
            await _pumpTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
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
            if (_session.State is not MqttSessionState.Connected and not MqttSessionState.Connecting)
            {
                await _session.ConnectAsync(ct).ConfigureAwait(false);
            }

            await foreach (var message in _session.Messages.ReadAllAsync(ct).ConfigureAwait(false))
            {
                if (!await _broadcast.SendAsync(message, ct).ConfigureAwait(false))
                {
                    break;
                }
            }

            _broadcast.Complete();
            _errors.Complete();
        }
        catch (OperationCanceledException)
        {
            _broadcast.Complete();
            _errors.Complete();
        }
        catch (Exception exception)
        {
            PublishError(FlowErrorCodes.ProcessingFailed, "MQTT connection failed.", exception);
            _errors.Complete();
            _broadcast.Complete();
        }
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
