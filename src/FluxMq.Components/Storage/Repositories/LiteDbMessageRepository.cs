using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Components.Storage.Models;
using System.Runtime.CompilerServices;

namespace FluxMq.Components.Storage.Repositories;

public sealed class LiteDbMessageRepository : IMessageRepository
{
    private readonly FluxDbContext _ctx;
    private readonly object _sequenceLock = new();

    public LiteDbMessageRepository(FluxDbContext ctx) => _ctx = ctx;

    public void Add(SessionId sessionId, MqttEnvelope envelope)
    {
        lock (_sequenceLock)
        {
            var nextSequence = CountBySession(sessionId) + 1;
            _ctx.Messages.Insert(StoredMessage.From(sessionId, envelope, nextSequence));
        }
    }

    public void AddBatch(SessionId sessionId, IEnumerable<MqttEnvelope> envelopes)
    {
        lock (_sequenceLock)
        {
            var sequence = CountBySession(sessionId);
            var stored = envelopes
                .Select(envelope => StoredMessage.From(sessionId, envelope, ++sequence))
                .ToArray();

            if (stored.Length > 0)
            {
                _ctx.Messages.InsertBulk(stored);
            }
        }
    }

    public IReadOnlyList<StoredMessage> GetBySession(SessionId sessionId)
        => _ctx.Messages.Find(m => m.SessionId == sessionId)
                        .OrderBy(m => m.ReceivedAt)
                        .ThenBy(m => m.Sequence)
                        .ToList();

    public IReadOnlyList<StoredMessage> GetByTopic(string topic)
        => _ctx.Messages.Find(m => m.Topic == topic)
                        .OrderBy(m => m.ReceivedAt)
                        .ThenBy(m => m.Sequence)
                        .ToList();

    public async IAsyncEnumerable<StoredMessage> ReadBySessionAsync(
        SessionId sessionId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var message in GetBySession(sessionId))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return message;
            await Task.Yield();
        }
    }

    public async IAsyncEnumerable<MqttEnvelope> ReadEnvelopesBySessionAsync(
        SessionId sessionId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var message in ReadBySessionAsync(sessionId, cancellationToken).ConfigureAwait(false))
        {
            yield return message.ToEnvelope();
        }
    }

    public long CountBySession(SessionId sessionId)
        => _ctx.Messages.Count(m => m.SessionId == sessionId);
}
