using FluxMq.Core.Ids;
using FluxMq.Core.Models;

namespace FluxMq.Storage.Models;

public sealed class StoredSession
{
    public SessionId Id { get; set; } = SessionId.New();
    public ConnectionProfileId ProfileId { get; set; }
    public string ProfileName { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndedAt { get; set; }

    public static StoredSession From(MqttConnectionProfile profile) => new()
    {
        ProfileId = profile.Id,
        ProfileName = profile.Name
    };
}
