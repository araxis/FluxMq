using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Components.Storage.Models;
using LiteDB;
using System.Runtime.CompilerServices;

namespace FluxMq.Components.Storage;

public sealed class FluxDbContext : IDisposable
{
    private static readonly object MapperRegistrationLock = new();
    private static readonly ConditionalWeakTable<BsonMapper, object> RegisteredMappers = new();
    private static readonly object MapperRegistrationMarker = new();

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
        RegisterMappers();
        EnsureIndexes();
    }

    private void RegisterMappers()
    {
        lock (MapperRegistrationLock)
        {
            if (RegisteredMappers.TryGetValue(_db.Mapper, out _))
            {
                return;
            }

            _db.Mapper.RegisterType<ConnectionProfileId>(
                serialize: id => new BsonValue(id.Value),
                deserialize: bson => new ConnectionProfileId(bson.AsGuid));

            _db.Mapper.RegisterType<SessionId>(
                serialize: id => new BsonValue(id.Value),
                deserialize: bson => new SessionId(bson.AsGuid));

            _db.Mapper.RegisterType<MessageId>(
                serialize: id => new BsonValue(id.Value),
                deserialize: bson => new MessageId(bson.AsGuid));

            RegisteredMappers.Add(_db.Mapper, MapperRegistrationMarker);
        }
    }

    private void EnsureIndexes()
    {
        Messages.EnsureIndex(m => m.SessionId);
        Messages.EnsureIndex(m => m.Sequence);
        Messages.EnsureIndex(m => m.ReceivedAt);
        Messages.EnsureIndex(m => m.Topic);
        Sessions.EnsureIndex(nameof(StoredSession.ProfileId));
        Sessions.EnsureIndex(nameof(StoredSession.ProjectName));
    }

    public void Dispose() => _db.Dispose();
}
