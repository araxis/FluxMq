using FluxMq.Core.Models;
using FluxMq.Storage.Models;

namespace FluxMq.Storage.Repositories;

public interface ISessionRepository
{
    StoredSession Start(MqttConnectionProfile profile);
    void End(Guid sessionId);
    StoredSession? Get(Guid sessionId);
    IReadOnlyList<StoredSession> GetAll();
    bool Delete(Guid sessionId);
}
