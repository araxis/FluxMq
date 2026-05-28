using FluxMq.Core.Models;
using FluxMq.Core.Mqtt;
using FluxMq.Pipeline.Runtime;

namespace FluxMq.App.Scenarios;

public sealed class RuntimeMqttScenarioClientFactory : IMqttScenarioClientFactory
{
    private readonly ApplicationRuntime _runtime;
    private readonly Func<MqttConnectionProfile, IFluxMqttClient> _clientFactory;

    public RuntimeMqttScenarioClientFactory(
        ApplicationRuntime runtime,
        Func<MqttConnectionProfile, IFluxMqttClient>? clientFactory = null)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _clientFactory = clientFactory ?? (static profile => new FluxMqttClient(profile));
    }

    public IFluxMqttClient CreateClient(string connectionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);

        var profile = RuntimeMqttConnectionProfileResolver.Resolve(_runtime, connectionName);
        return _clientFactory(MqttScenarioClientProfiles.Create(profile));
    }
}
