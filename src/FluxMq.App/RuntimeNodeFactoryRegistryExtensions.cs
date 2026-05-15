using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Core.Session;
using FluxMq.Components.MessageSource;
using FluxMq.Components.MqttMetrics;
using FluxMq.Components.MqttPayloadInspector;
using FluxMq.Components.Storage.Repositories;
using FluxMq.Pipeline.Components;
using FluxMq.Pipeline.Definitions;
using FluxMq.Pipeline.Runtime;
using MQTTnet.Protocol;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.App;

public static class RuntimeNodeFactoryRegistryExtensions
{
    private static readonly PortName InputPort = new("Input");
    private static readonly PortName OutputPort = new("Output");
    private static readonly PortName SnapshotsPort = new("Snapshots");
    private static readonly PortName ErrorsPort = new("Errors");

    public static RuntimeNodeFactoryRegistry RegisterPipelineComponentFactories(
        this RuntimeNodeFactoryRegistry registry,
        Func<MqttConnectionProfile, IMqttSession>? sessionFactory = null,
        IMessageRepository? messageRepository = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        sessionFactory ??= static profile => new MqttSession(profile);

        return registry
            .Register(PipelineFlowNodeTypes.Connection, context => CreateConnection(context.Address, context.Definition, sessionFactory))
            .Register(PipelineFlowNodeTypes.Trigger, context => CreateTrigger(context.Address, context.Definition, context))
            .Register(PipelineFlowNodeTypes.TrafficSource, context => CreateTrafficSource(context.Address, context.Definition, sessionFactory, messageRepository))
            .Register(PipelineFlowNodeTypes.PayloadInspector, CreatePayloadInspector)
            .Register(PipelineFlowNodeTypes.MetricsSink, CreateMetricsSink);
    }

    private static RuntimeNode CreateConnection(
        NodeAddress address,
        NodeDefinition definition,
        Func<MqttConnectionProfile, IMqttSession> sessionFactory)
    {
        var profile = GetConnectionProfile(definition, PipelineFlowNodeTypes.Connection.Value);
        var component = new MqttConnectionComponent(sessionFactory(profile), disposeSessionOnDispose: true);

        return RuntimeNode.Create(
            address,
            component,
            outputs:
            [
                new OutputPort<FlowError>(address.Port(ErrorsPort), component.Errors)
            ]);
    }

    private static RuntimeNode CreateTrafficSource(
        NodeAddress address,
        NodeDefinition definition,
        Func<MqttConnectionProfile, IMqttSession> sessionFactory,
        IMessageRepository? messageRepository)
    {
        var kind = GetStringOrDefault(definition, "kind", "live");
        IFlowNode component;
        ISourceBlock<MqttEnvelope> output;

        switch (kind.Trim().ToLowerInvariant())
        {
            case "live":
            case "mqtt":
            {
                var profile = GetConnectionProfile(definition, PipelineFlowNodeTypes.TrafficSource.Value);
                var live = new LiveMqttSourceComponent(
                    sessionFactory(profile),
                    GetSubscriptions(definition, PipelineFlowNodeTypes.TrafficSource.Value),
                    boundedCapacity: GetBoundedCapacity(definition));
                component = live;
                output = live.Output;
                break;
            }

            case "stored":
            case "stored-session":
            {
                if (messageRepository is null)
                {
                    throw new InvalidOperationException("Stored traffic source requires a message repository.");
                }

                var stored = new StoredSessionSourceComponent(
                    messageRepository,
                    GetRequiredSessionId(definition, "sessionId"),
                    preserveTiming: GetBoolOrDefault(definition, "preserveTiming", false),
                    speed: GetDoubleOrDefault(definition, "speed", 1),
                    boundedCapacity: GetBoundedCapacity(definition));
                component = stored;
                output = stored.Output;
                break;
            }

            case "generated":
            case "test":
            {
                var generated = new GeneratedMqttSourceComponent(
                    GetGeneratedMessages(definition),
                    boundedCapacity: GetBoundedCapacity(definition));
                component = generated;
                output = generated.Output;
                break;
            }

            default:
                throw new InvalidOperationException("Configuration value 'kind' must be live, stored-session, or generated.");
        }

        return RuntimeNode.Create(
            address,
            component,
            outputs:
            [
                new OutputPort<MqttEnvelope>(address.Port(OutputPort), output),
                new OutputPort<FlowError>(address.Port(ErrorsPort), component.Errors)
            ]);
    }

