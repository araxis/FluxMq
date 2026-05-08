using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Storage.Models;

namespace FluxMq.Storage.Repositories;

public interface ISessionRepository
{
    StoredSession Start(MqttConnectionProfile profile);
    void End(SessionId sessionId);
    StoredSession? Get(SessionId sessionId);
    IReadOnlyList<StoredSession> GetAll();
    bool Delete(SessionId sessionId);
}
