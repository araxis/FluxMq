using FluxMq.Core.Models;
using FluxMq.Core.Mqtt;
using FluxMq.App.Definitions;
using FluxFlow.Components.Secrets;

namespace FluxMq.App.Scenarios;

public sealed class ApplicationDefinitionMqttScenarioClientFactory : IMqttScenarioClientFactory
{
    private readonly FluxMqApplicationDefinition _definition;
    private readonly Func<MqttConnectionProfile, IMqttBrokerClient> _clientFactory;

    public ApplicationDefinitionMqttScenarioClientFactory(
        FluxMqApplicationDefinition definition,
        Func<MqttConnectionProfile, IMqttBrokerClient>? clientFactory = null,
        ISecretResolver? secretResolver = null)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _clientFactory = clientFactory ?? (profile => new MqttBrokerClient(profile, secretResolver));
    }

    public IMqttBrokerClient CreateClient(string connectionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);

        var profile = ApplicationDefinitionMqttConnectionProfileResolver.Resolve(_definition, connectionName);
        return _clientFactory(MqttScenarioClientProfiles.Create(profile));
    }
}
