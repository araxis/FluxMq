using FluxMq.Core.Models;
using System.Threading.Channels;

namespace FluxMq.Pipeline;

public sealed class MessagePipeline : IAsyncDisposable
{
    private readonly ChannelReader<MqttEnvelope> _source;
    private readonly IReadOnlyList<IMessageProcessor> _processors;
    private readonly CancellationTokenSource _cts = new();
    private Task? _pumpTask;

    public MessagePipeline(ChannelReader<MqttEnvelope> source, IEnumerable<IMessageProcessor> processors)
    {
        _source = source;
        _processors = processors.ToList();
    }

    public void Start() => _pumpTask = PumpAsync(_cts.Token);

    private async Task PumpAsync(CancellationToken ct)
    {
        await foreach (var envelope in _source.ReadAllAsync(ct))
        {
            foreach (var processor in _processors)
                await processor.ProcessAsync(envelope, ct);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        if (_pumpTask is not null)
            await _pumpTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        _cts.Dispose();
    }
}