    private static RuntimeNode CreateTrigger(
        NodeAddress address,
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
            address,
            component,
            outputs:
            [
                new OutputPort<MqttEnvelope>(address.Port(OutputPort), component.Output),
                new OutputPort<FlowError>(address.Port(ErrorsPort), component.Errors)
            ]);
    }

    private static RuntimeNode CreatePayloadInspector(NodeAddress address, NodeDefinition definition)
    {
        var component = new PayloadInspectorMapperComponent(boundedCapacity: GetBoundedCapacity(definition));

        return RuntimeNode.Create(
            address,
            component,
            inputs:
            [
                new InputPort<MqttEnvelope>(address.Port(InputPort), component.Input)
            ],
            outputs:
            [
                new OutputPort<InspectedMqttMessage>(address.Port(OutputPort), component.Output),
                new OutputPort<FlowError>(address.Port(ErrorsPort), component.Errors)
            ]);
    }

    private static RuntimeNode CreateMetricsSink(NodeAddress address, NodeDefinition definition)
    {
        var component = new MqttMetricsSinkComponent(boundedCapacity: GetBoundedCapacity(definition));

        return RuntimeNode.Create(
            address,
            component,
            inputs:
            [
                new InputPort<MqttEnvelope>(address.Port(InputPort), component.Input)
            ],
            outputs:
            [
                new OutputPort<MqttMetricsSnapshot>(address.Port(SnapshotsPort), component.Snapshots),
                new OutputPort<FlowError>(address.Port(ErrorsPort), component.Errors)
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

    private static string GetStringOrDefault(NodeDefinition definition, string key, string defaultValue)
    {
        if (!definition.Configuration.TryGetValue(key, out var value))
        {
            return defaultValue;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"Configuration value '{key}' must be a string.");
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

    private static MqttConnectionProfile GetConnectionProfile(NodeDefinition definition, string nodeType)
    {
        if (!definition.Configuration.TryGetValue("profile", out var profileElement))
        {
            throw new InvalidOperationException($"Configuration value 'profile' is required for {nodeType}.");
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

    private static IReadOnlyList<MqttSubscription> GetSubscriptions(NodeDefinition definition, string requiredFor = "mqtt.trigger")
    {
        if (!definition.Configuration.TryGetValue("subscriptions", out var subscriptionsElement))
        {
            throw new InvalidOperationException($"Configuration value 'subscriptions' is required for {requiredFor}.");
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

    private static SessionId GetRequiredSessionId(NodeDefinition definition, string key)
    {
        var value = GetRequiredString(definition, key);
        if (!Guid.TryParse(value, out var guid) || guid == Guid.Empty)
        {
            throw new InvalidOperationException($"Configuration value '{key}' must be a non-empty GUID.");
        }

        return new SessionId(guid);
    }

    private static bool GetBoolOrDefault(NodeDefinition definition, string key, bool defaultValue)
    {
        if (!definition.Configuration.TryGetValue(key, out var value))
        {
            return defaultValue;
        }

        if (value.ValueKind != JsonValueKind.True && value.ValueKind != JsonValueKind.False)
        {
            throw new InvalidOperationException($"Configuration value '{key}' must be a boolean.");
        }

        return value.GetBoolean();
    }

    private static double GetDoubleOrDefault(NodeDefinition definition, string key, double defaultValue)
    {
        if (!definition.Configuration.TryGetValue(key, out var value))
        {
            return defaultValue;
        }

        if (value.ValueKind != JsonValueKind.Number ||
            !value.TryGetDouble(out var result) ||
            result <= 0 ||
            double.IsNaN(result) ||
            double.IsInfinity(result))
        {
            throw new InvalidOperationException($"Configuration value '{key}' must be a positive finite number.");
        }

        return result;
    }

    private static IReadOnlyList<MqttEnvelope> GetGeneratedMessages(NodeDefinition definition)
    {
        if (!definition.Configuration.TryGetValue("messages", out var messagesElement))
        {
            return [];
        }

        if (messagesElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Configuration value 'messages' must be an array.");
        }

        var messages = new List<MqttEnvelope>();
        foreach (var item in messagesElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("Each generated message must be an object.");
            }

            messages.Add(new MqttEnvelope
            {
                Topic = ReadRequiredString(item, "topic"),
                Payload = ReadPayload(item),
                ReceivedAt = ReadDateTimeOffsetOrDefault(item, "receivedAt", DateTimeOffset.UtcNow),
                QualityOfService = ParseQualityOfService(item),
                Retain = ReadBoolOrDefault(item, "retain", false)
            });
        }

        return messages;
    }

    private static byte[] ReadPayload(JsonElement item)
    {
        if (!item.TryGetProperty("payload", out var payload))
        {
            return [];
        }

        return payload.ValueKind switch
        {
            JsonValueKind.String => DecodePayload(payload.GetString() ?? string.Empty, ReadStringOrDefault(item, "payloadEncoding", "utf8")),
            JsonValueKind.Array => payload.EnumerateArray().Select(ReadByte).ToArray(),
            _ => throw new InvalidOperationException("Generated message payload must be a string or byte array.")
        };
    }

    private static byte[] DecodePayload(string value, string encoding)
        => encoding.Trim().ToLowerInvariant() switch
        {
            "utf8" or "text" => Encoding.UTF8.GetBytes(value),
            "base64" => Convert.FromBase64String(value),
            _ => throw new InvalidOperationException("Generated message payloadEncoding must be utf8 or base64.")
        };

    private static byte ReadByte(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetByte(out var result))
        {
            throw new InvalidOperationException("Generated message byte payload values must be between 0 and 255.");
        }

        return result;
    }

    private static DateTimeOffset ReadDateTimeOffsetOrDefault(JsonElement element, string propertyName, DateTimeOffset defaultValue)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return defaultValue;
        }

        if (property.ValueKind != JsonValueKind.String || !DateTimeOffset.TryParse(property.GetString(), out var value))
        {
            throw new InvalidOperationException($"Configuration value '{propertyName}' must be a valid date/time string.");
        }

        return value;
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
