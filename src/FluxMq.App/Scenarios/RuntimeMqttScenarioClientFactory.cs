using FluxMq.Core.Models;
using FluxMq.Core.Mqtt;
using FluxFlow.Components.Secrets;
using FluxFlow.Engine.Runtime;

namespace FluxMq.App.Scenarios;

public sealed class RuntimeMqttScenarioClientFactory : IMqttScenarioClientFactory
{
    private readonly ApplicationRuntime _runtime;
    private readonly Func<MqttConnectionProfile, IMqttBrokerClient> _clientFactory;

    public RuntimeMqttScenarioClientFactory(
        ApplicationRuntime runtime,
        Func<MqttConnectionProfile, IMqttBrokerClient>? clientFactory = null,
        ISecretResolver? secretResolver = null)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _clientFactory = clientFactory ?? (profile => new MqttBrokerClient(profile, secretResolver));
    }

    public IMqttBrokerClient CreateClient(string connectionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);

        var profile = RuntimeMqttConnectionProfileResolver.Resolve(_runtime, connectionName);
        return _clientFactory(MqttScenarioClientProfiles.Create(profile));
    }
}
