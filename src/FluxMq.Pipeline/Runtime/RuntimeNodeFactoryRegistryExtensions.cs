using FluxMq.Core.Models;
using FluxMq.Core.Session;
using FluxMq.Pipeline.Components;
using FluxMq.Pipeline.Components.MessageSource;
using FluxMq.Pipeline.Components.MqttMetrics;
using FluxMq.Pipeline.Definitions;
using MQTTnet.Protocol;
using System.Text.Json;
using FluxMq.Pipeline.Components.MqttPayloadInspector;

namespace FluxMq.Pipeline.Runtime;

public static class RuntimeNodeFactoryRegistryExtensions
{
    private static readonly PortName InputPort = new("Input");
    private static readonly PortName OutputPort = new("Output");
    private static readonly PortName SnapshotsPort = new("Snapshots");
    private static readonly PortName ErrorsPort = new("Errors");

    public static RuntimeNodeFactoryRegistry RegisterPipelineComponentFactories(
        this RuntimeNodeFactoryRegistry registry,
        Func<MqttConnectionProfile, IMqttSession>? sessionFactory = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        sessionFactory ??= static profile => new MqttSession(profile);

        return registry
            .Register(PipelineFlowNodeTypes.Connection, context => CreateConnection(context.Name, context.Definition, sessionFactory))
            .Register(PipelineFlowNodeTypes.Trigger, context => CreateTrigger(context.Name, context.Definition, context))
            .Register(PipelineFlowNodeTypes.PayloadInspector, CreatePayloadInspector)
            .Register(PipelineFlowNodeTypes.MetricsSink, CreateMetricsSink);
    }

    private static RuntimeNode CreateConnection(
        NodeName name,
        NodeDefinition definition,
        Func<MqttConnectionProfile, IMqttSession> sessionFactory)
    {
        var profile = GetConnectionProfile(definition);
        var component = new MqttConnectionComponent(sessionFactory(profile), disposeSessionOnDispose: true);

        return RuntimeNode.Create(
            name,
            component,
            outputs:
            [
                new OutputPort<FlowError>(ErrorsPort, component.Errors)
            ]);
    }

    private static RuntimeNode CreateTrigger(
        NodeName name,
        NodeDefinition definition,
        RuntimeNodeFactoryContext context)
    {
        var connectionRef = GetRequiredString(definition, "connection");
        var resource = context.GetResource(new NodeName(connectionRef));
        if (resource.Node is not MqttConnectionComponent connection)
        {
            throw new InvalidOperationException(
                $"Resource '{connectionRef}' must be of type '{PipelineFlowNodeTypes.Connection.Value}' to be used by an mqtt.trigger.");
        }

        var subscriptions = GetSubscriptions(definition);
        var component = new MqttTriggerComponent(
            connection,
            subscriptions,
            boundedCapacity: GetBoundedCapacity(definition));

        return RuntimeNode.Create(
            name,
            component,
            outputs:
            [
                new OutputPort<MqttEnvelope>(OutputPort, component.Output),
                new OutputPort<FlowError>(ErrorsPort, component.Errors)
            ]);
    }

    private static RuntimeNode CreatePayloadInspector(NodeName name, NodeDefinition definition)
    {
        var component = new PayloadInspectorMapperComponent(boundedCapacity: GetBoundedCapacity(definition));

        return RuntimeNode.Create(
            name,
            component,
            inputs:
            [
                new InputPort<MqttEnvelope>(InputPort, component.Input)
            ],
            outputs:
            [
                new OutputPort<InspectedMqttMessage>(OutputPort, component.Output),
                new OutputPort<FlowError>(ErrorsPort, component.Errors)
            ]);
    }

    private static RuntimeNode CreateMetricsSink(NodeName name, NodeDefinition definition)
    {
        var component = new MqttMetricsSinkComponent(boundedCapacity: GetBoundedCapacity(definition));

        return RuntimeNode.Create(
            name,
            component,
            inputs:
            [
                new InputPort<MqttEnvelope>(InputPort, component.Input)
            ],
            outputs:
            [
                new OutputPort<MqttMetricsSnapshot>(SnapshotsPort, component.Snapshots),
                new OutputPort<FlowError>(ErrorsPort, component.Errors)
            ]);
    }

    private static string GetRequiredString(NodeDefinition definition, string key)
    {
        if (!definition.Configuration.TryGetValue(key, out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"Configuration value '{key}' is required and must be a string.");
        }

        var s = value.GetString();
        if (string.IsNullOrWhiteSpace(s))
        {
            throw new InvalidOperationException($"Configuration value '{key}' must not be empty.");
        }

        return s;
    }

