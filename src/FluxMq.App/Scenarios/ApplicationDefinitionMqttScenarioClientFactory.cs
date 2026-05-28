using FluxMq.Core.Models;
using FluxMq.Core.Mqtt;
using FluxMq.Pipeline.Definitions;

namespace FluxMq.App.Scenarios;

public sealed class ApplicationDefinitionMqttScenarioClientFactory : IMqttScenarioClientFactory
{
    private readonly ApplicationDefinition _definition;
    private readonly Func<MqttConnectionProfile, IFluxMqttClient> _clientFactory;

    public ApplicationDefinitionMqttScenarioClientFactory(
        ApplicationDefinition definition,
        Func<MqttConnectionProfile, IFluxMqttClient>? clientFactory = null)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _clientFactory = clientFactory ?? (static profile => new FluxMqttClient(profile));
    }

    public IFluxMqttClient CreateClient(string connectionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);

        var profile = ApplicationDefinitionMqttConnectionProfileResolver.Resolve(_definition, connectionName);
        return _clientFactory(MqttScenarioClientProfiles.Create(profile));
    }
}
