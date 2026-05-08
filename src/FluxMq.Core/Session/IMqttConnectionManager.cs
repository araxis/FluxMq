using FluxMq.Core.Ids;
using FluxMq.Core.Models;

namespace FluxMq.Core.Session;

public interface IMqttConnectionManager : IAsyncDisposable
{
    IReadOnlyDictionary<ConnectionProfileId, IMqttSession> Sessions { get; }

    event EventHandler<SessionStateChangedEventArgs>? StateChanged;

    Task<IMqttSession> ConnectAsync(MqttConnectionProfile profile, CancellationToken ct = default);
    Task DisconnectAsync(ConnectionProfileId profileId, CancellationToken ct = default);
    Task RemoveAsync(ConnectionProfileId profileId, CancellationToken ct = default);
}
