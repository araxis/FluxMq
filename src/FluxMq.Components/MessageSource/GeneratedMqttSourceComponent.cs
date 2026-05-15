using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Pipeline.Components;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Components.MessageSource;

public sealed class GeneratedMqttSourceComponent : IFlowNode, IAsyncDisposable
{
    private readonly IReadOnlyList<MqttEnvelope> _messages;
    private readonly BufferBlock<MqttEnvelope> _output;
    private readonly BroadcastBlock<FlowError> _errors;
    private readonly CancellationTokenSource _cts = new();
    private Task? _producerTask;
    private int _started;

    public GeneratedMqttSourceComponent(
        IEnumerable<MqttEnvelope> messages,
        FlowNodeId? id = null,
        int boundedCapacity = 1000)
    {
        Id = id ?? FlowNodeId.New();
        _messages = messages?.ToArray() ?? throw new ArgumentNullException(nameof(messages));
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

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
        {
            return Task.CompletedTask;
        }

        _producerTask = RunAsync(cancellationToken);
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
        _errors.Post(new FlowError
        {
            NodeId = Id,
            Code = FlowErrorCodes.NodeFaulted,
            Message = "Generated source faulted.",
            Exception = exception
        });
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
        _cts.Dispose();
    }

    private async Task RunAsync(CancellationToken externalToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, externalToken);
        var ct = linkedCts.Token;

        try
        {
            foreach (var message in _messages)
            {
                ct.ThrowIfCancellationRequested();
                await _output.SendAsync(message, ct).ConfigureAwait(false);
            }

            _output.Complete();
        }
        catch (OperationCanceledException)
        {
            _output.Complete();
        }
        catch (Exception exception)
        {
            _errors.Post(new FlowError
            {
                NodeId = Id,
                Code = FlowErrorCodes.ProcessingFailed,
                Message = "Generated source failed.",
                Exception = exception
            });
            _output.Complete();
        }
    }
}
