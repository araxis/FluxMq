using FluxMq.Core.Ids;
using FluxMq.Core.Models;

namespace FluxMq.Storage.Repositories;

public interface IConnectionProfileRepository
{
    MqttConnectionProfile? Get(ConnectionProfileId id);
    IReadOnlyList<MqttConnectionProfile> GetAll();
    void Save(MqttConnectionProfile profile);
    bool Delete(ConnectionProfileId id);
}
