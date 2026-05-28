using FluxMq.Components.MessageSource;
using FluxMq.Core.Models;
using FluxMq.Pipeline.Runtime;

namespace FluxMq.App.Scenarios;

internal static class RuntimeMqttConnectionProfileResolver
{
    public static MqttConnectionProfile Resolve(ApplicationRuntime runtime, string connectionName)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);

        var resource = runtime.Resources.FirstOrDefault(node =>
            string.Equals(node.Address.Node.Value, connectionName, StringComparison.Ordinal));

        if (resource is null)
        {
            throw new InvalidOperationException($"MQTT connection resource '{connectionName}' does not exist.");
        }

        if (resource.Node is not MqttConnectionComponent connection)
        {
            throw new InvalidOperationException($"Resource '{connectionName}' is not an MQTT connection.");
        }

        return connection.Client.Profile;
    }
}
