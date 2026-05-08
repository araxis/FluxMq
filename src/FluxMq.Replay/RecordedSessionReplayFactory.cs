using FluxMq.Core.Ids;
using FluxMq.Pipeline.Components;
using FluxMq.Storage.Repositories;

namespace FluxMq.Replay;

public sealed class RecordedSessionReplayFactory
{
    private readonly IMessageRepository _messages;
    private readonly Func<TimeSpan, CancellationToken, ValueTask>? _delayAsync;

    public RecordedSessionReplayFactory(
        IMessageRepository messages,
        Func<TimeSpan, CancellationToken, ValueTask>? delayAsync = null)
    {
        _messages = messages;
        _delayAsync = delayAsync;
    }

    public ReplaySourceComponent Create(SessionId sessionId, RecordedSessionReplayOptions? options = null)
    {
        options ??= new RecordedSessionReplayOptions();

        var envelopes = _messages
            .GetBySession(sessionId)
            .Select(message => message.ToEnvelope())
            .ToArray();

        return new ReplaySourceComponent(
            envelopes,
            options.NodeId,
            options.Speed,
            options.BoundedCapacity,
            _delayAsync);
    }
}
