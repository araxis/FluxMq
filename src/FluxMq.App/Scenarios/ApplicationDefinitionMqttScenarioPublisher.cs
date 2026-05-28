using FluxMq.Components.MqttPublisher;
using FluxMq.Core.Models;
using FluxMq.Core.Mqtt;
using FluxMq.Pipeline.Definitions;

namespace FluxMq.App.Scenarios;

public sealed class ApplicationDefinitionMqttScenarioPublisher : IMqttScenarioPublisher
{
    private readonly IMqttScenarioClientFactory _clientFactory;

    public ApplicationDefinitionMqttScenarioPublisher(
        ApplicationDefinition definition,
        Func<MqttConnectionProfile, IFluxMqttClient>? clientFactory = null)
        : this(new ApplicationDefinitionMqttScenarioClientFactory(definition, clientFactory))
    {
    }

    public ApplicationDefinitionMqttScenarioPublisher(IMqttScenarioClientFactory clientFactory)
        => _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));

    public async Task PublishAsync(
        string connectionName,
        MqttPublishRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);
        ArgumentNullException.ThrowIfNull(request);

        await using var client = _clientFactory.CreateClient(connectionName);
        if (client.State is not MqttClientState.Connected)
        {
            await client.ConnectAsync(cancellationToken).ConfigureAwait(false);
        }

        await client.PublishAsync(
            request.Topic,
            request.Payload,
            request.QualityOfService,
            request.Retain,
            cancellationToken).ConfigureAwait(false);
    }
}
