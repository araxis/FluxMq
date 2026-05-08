using FluxMq.Core.Models;
using System.Threading.Channels;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Pipeline;

public sealed class MqttPipeline : IAsyncDisposable
{
    private readonly BufferBlock<MqttEnvelope> _buffer;
    private readonly BroadcastBlock<MqttEnvelope> _broadcast;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _feedTask;

    /// <summary>
    /// Linkable fan-out source. Consumers attach their own ActionBlocks here.
    /// </summary>
    public ISourceBlock<MqttEnvelope> Output => _broadcast;

    public MqttPipeline(ChannelReader<MqttEnvelope> source, int boundedCapacity = 1000)
    {
        _buffer = new BufferBlock<MqttEnvelope>(new DataflowBlockOptions
        {
            BoundedCapacity = boundedCapacity,
            CancellationToken = _cts.Token
        });

        // MqttEnvelope is an immutable record — identity clone is safe
        _broadcast = new BroadcastBlock<MqttEnvelope>(
            static envelope => envelope,
            new DataflowBlockOptions { CancellationToken = _cts.Token });

        _buffer.LinkTo(_broadcast, new DataflowLinkOptions { PropagateCompletion = true });

        _feedTask = FeedAsync(source, _cts.Token);
    }

    public IDisposable LinkTo(ITargetBlock<MqttEnvelope> target, bool propagateCompletion = true)
        => _broadcast.LinkTo(target, new DataflowLinkOptions { PropagateCompletion = propagateCompletion });

    private async Task FeedAsync(ChannelReader<MqttEnvelope> source, CancellationToken ct)
    {
        try
        {
            await foreach (var envelope in source.ReadAllAsync(ct))
                await _buffer.SendAsync(envelope, ct);
        }
        catch (OperationCanceledException) { }
        finally
        {
            _buffer.Complete();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        await _feedTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        _cts.Dispose();
    }
}
