using FluxMq.Core.Ids;
using FluxMq.Core.Models;

namespace FluxMq.Storage.Models;

public sealed class StoredSession
{
    public SessionId Id { get; set; } = SessionId.New();
    public string Name { get; set; } = string.Empty;
    public string ProjectName { get; set; } = "Default";
    public ConnectionProfileId ProfileId { get; set; }
    public string ProfileName { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndedAt { get; set; }

    public static StoredSession From(MqttConnectionProfile profile, string? name = null, string? projectName = null) => new()
    {
        Name = string.IsNullOrWhiteSpace(name)
            ? $"{profile.Name} {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss}"
            : name,
        ProjectName = string.IsNullOrWhiteSpace(projectName) ? "Default" : projectName,
        ProfileId = profile.Id,
        ProfileName = profile.Name
    };
}
