using FluxMq.Core.Ids;
using FluxMq.Core.Models;

namespace FluxMq.Core.Mqtt;

public interface IMqttConnectionManager : IAsyncDisposable
{
    IReadOnlyDictionary<ConnectionProfileId, IFluxMqttClient> Clients { get; }

    event EventHandler<MqttClientStateChangedEventArgs>? StateChanged;

    Task<IFluxMqttClient> ConnectAsync(MqttConnectionProfile profile, CancellationToken ct = default);
    Task DisconnectAsync(ConnectionProfileId profileId, CancellationToken ct = default);
    Task RemoveAsync(ConnectionProfileId profileId, CancellationToken ct = default);
}
