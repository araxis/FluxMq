using FluxMq.Core.Models;
using FluxMq.Core.Session;

namespace FluxMq.UI.Services;

public sealed class ManagedConnection
{
    public Guid Id { get; } = Guid.NewGuid();
    public MqttConnectionProfile Profile { get; }
    public string Subscription { get; }
    public MqttSessionState State { get; internal set; } = MqttSessionState.Disconnected;
    public string? LastError { get; internal set; }

    public ManagedConnection(MqttConnectionProfile profile, string subscription = "#")
    {
        Profile = profile;
        Subscription = subscription;
    }
}
