using FluxMq.Core.Mqtt;

namespace FluxMq.App.Scenarios;

public interface IMqttScenarioClientFactory
{
    IMqttBrokerClient CreateClient(string connectionName);
}
