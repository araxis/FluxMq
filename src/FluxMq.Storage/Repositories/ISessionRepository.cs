using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Storage.Models;

namespace FluxMq.Storage.Repositories;

public interface ISessionRepository
{
    StoredSession Start(MqttConnectionProfile profile, string? name = null, string? projectName = null);
    void End(SessionId sessionId);
    StoredSession? Get(SessionId sessionId);
    IReadOnlyList<StoredSession> GetAll();
    bool Delete(SessionId sessionId);
}
