using FluxMq.Core.Session;

namespace FluxMq.App.Scenarios;

public interface IMqttScenarioClientFactory
{
    IMqttSession CreateClient(string connectionName);
}
