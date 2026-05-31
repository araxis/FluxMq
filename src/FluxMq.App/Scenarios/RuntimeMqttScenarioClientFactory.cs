using FluxMq.Core.Models;
using FluxMq.Core.Mqtt;
using FluxFlow.Engine.Runtime;

namespace FluxMq.App.Scenarios;

public sealed class RuntimeMqttScenarioClientFactory : IMqttScenarioClientFactory
{
    private readonly ApplicationRuntime _runtime;
    private readonly Func<MqttConnectionProfile, IMqttBrokerClient> _clientFactory;

    public RuntimeMqttScenarioClientFactory(
        ApplicationRuntime runtime,
        Func<MqttConnectionProfile, IMqttBrokerClient>? clientFactory = null)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _clientFactory = clientFactory ?? (static profile => new MqttBrokerClient(profile));
    }

    public IMqttBrokerClient CreateClient(string connectionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);

        var profile = RuntimeMqttConnectionProfileResolver.Resolve(_runtime, connectionName);
        return _clientFactory(MqttScenarioClientProfiles.Create(profile));
    }
}
