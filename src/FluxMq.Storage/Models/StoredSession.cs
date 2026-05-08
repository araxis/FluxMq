using FluxMq.Core.Models;

namespace FluxMq.Storage.Models;

public sealed class StoredSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProfileId { get; set; }
    public string ProfileName { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndedAt { get; set; }

    public static StoredSession From(MqttConnectionProfile profile) => new()
    {
        ProfileId = profile.Id,
        ProfileName = profile.Name
    };
}
