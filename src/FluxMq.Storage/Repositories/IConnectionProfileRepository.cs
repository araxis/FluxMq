using FluxMq.Core.Models;

namespace FluxMq.Storage.Repositories;

public interface IConnectionProfileRepository
{
    MqttConnectionProfile? Get(Guid id);
    IReadOnlyList<MqttConnectionProfile> GetAll();
    void Save(MqttConnectionProfile profile);
    bool Delete(Guid id);
}
