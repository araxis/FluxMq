using FluxMq.Core.Models;

namespace FluxMq.Core.Session;

public sealed class SessionStateChangedEventArgs(Guid sessionId, MqttConnectionProfile profile, MqttSessionState state) : EventArgs
{
    public Guid SessionId { get; } = sessionId;
    public MqttConnectionProfile Profile { get; } = profile;
    public MqttSessionState State { get; } = state;
}
