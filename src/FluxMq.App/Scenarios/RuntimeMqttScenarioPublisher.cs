using FluxMq.Components.MessageSource;
using FluxMq.Components.MqttPublisher;
using FluxMq.Pipeline.Runtime;

namespace FluxMq.App.Scenarios;

public sealed class RuntimeMqttScenarioPublisher(ApplicationRuntime runtime) : IMqttScenarioPublisher
{
    private readonly ApplicationRuntime _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));

    public async Task PublishAsync(
        string connectionName,
        MqttPublishRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);
        ArgumentNullException.ThrowIfNull(request);

        var resource = _runtime.Resources.FirstOrDefault(node =>
            string.Equals(node.Address.Node.Value, connectionName, StringComparison.Ordinal));

        if (resource is null)
        {
            throw new InvalidOperationException($"MQTT connection resource '{connectionName}' does not exist.");
        }

        if (resource.Node is not MqttConnectionComponent connection)
        {
            throw new InvalidOperationException($"Resource '{connectionName}' is not an MQTT connection.");
        }

        await connection.Session.PublishAsync(
            request.Topic,
            request.Payload,
            request.QualityOfService,
            request.Retain,
            cancellationToken).ConfigureAwait(false);
    }
}
