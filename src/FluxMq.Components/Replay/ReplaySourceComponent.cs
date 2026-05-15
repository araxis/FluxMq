using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Pipeline.Components;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Components.Replay;

public sealed class ReplaySourceComponent : IFlowNode, IAsyncDisposable
{
    private readonly IReadOnlyList<MqttEnvelope> _messages;
    private readonly BufferBlock<MqttEnvelope> _output;
    private readonly BroadcastBlock<FlowError> _errors;
    private readonly Func<TimeSpan, CancellationToken, ValueTask> _delayAsync;
    private readonly CancellationTokenSource _cts = new();
    private Task? _producerTask;
    private int _started;

    public ReplaySourceComponent(
        IEnumerable<MqttEnvelope> messages,
        FlowNodeId? id = null,
        double speed = 1,
        int boundedCapacity = 1000,
        Func<TimeSpan, CancellationToken, ValueTask>? delayAsync = null)
    {
        if (speed <= 0 || double.IsNaN(speed) || double.IsInfinity(speed))
        {
            throw new ArgumentOutOfRangeException(nameof(speed), speed, "Replay speed must be a positive finite value.");
        }

        Id = id ?? FlowNodeId.New();
        Speed = speed;
        _messages = messages.OrderBy(message => message.ReceivedAt).ToArray();
        _delayAsync = delayAsync ?? DefaultDelayAsync;
        _errors = new BroadcastBlock<FlowError>(static error => error);
        _output = new BufferBlock<MqttEnvelope>(new DataflowBlockOptions
        {
            BoundedCapacity = boundedCapacity,
            CancellationToken = _cts.Token
        });

        _output.Completion.ContinueWith(
            _ => _errors.Complete(),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    public FlowNodeId Id { get; }
    public double Speed { get; }
    public ISourceBlock<FlowError> Errors => _errors;
    public Task Completion => _output.Completion;
    public ISourceBlock<MqttEnvelope> Output => _output;

    public Task StartAsync(CancellationToken ct = default)
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
        {
            return _producerTask ?? Completion;
        }

        _producerTask = RunAsync(ct);
        return _producerTask;
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
        PublishError(FlowErrorCodes.NodeFaulted, "Replay source faulted.", exception);
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
            DateTimeOffset? previousReceivedAt = null;

            foreach (var message in _messages)
            {
                ct.ThrowIfCancellationRequested();

                if (previousReceivedAt is not null)
                {
                    var delay = ScaleDelay(message.ReceivedAt - previousReceivedAt.Value);
                    if (delay > TimeSpan.Zero)
                    {
                        await _delayAsync(delay, ct).ConfigureAwait(false);
                    }
                }

                await _output.SendAsync(message, ct).ConfigureAwait(false);
                previousReceivedAt = message.ReceivedAt;
            }

            _output.Complete();
        }
        catch (OperationCanceledException)
        {
            _output.Complete();
        }
        catch (Exception exception)
        {
            PublishError(FlowErrorCodes.ProcessingFailed, "Replay source failed.", exception);
            _output.Complete();
        }
    }

    private TimeSpan ScaleDelay(TimeSpan originalDelay)
    {
        if (originalDelay <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        return TimeSpan.FromTicks((long)Math.Ceiling(originalDelay.Ticks / Speed));
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

    private static async ValueTask DefaultDelayAsync(TimeSpan delay, CancellationToken ct)
    {
        await Task.Delay(delay, ct).ConfigureAwait(false);
    }
}