    private static int GetBoundedCapacity(NodeDefinition definition)
    {
        const int defaultBoundedCapacity = 1000;

        if (!definition.Configuration.TryGetValue("boundedCapacity", out var value))
        {
            return defaultBoundedCapacity;
        }

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var boundedCapacity) || boundedCapacity <= 0)
        {
            throw new InvalidOperationException("Configuration value 'boundedCapacity' must be a positive integer.");
        }

        return boundedCapacity;
    }

    private static MqttConnectionProfile GetConnectionProfile(NodeDefinition definition)
    {
        if (!definition.Configuration.TryGetValue("profile", out var profileElement))
        {
            throw new InvalidOperationException("Configuration value 'profile' is required for mqtt.connection.");
        }

        if (profileElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Configuration value 'profile' must be an object.");
        }

        var defaults = new MqttConnectionProfile();

        return new MqttConnectionProfile
        {
            Name = ReadRequiredString(profileElement, "name"),
            Host = ReadStringOrDefault(profileElement, "host", defaults.Host),
            Port = ReadIntOrDefault(profileElement, "port", defaults.Port, minValue: 1),
            ClientId = ReadStringOrDefault(profileElement, "clientId", defaults.ClientId),
            UseTls = ReadBoolOrDefault(profileElement, "useTls", defaults.UseTls),
            Username = ReadNullableString(profileElement, "username"),
            Password = ReadNullableString(profileElement, "password"),
            KeepAlive = TimeSpan.FromSeconds(ReadIntOrDefault(profileElement, "keepAliveSeconds", (int)defaults.KeepAlive.TotalSeconds, minValue: 1)),
            CleanStart = ReadBoolOrDefault(profileElement, "cleanStart", defaults.CleanStart)
        };
    }

    private static IReadOnlyList<MqttSubscription> GetSubscriptions(NodeDefinition definition)
    {
        if (!definition.Configuration.TryGetValue("subscriptions", out var subscriptionsElement))
        {
            throw new InvalidOperationException("Configuration value 'subscriptions' is required for mqtt.trigger.");
        }

        return subscriptionsElement.ValueKind switch
        {
            JsonValueKind.String => [new MqttSubscription(ReadTopicFilter(subscriptionsElement.GetString()), MqttQualityOfServiceLevel.AtMostOnce)],
            JsonValueKind.Array => ParseSubscriptionArray(subscriptionsElement),
            _ => throw new InvalidOperationException("Configuration value 'subscriptions' must be a string or an array.")
        };
    }

    private static IReadOnlyList<MqttSubscription> ParseSubscriptionArray(JsonElement subscriptionsElement)
    {
        var subscriptions = new List<MqttSubscription>();

        foreach (var element in subscriptionsElement.EnumerateArray())
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    subscriptions.Add(new MqttSubscription(ReadTopicFilter(element.GetString()), MqttQualityOfServiceLevel.AtMostOnce));
                    break;
                case JsonValueKind.Object:
                {
                    var topicFilter = ReadRequiredString(element, "topicFilter");
                    var qualityOfService = ParseQualityOfService(element);
                    subscriptions.Add(new MqttSubscription(topicFilter, qualityOfService));
                    break;
                }
                default:
                    throw new InvalidOperationException("Each subscription must be a string or an object.");
            }
        }

        if (subscriptions.Count == 0)
        {
            throw new InvalidOperationException("Configuration value 'subscriptions' must contain at least one subscription.");
        }

        return subscriptions;
    }

    private static MqttQualityOfServiceLevel ParseQualityOfService(JsonElement subscriptionElement)
    {
        if (!subscriptionElement.TryGetProperty("qos", out var qosElement))
        {
            return MqttQualityOfServiceLevel.AtMostOnce;
        }

        if (qosElement.ValueKind == JsonValueKind.Number && qosElement.TryGetInt32(out var qosValue))
        {
            return qosValue switch
            {
                0 => MqttQualityOfServiceLevel.AtMostOnce,
                1 => MqttQualityOfServiceLevel.AtLeastOnce,
                2 => MqttQualityOfServiceLevel.ExactlyOnce,
                _ => throw new InvalidOperationException("Configuration value 'qos' must be 0, 1, or 2.")
            };
        }

        if (qosElement.ValueKind == JsonValueKind.String)
        {
            var value = qosElement.GetString();
            if (string.Equals(value, "AtMostOnce", StringComparison.OrdinalIgnoreCase))
            {
                return MqttQualityOfServiceLevel.AtMostOnce;
            }

            if (string.Equals(value, "AtLeastOnce", StringComparison.OrdinalIgnoreCase))
            {
                return MqttQualityOfServiceLevel.AtLeastOnce;
            }

            if (string.Equals(value, "ExactlyOnce", StringComparison.OrdinalIgnoreCase))
            {
                return MqttQualityOfServiceLevel.ExactlyOnce;
            }
        }

        throw new InvalidOperationException("Configuration value 'qos' must be 0, 1, 2, AtMostOnce, AtLeastOnce, or ExactlyOnce.");
    }

    private static string ReadRequiredString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"Configuration value '{propertyName}' is required and must be a string.");
        }

        return ReadTopicFilter(property.GetString(), propertyName);
    }

    private static string ReadStringOrDefault(JsonElement element, string propertyName, string defaultValue)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return defaultValue;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"Configuration value '{propertyName}' must be a string.");
        }

        var value = property.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Configuration value '{propertyName}' must not be empty.");
        }

        return value;
    }

    private static string? ReadNullableString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"Configuration value '{propertyName}' must be a string or null.");
        }

        var value = property.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static int ReadIntOrDefault(JsonElement element, string propertyName, int defaultValue, int minValue)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return defaultValue;
        }

        if (property.ValueKind != JsonValueKind.Number || !property.TryGetInt32(out var value) || value < minValue)
        {
            throw new InvalidOperationException($"Configuration value '{propertyName}' must be an integer greater than or equal to {minValue}.");
        }

        return value;
    }

    private static bool ReadBoolOrDefault(JsonElement element, string propertyName, bool defaultValue)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return defaultValue;
        }

        if (property.ValueKind != JsonValueKind.True && property.ValueKind != JsonValueKind.False)
        {
            throw new InvalidOperationException($"Configuration value '{propertyName}' must be a boolean.");
        }

        return property.GetBoolean();
    }

    private static string ReadTopicFilter(string? value, string propertyName = "topicFilter")
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Configuration value '{propertyName}' must not be empty.");
        }

        return value;
    }
}
