using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Storage.Models;

namespace FluxMq.Storage.Repositories;

public sealed class LiteDbSessionRepository : ISessionRepository
{
    private readonly FluxDbContext _ctx;

    public LiteDbSessionRepository(FluxDbContext ctx) => _ctx = ctx;

    public StoredSession Start(MqttConnectionProfile profile)
    {
        var session = StoredSession.From(profile);
        _ctx.Sessions.Insert(session);
        return session;
    }

    public void End(SessionId sessionId)
    {
        var session = _ctx.Sessions.FindById(sessionId.Value);
        if (session is null) return;
        session.EndedAt = DateTimeOffset.UtcNow;
        _ctx.Sessions.Update(session);
    }

    public StoredSession? Get(SessionId sessionId)
        => _ctx.Sessions.FindById(sessionId.Value);

    public IReadOnlyList<StoredSession> GetAll()
        => _ctx.Sessions.FindAll().OrderByDescending(s => s.StartedAt).ToList();

    public bool Delete(SessionId sessionId)
        => _ctx.Sessions.Delete(sessionId.Value);
}
