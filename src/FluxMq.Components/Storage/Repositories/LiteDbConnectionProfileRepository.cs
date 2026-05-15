using FluxMq.Core.Ids;
using FluxMq.Core.Models;

namespace FluxMq.Components.Storage.Repositories;

public sealed class LiteDbConnectionProfileRepository : IConnectionProfileRepository
{
    private readonly FluxDbContext _ctx;

    public LiteDbConnectionProfileRepository(FluxDbContext ctx) => _ctx = ctx;

    public MqttConnectionProfile? Get(ConnectionProfileId id)
        => _ctx.ConnectionProfiles.FindById(id.Value);

    public IReadOnlyList<MqttConnectionProfile> GetAll()
        => _ctx.ConnectionProfiles.FindAll().ToList();

    public void Save(MqttConnectionProfile profile)
        => _ctx.ConnectionProfiles.Upsert(profile);

    public bool Delete(ConnectionProfileId id)
        => _ctx.ConnectionProfiles.Delete(id.Value);
}
