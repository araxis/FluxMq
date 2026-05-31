using FluxMq.Core.Models;
using FluxMq.Core.Mqtt;
using FluxMq.App.Definitions;

namespace FluxMq.App.Scenarios;

public sealed class ApplicationDefinitionMqttScenarioClientFactory : IMqttScenarioClientFactory
{
    private readonly FluxMqApplicationDefinition _definition;
    private readonly Func<MqttConnectionProfile, IMqttBrokerClient> _clientFactory;

    public ApplicationDefinitionMqttScenarioClientFactory(
        FluxMqApplicationDefinition definition,
        Func<MqttConnectionProfile, IMqttBrokerClient>? clientFactory = null)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _clientFactory = clientFactory ?? (static profile => new MqttBrokerClient(profile));
    }

    public IMqttBrokerClient CreateClient(string connectionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);

        var profile = ApplicationDefinitionMqttConnectionProfileResolver.Resolve(_definition, connectionName);
        return _clientFactory(MqttScenarioClientProfiles.Create(profile));
    }
}
