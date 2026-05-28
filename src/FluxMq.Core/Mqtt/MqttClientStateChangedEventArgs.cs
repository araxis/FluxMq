using FluxMq.Core.Ids;
using FluxMq.Core.Models;

namespace FluxMq.Core.Mqtt;

public sealed class MqttClientStateChangedEventArgs(
    ConnectionProfileId profileId,
    MqttConnectionProfile profile,
    MqttClientState state) : EventArgs
{
    public ConnectionProfileId ProfileId { get; } = profileId;
    public MqttConnectionProfile Profile { get; } = profile;
    public MqttClientState State { get; } = state;
}
