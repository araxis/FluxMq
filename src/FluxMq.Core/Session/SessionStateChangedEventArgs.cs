using FluxMq.Core.Ids;
using FluxMq.Core.Models;

namespace FluxMq.Core.Session;

public sealed class SessionStateChangedEventArgs(
    ConnectionProfileId profileId,
    MqttConnectionProfile profile,
    MqttSessionState state) : EventArgs
{
    public ConnectionProfileId ProfileId { get; } = profileId;
    public MqttConnectionProfile Profile { get; } = profile;
    public MqttSessionState State { get; } = state;
}
