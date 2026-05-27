using FluxMq.Core.Models;
using FluxMq.Core.Session;
using FluxMq.Pipeline.Definitions;

namespace FluxMq.App.Scenarios;

public sealed class ApplicationDefinitionMqttScenarioClientFactory : IMqttScenarioClientFactory
{
    private readonly ApplicationDefinition _definition;
    private readonly Func<MqttConnectionProfile, IMqttSession> _clientFactory;

    public ApplicationDefinitionMqttScenarioClientFactory(
        ApplicationDefinition definition,
        Func<MqttConnectionProfile, IMqttSession>? clientFactory = null)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _clientFactory = clientFactory ?? (static profile => new MqttSession(profile));
    }

    public IMqttSession CreateClient(string connectionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);

        var profile = ApplicationDefinitionMqttConnectionProfileResolver.Resolve(_definition, connectionName);
        return _clientFactory(MqttScenarioClientProfiles.Create(profile));
    }
}
