using FluxMq.Core.Models;
using FluxMq.Storage.Models;
using LiteDB;

namespace FluxMq.Storage;

public sealed class FluxDbContext : IDisposable
{
    private readonly ILiteDatabase _db;

    public ILiteCollection<MqttConnectionProfile> ConnectionProfiles
        => _db.GetCollection<MqttConnectionProfile>("connection_profiles");

    public ILiteCollection<StoredSession> Sessions
        => _db.GetCollection<StoredSession>("sessions");

    public ILiteCollection<StoredMessage> Messages
        => _db.GetCollection<StoredMessage>("messages");

    /// <summary>Production constructor — opens or creates a LiteDB file.</summary>
    public FluxDbContext(string path = "flux.db")
        : this(new LiteDatabase(path)) { }

    /// <summary>Test constructor — accepts any ILiteDatabase (e.g. in-memory).</summary>
    public FluxDbContext(ILiteDatabase database)
    {
        _db = database;
        EnsureIndexes();
    }

    private void EnsureIndexes()
    {
        Messages.EnsureIndex(m => m.SessionId);
        Messages.EnsureIndex(m => m.Topic);
        Sessions.EnsureIndex(s => s.ProfileId);
    }

    public void Dispose() => _db.Dispose();
}
