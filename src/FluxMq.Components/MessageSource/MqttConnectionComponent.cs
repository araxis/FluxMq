using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Core.Mqtt;
using FluxMq.Pipeline.Components;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Components.MessageSource;

/// <summary>
/// Resource node that owns an <see cref="IMqttBrokerClient"/>: connection settings,
/// connect / disconnect lifecycle, reconnect (handled by the client itself).
///
/// On start, pumps the client's single-consumer message channel into a
/// <see cref="BroadcastBlock{T}"/> so multiple triggers can fan out from the
/// same connection without racing for the channel.
///
/// Topic subscriptions are NOT this node's concern. <see cref="MqttTriggerComponent"/>
/// instances reference a connection by name, install their own filters against the
/// client, and link their inputs to <see cref="Messages"/>.
/// </summary>
public sealed class MqttConnectionComponent : IFlowNode, IAsyncDisposable
{
    private readonly IMqttBrokerClient _client;
    private readonly BroadcastBlock<MqttEnvelope> _broadcast;
    private readonly BroadcastBlock<FlowError> _errors;
    private readonly CancellationTokenSource _cts = new();
    private readonly bool _disposeClientOnDispose;
    private Task? _pumpTask;
    private int _started;

    public MqttConnectionComponent(
        IMqttBrokerClient client,
        bool disposeClientOnDispose = true,
        FlowNodeId? id = null)
    {
        Id = id ?? FlowNodeId.New();
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _disposeClientOnDispose = disposeClientOnDispose;
        _errors = new BroadcastBlock<FlowError>(static error => error);
        _broadcast = new BroadcastBlock<MqttEnvelope>(static envelope => envelope);
    }

    public FlowNodeId Id { get; }
    public ISourceBlock<FlowError> Errors => _errors;
    public Task Completion => _broadcast.Completion;

    /// <summary>The live MQTT client this connection owns. Triggers subscribe through it.</summary>
    public IMqttBrokerClient Client => _client;

    /// <summary>Broadcast of every envelope received by the client. Triggers link their filters here.</summary>
    public ISourceBlock<MqttEnvelope> Messages => _broadcast;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
        {
            return;
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
        try
        {
            if (_client.State is not MqttClientState.Connected)
            {
                await _client.ConnectAsync(linkedCts.Token).ConfigureAwait(false);
            }

            if (_client.State is not MqttClientState.Connected)
            {
                throw new InvalidOperationException("MQTT connection did not reach the connected state.");
            }

            _pumpTask = PumpMessagesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _broadcast.Complete();
            _errors.Complete();
            throw;
        }
        catch (Exception exception)
        {
            PublishError(FlowErrorCodes.ProcessingFailed, "MQTT connection failed.", exception);
            _errors.Complete();
            _broadcast.Complete();
            throw;
        }
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
        if (_disposeClientOnDispose)
        {
            await _client.DisposeAsync().ConfigureAwait(false);
        }
        _cts.Dispose();
    }

    private async Task PumpMessagesAsync(CancellationToken externalToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, externalToken);
        var ct = linkedCts.Token;

        try
        {
            await foreach (var message in _client.Messages.ReadAllAsync(ct).ConfigureAwait(false))
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
            Context = _client.Profile.Name
        });
    }
}
