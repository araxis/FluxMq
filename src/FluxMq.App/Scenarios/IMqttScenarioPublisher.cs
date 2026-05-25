using FluxMq.Components.MqttPublisher;

namespace FluxMq.App.Scenarios;

public interface IMqttScenarioPublisher
{
    Task PublishAsync(
        string connectionName,
        MqttPublishRequest request,
        CancellationToken cancellationToken = default);
}
