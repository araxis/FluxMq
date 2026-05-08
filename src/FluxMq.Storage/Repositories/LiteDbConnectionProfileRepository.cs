using FluxMq.Core.Models;

namespace FluxMq.Storage.Repositories;

public sealed class LiteDbConnectionProfileRepository : IConnectionProfileRepository
{
    private readonly FluxDbContext _ctx;

    public LiteDbConnectionProfileRepository(FluxDbContext ctx) => _ctx = ctx;

    public MqttConnectionProfile? Get(Guid id)
        => _ctx.ConnectionProfiles.FindById(id);

    public IReadOnlyList<MqttConnectionProfile> GetAll()
        => _ctx.ConnectionProfiles.FindAll().ToList();

    public void Save(MqttConnectionProfile profile)
        => _ctx.ConnectionProfiles.Upsert(profile);

    public bool Delete(Guid id)
        => _ctx.ConnectionProfiles.Delete(id);
}
