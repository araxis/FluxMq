using FluxMq.Core.Mqtt;

namespace FluxMq.App.Scenarios;

public interface IMqttScenarioClientFactory
{
    IFluxMqttClient CreateClient(string connectionName);
}
