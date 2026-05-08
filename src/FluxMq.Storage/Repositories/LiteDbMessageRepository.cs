using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Storage.Models;

namespace FluxMq.Storage.Repositories;

public sealed class LiteDbMessageRepository : IMessageRepository
{
    private readonly FluxDbContext _ctx;

    public LiteDbMessageRepository(FluxDbContext ctx) => _ctx = ctx;

    public void Add(SessionId sessionId, MqttEnvelope envelope)
        => _ctx.Messages.Insert(StoredMessage.From(sessionId, envelope));

    public void AddBatch(SessionId sessionId, IEnumerable<MqttEnvelope> envelopes)
        => _ctx.Messages.InsertBulk(envelopes.Select(e => StoredMessage.From(sessionId, e)));

    public IReadOnlyList<StoredMessage> GetBySession(SessionId sessionId)
        => _ctx.Messages.Find(m => m.SessionId == sessionId)
                        .OrderBy(m => m.ReceivedAt)
                        .ToList();

    public IReadOnlyList<StoredMessage> GetByTopic(string topic)
        => _ctx.Messages.Find(m => m.Topic == topic)
                        .OrderBy(m => m.ReceivedAt)
                        .ToList();

    public long CountBySession(SessionId sessionId)
        => _ctx.Messages.Count(m => m.SessionId == sessionId);
}
