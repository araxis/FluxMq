using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Core.Session;
using FluxMq.Components.FileWriter;
using FluxMq.Components.MessageFilter;
using FluxMq.Components.MessageSource;
using FluxMq.Components.MqttMetrics;
using FluxMq.Components.MqttPayloadInspector;
using FluxMq.Components.MqttPublisher;
using FluxMq.Components.Replay;
using FluxMq.Components.Storage.Repositories;
using FluxMq.Pipeline.Components;
using FluxMq.Pipeline.Definitions;
using FluxMq.Pipeline.Mapping;
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
        IMessageRepository? messageRepository = null,
        IFlowExpressionEngine? expressionEngine = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        sessionFactory ??= static profile => new MqttSession(profile);
        expressionEngine ??= new DynamicExpressoFlowExpressionEngine();

        return registry
            .Register(PipelineFlowNodeTypes.Connection, context => CreateConnection(context.Address, context.Definition, sessionFactory))
            .Register(PipelineFlowNodeTypes.Trigger, context => CreateTrigger(context.Address, context.Definition, context))
            .Register(PipelineFlowNodeTypes.LiveSource, context => CreateLiveMqttSource(context.Address, context.Definition, sessionFactory))
            .Register(PipelineFlowNodeTypes.StoredSessionSource, context => CreateStoredSessionSource(context.Address, context.Definition, messageRepository))
            .Register(PipelineFlowNodeTypes.GeneratedSource, context => CreateGeneratedMqttSource(context.Address, context.Definition))
            .Register(PipelineFlowNodeTypes.PayloadInspector, CreatePayloadInspector)
            .Register(PipelineFlowNodeTypes.MqttMetrics, CreateMqttMetrics)
            .Register(PipelineFlowNodeTypes.MessageFilter, context => CreateMessageFilter(context.Address, context.Definition, expressionEngine))
            .Register(PipelineFlowNodeTypes.PublishRequestMapper, context => CreatePublishRequestMapper(context.Address, context.Definition, expressionEngine))
            .Register(PipelineFlowNodeTypes.MqttPublisher, context => CreatePublisher(context.Address, context.Definition, context))
            .Register(PipelineFlowNodeTypes.RecordingRequestMapper, CreateRecordingRequestMapper)
            .Register(PipelineFlowNodeTypes.MqttRecorder, context => CreateRecorder(context.Address, messageRepository))
            .Register(PipelineFlowNodeTypes.FileWriteRequestMapper, context => CreateFileWriteRequestMapper(context.Address, context.Definition, expressionEngine))
            .Register(PipelineFlowNodeTypes.FileWriter, CreateFileWriter);
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

    private static RuntimeNode CreateLiveMqttSource(
        NodeAddress address,
        NodeDefinition definition,
        Func<MqttConnectionProfile, IMqttSession> sessionFactory)
    {
        var profile = GetConnectionProfile(definition, PipelineFlowNodeTypes.LiveSource.Value);
        var component = new LiveMqttSourceComponent(
            sessionFactory(profile),
            GetSubscriptions(definition, PipelineFlowNodeTypes.LiveSource.Value),
            boundedCapacity: GetBoundedCapacity(definition));

        return SourceRuntimeNode(address, component, component.Output);
    }

    private static RuntimeNode CreateStoredSessionSource(
        NodeAddress address,
        NodeDefinition definition,
        IMessageRepository? messageRepository)
    {
        if (messageRepository is null)
        {
            throw new InvalidOperationException("Stored session source requires a message repository.");
        }

        var component = new StoredSessionSourceComponent(
            messageRepository,
            GetRequiredSessionId(definition, "sessionId"),
            preserveTiming: GetBoolOrDefault(definition, "preserveTiming", false),
            speed: GetDoubleOrDefault(definition, "speed", 1),
            boundedCapacity: GetBoundedCapacity(definition));

        return SourceRuntimeNode(address, component, component.Output);
    }

    private static RuntimeNode CreateGeneratedMqttSource(NodeAddress address, NodeDefinition definition)
    {
        var component = new GeneratedMqttSourceComponent(
            GetGeneratedMessages(definition),
            boundedCapacity: GetBoundedCapacity(definition));

        return SourceRuntimeNode(address, component, component.Output);
    }

    private static RuntimeNode SourceRuntimeNode(
        NodeAddress address,
        IFlowNode component,
        ISourceBlock<MqttEnvelope> output)
    {
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

    private static RuntimeNode CreateMessageFilter(
        NodeAddress address,
        NodeDefinition definition,
        IFlowExpressionEngine expressionEngine)
    {
        var patterns = GetFilterPatterns(definition);
        var expression = GetNullableString(definition, "expression");
        IFlowPredicate<MqttEnvelope> expressionPredicate = string.IsNullOrWhiteSpace(expression)
            ? new DelegateFlowPredicate<MqttEnvelope>(_ => true)
            : new MqttEnvelopeExpressionPredicate(expressionEngine, expression);

        Func<MqttEnvelope, bool> predicate = patterns.Count > 0
            ? envelope => MqttTopicFilterMatcher.MatchesAny(patterns, envelope.Topic) && expressionPredicate.IsMatch(envelope)
            : expressionPredicate.IsMatch;

        var component = new MessageFilterComponent(predicate, boundedCapacity: GetBoundedCapacity(definition));

        return RuntimeNode.Create(
            address,
            component,
            inputs:
            [
                new InputPort<MqttEnvelope>(address.Port(InputPort), component.Input)
            ],
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

    private static RuntimeNode CreateMqttMetrics(NodeAddress address, NodeDefinition definition)
    {
        var component = new MqttMetricsComponent(boundedCapacity: GetBoundedCapacity(definition));

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

    private static RuntimeNode CreatePublishRequestMapper(
        NodeAddress address,
        NodeDefinition definition,
        IFlowExpressionEngine expressionEngine)
    {
        EnsureMapperEngineSupported(definition, expressionEngine);

        var component = new MqttPublishRequestMapperComponent(
            new MqttPublishRequestExpressionMapper(
                expressionEngine,
                GetPublishRequestMapDefinition(definition)),
            boundedCapacity: GetBoundedCapacity(definition));

        return RuntimeNode.Create(
            address,
            component,
            inputs:
            [
                new InputPort<MqttEnvelope>(address.Port(InputPort), component.Input)
            ],
            outputs:
            [
                new OutputPort<MqttPublishRequest>(address.Port(OutputPort), component.Output),
                new OutputPort<FlowError>(address.Port(ErrorsPort), component.Errors)
            ]);
    }

    private static RuntimeNode CreateRecordingRequestMapper(NodeAddress address, NodeDefinition definition)
    {
        var component = new MqttRecordingRequestMapperComponent(
            GetRequiredSessionId(definition, "sessionId"),
            boundedCapacity: GetBoundedCapacity(definition));

        return RuntimeNode.Create(
            address,
            component,
            inputs:
            [
                new InputPort<MqttEnvelope>(address.Port(InputPort), component.Input)
            ],
            outputs:
            [
                new OutputPort<MqttRecordingRequest>(address.Port(OutputPort), component.Output),
                new OutputPort<FlowError>(address.Port(ErrorsPort), component.Errors)
            ]);
    }

    private static RuntimeNode CreatePublisher(
        NodeAddress address,
        NodeDefinition definition,
        RuntimeNodeFactoryContext context)
    {
        var connectionRef = GetRequiredString(definition, "connection");
        var resource = context.GetResource(new NodeName(connectionRef));
        if (resource.Node is not MqttConnectionComponent connection)
        {
            throw new InvalidOperationException(
                $"Resource '{connectionRef}' must be of type '{PipelineFlowNodeTypes.Connection.Value}' to be used by an mqtt.publisher.");
        }

        var component = new MqttPublisherComponent(connection.Session, boundedCapacity: GetBoundedCapacity(definition));

        return RuntimeNode.Create(
            address,
            component,
            inputs:
            [
                new InputPort<MqttPublishRequest>(address.Port(InputPort), component.Input)
            ],
            outputs:
            [
                new OutputPort<FlowError>(address.Port(ErrorsPort), component.Errors)
            ]);
    }

    private static RuntimeNode CreateRecorder(NodeAddress address, IMessageRepository? messageRepository)
    {
        if (messageRepository is null)
        {
            throw new InvalidOperationException("MQTT recorder requires a message repository.");
        }

        var component = new MqttRecorderComponent(messageRepository);

        return RuntimeNode.Create(
            address,
            component,
            inputs:
            [
                new InputPort<MqttRecordingRequest>(address.Port(InputPort), component.Input)
            ],
            outputs:
            [
                new OutputPort<FlowError>(address.Port(ErrorsPort), component.Errors)
            ]);
    }

    private static RuntimeNode CreateFileWriteRequestMapper(
        NodeAddress address,
        NodeDefinition definition,
        IFlowExpressionEngine expressionEngine)
    {
        EnsureMapperEngineSupported(definition, expressionEngine);

        var component = new FileWriteRequestMapperComponent(
            new FileWriteRequestExpressionMapper(
                expressionEngine,
                GetFileWriteRequestMapDefinition(definition)),
            boundedCapacity: GetBoundedCapacity(definition));

        return RuntimeNode.Create(
            address,
            component,
            inputs:
            [
                new InputPort<MqttEnvelope>(address.Port(InputPort), component.Input)
            ],
            outputs:
            [
                new OutputPort<FileWriteRequest>(address.Port(OutputPort), component.Output),
                new OutputPort<FlowError>(address.Port(ErrorsPort), component.Errors)
            ]);
    }

    private static RuntimeNode CreateFileWriter(NodeAddress address, NodeDefinition definition)
    {
        var component = new FileWriterComponent(boundedCapacity: GetBoundedCapacity(definition));

        return RuntimeNode.Create(
            address,
            component,
            inputs:
            [
                new InputPort<FileWriteRequest>(address.Port(InputPort), component.Input)
            ],
            outputs:
            [
                new OutputPort<FlowError>(address.Port(ErrorsPort), component.Errors)
            ]);
    }

    private static MqttPublishRequestMapDefinition GetPublishRequestMapDefinition(NodeDefinition definition)
    {
        if (definition.Configuration.TryGetValue("map", out var mapElement))
        {
            if (mapElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("Configuration value 'map' must be an object.");
            }

            return new MqttPublishRequestMapDefinition
            {
                TopicExpression = ReadOptionalString(mapElement, "topic") ?? ReadOptionalString(mapElement, "topicExpression"),
                PayloadExpression = ReadOptionalString(mapElement, "payload") ?? ReadOptionalString(mapElement, "payloadExpression"),
                QualityOfServiceExpression =
                    ReadOptionalString(mapElement, "qos") ??
                    ReadOptionalString(mapElement, "qualityOfService") ??
                    ReadOptionalString(mapElement, "qosExpression") ??
                    ReadOptionalString(mapElement, "qualityOfServiceExpression"),
                RetainExpression = ReadOptionalString(mapElement, "retain") ?? ReadOptionalString(mapElement, "retainExpression")
            };
        }

        var fixedTopic = GetNullableString(definition, "topic");
        return new MqttPublishRequestMapDefinition
        {
            TopicExpression =
                GetNullableString(definition, "topicExpression") ??
                (fixedTopic is null ? null : JsonSerializer.Serialize(fixedTopic)),
            PayloadExpression = GetNullableString(definition, "payloadExpression"),
            QualityOfServiceExpression =
                GetNullableString(definition, "qosExpression") ??
                GetNullableString(definition, "qualityOfServiceExpression"),
            RetainExpression = GetNullableString(definition, "retainExpression")
        };
    }

    private static FileWriteRequestMapDefinition GetFileWriteRequestMapDefinition(NodeDefinition definition)
    {
        if (definition.Configuration.TryGetValue("map", out var mapElement))
        {
            if (mapElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("Configuration value 'map' must be an object.");
            }

            return new FileWriteRequestMapDefinition
            {
                PathExpression =
                    ReadOptionalString(mapElement, "path") ??
                    ReadOptionalString(mapElement, "pathExpression") ??
                    throw new InvalidOperationException("File write request map requires a 'path' expression."),
                ContentExpression = ReadOptionalString(mapElement, "content") ?? ReadOptionalString(mapElement, "contentExpression"),
                ModeExpression = ReadOptionalString(mapElement, "mode") ?? ReadOptionalString(mapElement, "modeExpression"),
                CreateDirectoryExpression =
                    ReadOptionalString(mapElement, "createDirectory") ??
                    ReadOptionalString(mapElement, "createDirectoryExpression")
            };
        }

        return new FileWriteRequestMapDefinition
        {
            PathExpression =
                GetNullableString(definition, "pathExpression") ??
                throw new InvalidOperationException("File write request mapper requires configuration value 'pathExpression' or 'map.path'."),
            ContentExpression = GetNullableString(definition, "contentExpression"),
            ModeExpression = GetNullableString(definition, "modeExpression"),
            CreateDirectoryExpression = GetNullableString(definition, "createDirectoryExpression")
        };
    }

    private static void EnsureMapperEngineSupported(NodeDefinition definition, IFlowExpressionEngine expressionEngine)
    {
        var requestedEngine = GetNullableString(definition, "engine") ?? GetNullableString(definition, "mapper");
        if (requestedEngine is null)
        {
            return;
        }

        if (!string.Equals(requestedEngine, expressionEngine.Name, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Mapper engine '{requestedEngine}' is not registered. Current engine is '{expressionEngine.Name}'.");
        }
    }

    private static string? ReadOptionalString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"Configuration value '{propertyName}' must be a string.");
        }

        var value = property.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static IReadOnlyList<string> GetFilterPatterns(NodeDefinition definition)
    {
        if (!definition.Configuration.TryGetValue("patterns", out var element))
            return [];

        if (element.ValueKind == JsonValueKind.String)
        {
            var s = element.GetString();
            return string.IsNullOrWhiteSpace(s) ? [] : [s];
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            var list = new List<string>();
            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String && item.GetString() is { Length: > 0 } pattern)
                    list.Add(pattern);
            }
            return list;
        }

        return [];
    }

    private static string? GetNullableString(NodeDefinition definition, string key)
    {
        if (!definition.Configuration.TryGetValue(key, out var value) || value.ValueKind != JsonValueKind.String)
            return null;
        var s = value.GetString();
        return string.IsNullOrWhiteSpace(s) ? null : s;
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
