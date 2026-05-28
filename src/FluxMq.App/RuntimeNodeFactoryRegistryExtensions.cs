using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Core.Mqtt;
using FluxMq.Components.Assertions;
using FluxMq.Components.FileWriter;
using FluxMq.Components.JsonSchema;
using FluxMq.Components.Logging;
using FluxMq.Components.MessageFilter;
using FluxMq.Components.MessageSource;
using FluxMq.Components.MqttConditionRouter;
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
    private const MqttQualityOfServiceLevel DefaultSubscriptionQos = MqttQualityOfServiceLevel.AtLeastOnce;
    private static readonly PortName InputPort = new("Input");
    private static readonly PortName OutputPort = new("Output");
    private static readonly PortName ResultPort = new("Result");
    private static readonly PortName PassedPort = new("Passed");
    private static readonly PortName FailedPort = new("Failed");
    private static readonly PortName WhenTruePort = new("WhenTrue");
    private static readonly PortName WhenFalsePort = new("WhenFalse");
    private static readonly PortName ValidPort = new("Valid");
    private static readonly PortName InvalidPort = new("Invalid");
    private static readonly PortName SnapshotsPort = new("Snapshots");
    private static readonly PortName EntriesPort = new("Entries");
    private static readonly PortName FlowErrorsPort = new("FlowErrors");
    private static readonly PortName ErrorsPort = new("Errors");

    public static RuntimeNodeFactoryRegistry RegisterPipelineComponentFactories(
        this RuntimeNodeFactoryRegistry registry,
        Func<MqttConnectionProfile, IFluxMqttClient>? clientFactory = null,
        IMessageRepository? messageRepository = null,
        IFlowExpressionEngine? expressionEngine = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        clientFactory ??= static profile => new FluxMqttClient(profile);
        expressionEngine ??= new DynamicExpressoFlowExpressionEngine();

        return registry
            .Register(PipelineFlowNodeTypes.Connection, context => CreateConnection(context.Address, context.Definition, clientFactory))
            .Register(PipelineFlowNodeTypes.Trigger, context => CreateTrigger(context.Address, context.Definition, context))
            .Register(PipelineFlowNodeTypes.StoredSessionSource, context => CreateStoredSessionSource(context.Address, context.Definition, messageRepository))
            .Register(PipelineFlowNodeTypes.ReplaySource, context => CreateReplaySource(context.Address, context.Definition, messageRepository))
            .Register(PipelineFlowNodeTypes.GeneratedSource, context => CreateGeneratedMqttSource(context.Address, context.Definition))
            .Register(PipelineFlowNodeTypes.PayloadInspector, CreatePayloadInspector)
            .Register(PipelineFlowNodeTypes.MqttMetrics, CreateMqttMetrics)
            .Register(PipelineFlowNodeTypes.FlowLogger, CreateFlowLogger)
            .Register(PipelineFlowNodeTypes.MessageFilter, context => CreateMessageFilter(context.Address, context.Definition, expressionEngine))
            .Register(PipelineFlowNodeTypes.ConditionRouter, context => CreateConditionRouter(context.Address, context.Definition, expressionEngine))
            .Register(PipelineFlowNodeTypes.FlowAssertion, context => CreateFlowAssertion(context.Address, context.Definition, expressionEngine))
            .Register(PipelineFlowNodeTypes.JsonSchemaValidator, context => CreateJsonSchemaValidator(context.Address, context.Definition))
            .Register(PipelineFlowNodeTypes.DynamicMapper, context => CreateDynamicMapper(context.Address, context.Definition, expressionEngine))
            .Register(PipelineFlowNodeTypes.MqttPublisher, context => CreatePublisher(context.Address, context.Definition, context))
            .Register(PipelineFlowNodeTypes.MqttRecorder, context => CreateRecorder(context.Address, context.Definition, messageRepository))
            .Register(PipelineFlowNodeTypes.FileWriter, CreateFileWriter);
    }

    private static RuntimeNode CreateConnection(
        NodeAddress address,
        NodeDefinition definition,
        Func<MqttConnectionProfile, IFluxMqttClient> clientFactory)
    {
        var profile = GetConnectionProfile(definition, PipelineFlowNodeTypes.Connection.Value);
        var component = new MqttConnectionComponent(clientFactory(profile), disposeClientOnDispose: true);

        return RuntimeNode.Create(
            address,
            component,
            outputs:
            [
                new OutputPort<FlowError>(address.Port(ErrorsPort), component.Errors)
            ]);
    }

    private static RuntimeNode CreateStoredSessionSource(
        NodeAddress address,
        NodeDefinition definition,
        IMessageRepository? messageRepository)
    {
        var sessionId = GetOptionalSessionId(definition, "sessionId");
        if (sessionId is null)
        {
            return CreateEmptyMqttSource(address, definition);
        }

        if (messageRepository is null)
        {
            throw new InvalidOperationException("Stored session source requires a message repository.");
        }

        var component = new StoredSessionSourceComponent(
            messageRepository,
            sessionId.Value,
            preserveTiming: GetBoolOrDefault(definition, "preserveTiming", false),
            speed: GetDoubleOrDefault(definition, "speed", 1),
            boundedCapacity: GetBoundedCapacity(definition));

        return SourceRuntimeNode(address, component, component.Output);
    }

    private static RuntimeNode CreateReplaySource(
        NodeAddress address,
        NodeDefinition definition,
        IMessageRepository? messageRepository)
    {
        var sessionId = GetOptionalSessionId(definition, "sessionId");
        if (sessionId is null)
        {
            return CreateEmptyMqttSource(address, definition);
        }

        if (messageRepository is null)
        {
            throw new InvalidOperationException("Replay source requires a message repository.");
        }

        var factory = new RecordedSessionReplayFactory(messageRepository);
        var component = factory.Create(
            sessionId.Value,
            new RecordedSessionReplayOptions
            {
                Speed = GetDoubleOrDefault(definition, "speed", 1),
                BoundedCapacity = GetBoundedCapacity(definition)
            });

        return SourceRuntimeNode(address, component, component.Output);
    }

    private static RuntimeNode CreateEmptyMqttSource(NodeAddress address, NodeDefinition definition)
    {
        var component = new GeneratedMqttSourceComponent(
            [],
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

    private static RuntimeNode CreateConditionRouter(
        NodeAddress address,
        NodeDefinition definition,
        IFlowExpressionEngine expressionEngine)
    {
        var predicate = new MqttEnvelopeExpressionPredicate(
            expressionEngine,
            GetRequiredString(definition, "expression"));
        var component = new MqttConditionRouterComponent(predicate.IsMatch, boundedCapacity: GetBoundedCapacity(definition));

        return RuntimeNode.Create(
            address,
            component,
            inputs:
            [
                new InputPort<MqttEnvelope>(address.Port(InputPort), component.Input)
            ],
            outputs:
            [
                new OutputPort<MqttEnvelope>(address.Port(WhenTruePort), component.WhenTrue),
                new OutputPort<MqttEnvelope>(address.Port(WhenFalsePort), component.WhenFalse),
                new OutputPort<FlowLogEntry>(address.Port(EntriesPort), component.Entries),
                new OutputPort<FlowError>(address.Port(ErrorsPort), component.Errors)
            ]);
    }

    private static RuntimeNode CreateFlowAssertion(
        NodeAddress address,
        NodeDefinition definition,
        IFlowExpressionEngine expressionEngine)
    {
        var inputType = NormalizeMapperTypeName(GetStringOrDefault(definition, "inputType", "MqttEnvelope"));
        return inputType switch
        {
            "MqttEnvelope" => CreateFlowAssertion<MqttEnvelope>(address, definition, expressionEngine),
            "MqttPublishRequest" => CreateFlowAssertion<MqttPublishRequest>(address, definition, expressionEngine),
            "MqttRecordingRequest" => CreateFlowAssertion<MqttRecordingRequest>(address, definition, expressionEngine),
            "FileWriteRequest" => CreateFlowAssertion<FileWriteRequest>(address, definition, expressionEngine),
            "JsonSchemaValidationResult" => CreateFlowAssertion<JsonSchemaValidationResult>(address, definition, expressionEngine),
            "InspectedMqttMessage" => CreateFlowAssertion<InspectedMqttMessage>(address, definition, expressionEngine),
            "MqttMetricsSnapshot" => CreateFlowAssertion<MqttMetricsSnapshot>(address, definition, expressionEngine),
            "FlowLogEntry" => CreateFlowAssertion<FlowLogEntry>(address, definition, expressionEngine),
            "FlowError" => CreateFlowAssertion<FlowError>(address, definition, expressionEngine),
            _ => throw new InvalidOperationException(
                $"Flow assertion inputType '{inputType}' is not supported yet. Supported inputType values: MqttEnvelope, MqttPublishRequest, MqttRecordingRequest, FileWriteRequest, JsonSchemaValidationResult, InspectedMqttMessage, MqttMetricsSnapshot, FlowLogEntry, FlowError.")
        };
    }

    private static RuntimeNode CreateFlowAssertion<TInput>(
        NodeAddress address,
        NodeDefinition definition,
        IFlowExpressionEngine expressionEngine)
    {
        var expression = GetRequiredString(definition, "expression");
        var predicate = new FlowAssertionExpressionPredicate<TInput>(expressionEngine, expression);
        var component = new FlowAssertionComponent<TInput>(
            predicate,
            GetStringOrDefault(definition, "assertionName", "Message assertion"),
            expression,
            GetNullableString(definition, "failureMessage"),
            boundedCapacity: GetBoundedCapacity(definition));

        return RuntimeNode.Create(
            address,
            component,
            inputs:
            [
                new InputPort<TInput>(address.Port(InputPort), component.Input)
            ],
            outputs:
            [
                new OutputPort<FlowAssertionResult>(address.Port(ResultPort), component.Result),
                new OutputPort<TInput>(address.Port(PassedPort), component.Passed),
                new OutputPort<TInput>(address.Port(FailedPort), component.Failed),
                new OutputPort<FlowLogEntry>(address.Port(EntriesPort), component.Entries),
                new OutputPort<FlowError>(address.Port(ErrorsPort), component.Errors)
            ]);
    }

    private static RuntimeNode CreateMqttMetrics(NodeAddress address, NodeDefinition definition)
    {
        var component = new MqttMetricsComponent(
            boundedCapacity: GetBoundedCapacity(definition),
            rateWindow: TimeSpan.FromSeconds(GetDoubleOrDefault(
                definition,
                "rateWindowSeconds",
                MqttMetricsComponent.DefaultRateWindow.TotalSeconds)));

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

    private static RuntimeNode CreateFlowLogger(NodeAddress address, NodeDefinition definition)
    {
        var hasMessageInput = definition.Ports.ContainsKey(InputPort.Value);
        var hasFlowErrorInput = definition.Ports.ContainsKey(FlowErrorsPort.Value);
        if (!hasMessageInput && !hasFlowErrorInput)
        {
            hasMessageInput = true;
        }

        var component = new FlowLoggerComponent(
            boundedCapacity: GetBoundedCapacity(definition),
            maxEntries: GetIntOrDefault(definition, "maxEntries", 500, minValue: 1),
            includePayloadPreview: GetBoolOrDefault(definition, "includePayloadPreview", false),
            maxPayloadPreviewChars: GetIntOrDefault(definition, "maxPayloadPreviewChars", 512, minValue: 1),
            waitForMessageInputCompletion: hasMessageInput,
            waitForFlowErrorInputCompletion: hasFlowErrorInput);

        return RuntimeNode.Create(
            address,
            component,
            inputs:
            [
                new InputPort<MqttEnvelope>(address.Port(InputPort), component.Input),
                new InputPort<FlowError>(address.Port(FlowErrorsPort), component.FlowErrors)
            ],
            outputs:
            [
                new OutputPort<FlowLogEntry>(address.Port(EntriesPort), component.Entries),
                new OutputPort<FlowError>(address.Port(ErrorsPort), component.Errors)
            ]);
    }

    private static RuntimeNode CreateJsonSchemaValidator(NodeAddress address, NodeDefinition definition)
    {
        var component = new JsonSchemaValidatorComponent(
            GetJsonSchemaValidatorDefinition(definition),
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
                new OutputPort<JsonSchemaValidationResult>(address.Port(ResultPort), component.Result),
                new OutputPort<MqttEnvelope>(address.Port(ValidPort), component.Valid),
                new OutputPort<MqttEnvelope>(address.Port(InvalidPort), component.Invalid),
                new OutputPort<FlowError>(address.Port(ErrorsPort), component.Errors)
            ]);
    }

    private static RuntimeNode CreatePublishRequestMapper(
        NodeAddress address,
        NodeDefinition definition,
        IFlowExpressionEngine expressionEngine)
    {
        var mapperEngine = GetMapperExpressionEngine(definition, expressionEngine);

        var component = new MqttPublishRequestMapperComponent(
            new MqttPublishRequestExpressionMapper(
                mapperEngine,
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

    private static RuntimeNode CreateDynamicMapper(
        NodeAddress address,
        NodeDefinition definition,
        IFlowExpressionEngine expressionEngine)
    {
        var inputType = NormalizeMapperTypeName(GetStringOrDefault(definition, "inputType", "MqttEnvelope"));
        if (inputType is not "MqttEnvelope")
        {
            throw new InvalidOperationException(
                $"Dynamic mapper inputType '{inputType}' is not supported yet. Supported inputType: MqttEnvelope.");
        }

        var outputType = NormalizeMapperTypeName(
            GetNullableString(definition, "outputType") ??
            GetNullableString(definition, "targetType") ??
            throw new InvalidOperationException("Dynamic mapper requires configuration value 'outputType'."));

        return outputType switch
        {
            "MqttPublishRequest" => CreatePublishRequestMapper(address, definition, expressionEngine),
            "MqttRecordingRequest" => CreateRecordingRequestMapper(address, definition, expressionEngine),
            "FileWriteRequest" => CreateFileWriteRequestMapper(address, definition, expressionEngine),
            _ => throw new InvalidOperationException(
                $"Dynamic mapper outputType '{outputType}' is not supported yet. Supported outputType values: MqttPublishRequest, MqttRecordingRequest, FileWriteRequest.")
        };
    }

    private static RuntimeNode CreateRecordingRequestMapper(
        NodeAddress address,
        NodeDefinition definition,
        IFlowExpressionEngine expressionEngine)
    {
        var expression = GetNullableString(definition, "expression");
        var component = string.IsNullOrWhiteSpace(expression)
            ? new MqttRecordingRequestMapperComponent(
                GetRequiredSessionId(definition, "sessionId"),
                boundedCapacity: GetBoundedCapacity(definition))
            : new MqttRecordingRequestMapperComponent(
                new MqttRecordingRequestExpressionMapper(
                    GetMapperExpressionEngine(definition, expressionEngine),
                    expression),
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

        var component = new MqttPublisherComponent(connection.Client, boundedCapacity: GetBoundedCapacity(definition));

        return RuntimeNode.Create(
            address,
            component,
            inputs:
            [
                new InputPort<MqttPublishRequest>(address.Port(InputPort), component.Input)
            ],
            outputs:
            [
                new OutputPort<FlowLogEntry>(address.Port(EntriesPort), component.Entries),
                new OutputPort<FlowError>(address.Port(ErrorsPort), component.Errors)
            ]);
    }

    private static RuntimeNode CreateRecorder(NodeAddress address, NodeDefinition definition, IMessageRepository? messageRepository)
    {
        if (messageRepository is null)
        {
            throw new InvalidOperationException("MQTT recorder requires a message repository.");
        }

        var component = new MqttRecorderComponent(messageRepository, boundedCapacity: GetBoundedCapacity(definition));

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
        var mapperEngine = GetMapperExpressionEngine(definition, expressionEngine);

        var component = new FileWriteRequestMapperComponent(
            new FileWriteRequestExpressionMapper(
                mapperEngine,
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
        var expression = GetNullableString(definition, "expression");
        if (!string.IsNullOrWhiteSpace(expression))
        {
            return new MqttPublishRequestMapDefinition
            {
                Expression = expression
            };
        }

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
        var expression = GetNullableString(definition, "expression");
        if (!string.IsNullOrWhiteSpace(expression))
        {
            return new FileWriteRequestMapDefinition
            {
                Expression = expression
            };
        }

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

    private static JsonSchemaValidatorDefinition GetJsonSchemaValidatorDefinition(NodeDefinition definition)
    {
        var schema = GetNullableString(definition, "schema");
        var schemaPath = GetNullableString(definition, "schemaPath") ?? GetNullableString(definition, "schemaFile");
        if (string.IsNullOrWhiteSpace(schema))
        {
            if (string.IsNullOrWhiteSpace(schemaPath))
            {
                throw new InvalidOperationException(
                    "JSON Schema validator requires configuration value 'schema' or 'schemaPath'.");
            }

            schema = File.ReadAllText(schemaPath);
        }

        return new JsonSchemaValidatorDefinition
        {
            SchemaJson = schema,
            SchemaId = GetNullableString(definition, "schemaId") ?? schemaPath ?? "inline"
        };
    }

    private static string NormalizeMapperTypeName(string value)
    {
        var trimmed = value.Trim();
        return trimmed switch
        {
            _ when trimmed.Contains('.') => trimmed[(trimmed.LastIndexOf('.') + 1)..],
            _ => trimmed
        };
    }

    private static IFlowExpressionEngine GetMapperExpressionEngine(
        NodeDefinition definition,
        IFlowExpressionEngine defaultExpressionEngine)
    {
        var requestedEngine = GetNullableString(definition, "engine") ?? GetNullableString(definition, "mapper");
        if (requestedEngine is null)
        {
            return defaultExpressionEngine;
        }

        if (string.Equals(requestedEngine, defaultExpressionEngine.Name, StringComparison.OrdinalIgnoreCase))
        {
            return defaultExpressionEngine;
        }

        if (string.Equals(requestedEngine, "jsonata", StringComparison.OrdinalIgnoreCase))
        {
            return new JsonataFlowExpressionEngine();
        }

        throw new InvalidOperationException(
            $"Mapper engine '{requestedEngine}' is not registered. Supported engines: {defaultExpressionEngine.Name}, jsonata.");
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
            JsonValueKind.String => [new MqttSubscription(ReadTopicFilter(subscriptionsElement.GetString()), DefaultSubscriptionQos)],
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
                    subscriptions.Add(new MqttSubscription(ReadTopicFilter(element.GetString()), DefaultSubscriptionQos));
                    break;
                case JsonValueKind.Object:
                {
                    var topicFilter = ReadRequiredString(element, "topicFilter");
                    var qualityOfService = ParseQualityOfService(element);
                    var receiveRetainedMessages = ReadBoolOrDefault(element, "receiveRetained", ReadBoolOrDefault(element, "retain", true));
                    var retainAsPublished = ReadBoolOrDefault(element, "retainAsPublished", true);
                    subscriptions.Add(new MqttSubscription(
                        topicFilter,
                        qualityOfService,
                        receiveRetainedMessages,
                        retainAsPublished));
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

    private static SessionId? GetOptionalSessionId(NodeDefinition definition, string key)
    {
        var value = GetNullableString(definition, key);
        if (value is null)
        {
            return null;
        }

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

    private static int GetIntOrDefault(NodeDefinition definition, string key, int defaultValue, int minValue)
    {
        if (!definition.Configuration.TryGetValue(key, out var value))
        {
            return defaultValue;
        }

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var result) || result < minValue)
        {
            throw new InvalidOperationException($"Configuration value '{key}' must be an integer greater than or equal to {minValue}.");
        }

        return result;
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
