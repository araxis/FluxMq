using FluxMq.Core.Models;
using FluxMq.Core.Mqtt;

namespace FluxMq.UI.Services;

public sealed class ManagedConnection
{
    public Guid Id { get; } = Guid.NewGuid();
    public string ResourceName { get; }
    public MqttConnectionProfile Profile { get; }
    public string Subscription { get; }
    public MqttClientState State { get; internal set; } = MqttClientState.Disconnected;
    public string? LastError { get; internal set; }

    public ManagedConnection(
        MqttConnectionProfile profile,
        string subscription = LiveMqttWorkspaceService.DefaultBrokerMonitorSubscription,
        string? resourceName = null)
    {
        ResourceName = string.IsNullOrWhiteSpace(resourceName)
            ? DeriveResourceName(profile)
            : resourceName.Trim();
        Profile = profile;
        Subscription = subscription;
    }

    private static string DeriveResourceName(MqttConnectionProfile profile)
        => string.IsNullOrWhiteSpace(profile.Name)
            ? "broker"
            : profile.Name.Trim().ToLowerInvariant().Replace(' ', '-');
}
