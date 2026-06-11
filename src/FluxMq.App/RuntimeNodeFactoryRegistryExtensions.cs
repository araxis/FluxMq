using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Core.Mqtt;
using FluxMq.Core.Secrets;
using FluxMq.Components.Assertions;
using FluxMq.Components.ConnectionStateTrigger;
using FluxMq.Components.Control;
using FluxMq.Components.FileWriter;
using FluxMq.Components.JsonSchema;
using FluxMq.Components.Logging;
using FluxMq.Components.Mapping;
using FluxMq.Components.MessageSource;
using FluxMq.Components.MqttMetrics;
using FluxMq.Components.MqttPayloadInspector;
using FluxMq.Components.MqttPublisher;
using FluxMq.Components.Replay;
using FluxMq.Components.Storage.Repositories;
using FluxMq.App.Metrics;
using FluxFlow.Components.Assertions.Options;
using FluxFlow.Components.Control.Contracts;
using FluxFlow.Components.Control.Options;
using FluxFlow.Components.Http;
using FluxFlow.Components.Http.Contracts;
using FluxFlow.Components.Mapping;
using FluxFlow.Components.Mapping.Options;
using FluxFlow.Components.Payloads;
using FluxFlow.Components.Payloads.Contracts;
using FluxFlow.Components.Routing;
using FluxFlow.Components.Routing.Contracts;
using FluxFlow.Components.Routing.Options;
using FluxFlow.Components.Serialization;
using FluxFlow.Components.Secrets;
using FluxFlow.Components.Sources.Nodes;
using FluxFlow.Components.Sources.Options;
using FluxFlow.Components.State;
using FluxFlow.Components.State.Contracts;
using FluxFlow.Components.State.Options;
using FluxFlow.Components.Storage;
using FluxFlow.Components.Storage.FileSystem;
using FluxFlow.Components.Storage.Options;
using FluxFlow.Components.Timers;
using FluxFlow.Components.Timers.Contracts;
using FluxFlow.Components.Timers.Options;
using FluxFlow.Engine.Components;
using FluxFlow.Engine.Definitions;
using FluxFlow.Engine.Mapping;
using FluxFlow.Engine.Runtime;
using FluxMq.Core.Metrics;
using Microsoft.Extensions.DependencyInjection;
using MQTTnet.Protocol;
using System.Text.Json;
using System.Threading.Tasks.Dataflow;
using AssertionNodeContext = FluxFlow.Components.Assertions.Contracts.AssertionNodeContext;
using static FluxMq.App.FluxMqRuntimeNodeConfigurationReader;

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
        Func<MqttConnectionProfile, IMqttBrokerClient>? clientFactory = null,
        IMessageRepository? messageRepository = null,
        IFlowExpressionEngine? expressionEngine = null,
        string? fileSystemStorageRootDirectory = null,
        ISecretResolver? secretResolver = null,
        FluxMetricRuntimeHost? metricRuntimeHost = null,
        IServiceProvider? runtimeServices = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        clientFactory ??= profile => new MqttBrokerClient(profile, secretResolver);
        expressionEngine ??= FluxMqExpressionEngines.CreateDefault();

        registry
            .RegisterPayloadComponents()
            .RegisterHttpComponents()
            .RegisterTimerComponents(FluxMqRuntimePackageComponentOptions.ConfigureTimerComponents)
            .RegisterMappingComponents(options => FluxMqRuntimePackageComponentOptions.ConfigureMappingComponents(options, expressionEngine))
            .RegisterStateComponents(options => FluxMqRuntimePackageComponentOptions.ConfigureStateComponents(options, expressionEngine))
            .RegisterStorageComponents(options => FluxMqRuntimePackageComponentOptions.ConfigureStorageComponents(options, fileSystemStorageRootDirectory))
            .RegisterSerializationComponents()
            .RegisterRoutingComponents(options => FluxMqRuntimePackageComponentOptions.ConfigureRoutingComponents(
                options,
                engine => GetExpressionEngine(engine, expressionEngine)));

        return registry.RegisterFluxMqRuntimeAdapters(
            ResolveFluxMqRuntimeNodeModules(runtimeServices),
            clientFactory,
            messageRepository,
            expressionEngine,
            metricRuntimeHost);
    }

    private static RuntimeNodeFactoryRegistry RegisterFluxMqRuntimeAdapters(
        this RuntimeNodeFactoryRegistry registry,
        IReadOnlyList<IFluxMqRuntimeNodeModule> modules,
        Func<MqttConnectionProfile, IMqttBrokerClient> clientFactory,
        IMessageRepository? messageRepository,
        IFlowExpressionEngine expressionEngine,
        FluxMetricRuntimeHost? metricRuntimeHost)
    {
        ArgumentNullException.ThrowIfNull(modules);

        foreach (var module in modules)
        {
            registry.Register(module.Type, context => module.Build(new FluxMqRuntimeNodeBuildContext(
                context,
                clientFactory,
                messageRepository,
                expressionEngine,
                metricRuntimeHost)));
        }

        return registry;
    }

    private static IReadOnlyList<IFluxMqRuntimeNodeModule> ResolveFluxMqRuntimeNodeModules(IServiceProvider? runtimeServices)
    {
        if (runtimeServices is null)
        {
            return CreateDefaultFluxMqRuntimeNodeModules();
        }

        return [.. FluxMqRuntimeNodeModuleTypes.All.Select(type =>
            runtimeServices.GetRequiredKeyedService<IFluxMqRuntimeNodeModule>(type.Value))];
    }

    private static IReadOnlyList<IFluxMqRuntimeNodeModule> CreateDefaultFluxMqRuntimeNodeModules()
        =>
        [
            new MqttConnectionRuntimeNodeModule(),
            new MqttTriggerRuntimeNodeModule(),
            new ConnectionStateTriggerRuntimeNodeModule(),
            new StoredSessionSourceRuntimeNodeModule(),
            new ReplaySourceRuntimeNodeModule(),
            new GeneratedMqttSourceRuntimeNodeModule(),
            new MetricSourceRuntimeNodeModule(),
            new PayloadInspectorRuntimeNodeModule(),
            new MqttMetricsRuntimeNodeModule(),
            new FlowLoggerRuntimeNodeModule(),
            new MessageFilterRuntimeNodeModule(),
            new ConditionRouterRuntimeNodeModule(),
            new FlowAssertionRuntimeNodeModule(),
            new JsonSchemaValidatorRuntimeNodeModule(),
            new MqttPublisherRuntimeNodeModule(),
            new MqttRecorderRuntimeNodeModule(),
            new FileWriterRuntimeNodeModule()
        ];

    private static ControlExpressionOptions GetFilterControlOptions(NodeDefinition definition)
    {
        var patterns = GetFilterPatterns(definition);
        var expression = GetNullableString(definition, "expression");
        var effectiveExpression = patterns.Count switch
        {
            > 0 when string.IsNullOrWhiteSpace(expression) => "topicMatches",
            > 0 => $"topicMatches && ({expression})",
            _ => string.IsNullOrWhiteSpace(expression) ? "true" : expression!
        };

        return CreateControlOptions(
            definition,
            FluxMqNodeTypes.MessageFilter.Value,
            "MqttEnvelope",
            effectiveExpression);
    }

    private static ControlExpressionOptions GetControlOptions(
        NodeDefinition definition,
        string nodeType,
        string defaultInputType = "MqttEnvelope",
        string? fallbackExpression = null)
    {
        var expression = GetNullableString(definition, "expression") ?? fallbackExpression;
        if (string.IsNullOrWhiteSpace(expression))
        {
            throw new InvalidOperationException($"{nodeType} requires configuration value 'expression'.");
        }

        return CreateControlOptions(
            definition,
            nodeType,
            NormalizeMapperTypeName(GetStringOrDefault(definition, "inputType", defaultInputType)),
            expression);
    }

    private static ControlExpressionOptions CreateControlOptions(
        NodeDefinition definition,
        string nodeType,
        string inputType,
        string expression)
    {
        if (string.IsNullOrWhiteSpace(inputType))
        {
            throw new InvalidOperationException($"{nodeType} option 'inputType' cannot be empty.");
        }

        var boundedCapacity = GetBoundedCapacity(definition);
        if (boundedCapacity <= 0)
        {
            throw new InvalidOperationException($"{nodeType} option 'boundedCapacity' must be greater than zero.");
        }

        return new ControlExpressionOptions
        {
            Engine = GetNullableString(definition, "engine"),
            Expression = expression.Trim(),
            ExpressionId = GetNullableString(definition, "expressionId"),
            ExpressionName = GetNullableString(definition, "expressionName"),
            InputType = inputType,
            BoundedCapacity = boundedCapacity
        };
    }

    private static IFlowExpressionEngine GetControlExpressionEngine(
        ControlExpressionOptions options,
        IFlowExpressionEngine defaultExpressionEngine)
        => GetExpressionEngine(options.Engine, defaultExpressionEngine);

    private static IFlowExpressionEngine GetAssertionExpressionEngine(
        AssertionOptions options,
        IFlowExpressionEngine defaultExpressionEngine)
        => GetExpressionEngine(options.Engine, defaultExpressionEngine);

    private static IFlowExpressionEngine GetExpressionEngine(
        string? requestedEngine,
        IFlowExpressionEngine defaultExpressionEngine)
    {
        if (string.IsNullOrWhiteSpace(requestedEngine) ||
            string.Equals(requestedEngine, defaultExpressionEngine.Name, StringComparison.OrdinalIgnoreCase))
        {
            return defaultExpressionEngine;
        }

        if (string.Equals(requestedEngine, "jsonata", StringComparison.OrdinalIgnoreCase))
        {
            return FluxMqExpressionEngines.CreateJsonata();
        }

        throw new InvalidOperationException(
            $"Control expression engine '{requestedEngine}' is not registered. Supported engines: {defaultExpressionEngine.Name}, jsonata.");
    }

    private static ControlNodeContext CreateControlNodeContext<TInput>(
        NodeAddress address,
        ControlExpressionOptions options)
        => new()
        {
            Address = address,
            Options = options,
            InputType = typeof(TInput)
        };

    internal static RuntimeNode CreateConnection(
        NodeAddress address,
        NodeDefinition definition,
        Func<MqttConnectionProfile, IMqttBrokerClient> clientFactory)
    {
        var profile = GetConnectionProfile(definition, FluxMqNodeTypes.Connection.Value);
        var component = new MqttConnectionComponent(clientFactory(profile), disposeClientOnDispose: true);

        return RuntimeNode.Create(
            address,
            component,
            outputs:
            [
                new OutputPort<FlowError>(address.Port(ErrorsPort), component.Errors)
            ]);
    }

    internal static RuntimeNode CreateTrigger(
        NodeAddress address,
        NodeDefinition definition,
        RuntimeNodeFactoryContext context)
    {
        var connectionRef = GetRequiredString(definition, "connection");
        var resource = context.GetResource(new NodeName(connectionRef));
        if (resource.Node is not MqttConnectionComponent connection)
        {
            throw new InvalidOperationException(
                $"Resource '{connectionRef}' must be of type '{FluxMqNodeTypes.Connection.Value}' to be used by an mqtt.trigger.");
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

    internal static RuntimeNode CreateConnectionStateTrigger(
        NodeAddress address,
        NodeDefinition definition,
        RuntimeNodeFactoryContext context)
    {
        var connectionRef = GetRequiredString(definition, "connection");
        var resource = context.GetResource(new NodeName(connectionRef));
        if (resource.Node is not MqttConnectionComponent connection)
        {
            throw new InvalidOperationException(
                $"Resource '{connectionRef}' must be of type '{FluxMqNodeTypes.Connection.Value}' to be used by an mqtt.connection-state-trigger.");
        }

        var component = new ConnectionStateTriggerComponent(connection.Client);

        return RuntimeNode.Create(
            address,
            component,
            outputs:
            [
                new OutputPort<MqttClientStateChangedEventArgs>(address.Port(OutputPort), component.Output),
                new OutputPort<FlowError>(address.Port(ErrorsPort), component.Errors)
            ]);
    }

    internal static RuntimeNode CreateMessageFilter(
        NodeAddress address,
        NodeDefinition definition,
        IFlowExpressionEngine expressionEngine)
    {
        var options = GetFilterControlOptions(definition);
        var component = new FlowFilterComponent<MqttEnvelope>(
            options,
            GetControlExpressionEngine(options, expressionEngine),
            new TopicFilterControlContextFactory(GetFilterPatterns(definition)),
            CreateControlNodeContext<MqttEnvelope>(address, options));

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

    internal static RuntimeNode CreatePayloadInspector(NodeAddress address, NodeDefinition definition)
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

    internal static RuntimeNode CreateConditionRouter(
        NodeAddress address,
        NodeDefinition definition,
        IFlowExpressionEngine expressionEngine)
    {
        var options = GetControlOptions(definition, FluxMqNodeTypes.ConditionRouter.Value);
        var inputType = FlowContractTypeNames.Normalize(options.InputType);
        return inputType switch
        {
            FlowContractTypeNames.NumberMetricReading => CreateConditionRouter<FluxMetricReading<double>>(address, options, expressionEngine),
            FlowContractTypeNames.MqttEnvelope => CreateConditionRouter<MqttEnvelope>(address, options, expressionEngine),
            _ => throw new InvalidOperationException(
                $"Flow condition inputType '{inputType}' is not supported yet. Supported inputType values: {FlowContractTypeNames.MqttEnvelope}, {FlowContractTypeNames.NumberMetricReading}.")
        };
    }

    private static RuntimeNode CreateConditionRouter<TInput>(
        NodeAddress address,
        ControlExpressionOptions options,
        IFlowExpressionEngine expressionEngine)
    {
        var component = new FlowWhenComponent<TInput>(
            options,
            GetControlExpressionEngine(options, expressionEngine),
            new FluxMqControlContextFactory(),
            CreateControlNodeContext<TInput>(address, options));

        return RuntimeNode.Create(
            address,
            component,
            inputs:
            [
                new InputPort<TInput>(address.Port(InputPort), component.Input)
            ],
            outputs:
            [
                new OutputPort<TInput>(address.Port(WhenTruePort), component.WhenTrue),
                new OutputPort<TInput>(address.Port(WhenFalsePort), component.WhenFalse),
                new OutputPort<FlowLogEntry>(address.Port(EntriesPort), component.Entries),
                new OutputPort<FlowError>(address.Port(ErrorsPort), component.Errors)
            ]);
    }

    internal static RuntimeNode CreateFlowAssertion(
        NodeAddress address,
        NodeDefinition definition,
        IFlowExpressionEngine expressionEngine)
    {
        var inputType = FlowContractTypeNames.Normalize(GetStringOrDefault(definition, "inputType", FlowContractTypeNames.MqttEnvelope));
        return inputType switch
        {
            FlowContractTypeNames.MqttEnvelope => CreateFlowAssertion<MqttEnvelope>(address, definition, expressionEngine),
            FlowContractTypeNames.MqttPublishRequest => CreateFlowAssertion<MqttPublishRequest>(address, definition, expressionEngine),
            FlowContractTypeNames.MqttRecordingRequest => CreateFlowAssertion<MqttRecordingRequest>(address, definition, expressionEngine),
            FlowContractTypeNames.FileWriteRequest => CreateFlowAssertion<FileWriteRequest>(address, definition, expressionEngine),
            FlowContractTypeNames.PayloadInspectionRequest => CreateFlowAssertion<PayloadInspectionRequest>(address, definition, expressionEngine),
            FlowContractTypeNames.PayloadInspectionResult => CreateFlowAssertion<PayloadInspectionResult>(address, definition, expressionEngine),
            FlowContractTypeNames.HttpRequestInput => CreateFlowAssertion<HttpRequestInput>(address, definition, expressionEngine),
            FlowContractTypeNames.HttpResponseOutput => CreateFlowAssertion<HttpResponseOutput>(address, definition, expressionEngine),
            FlowContractTypeNames.HttpErrorOutput => CreateFlowAssertion<HttpErrorOutput>(address, definition, expressionEngine),
            FlowContractTypeNames.JsonSchemaValidationResult => CreateFlowAssertion<JsonSchemaValidationResult>(address, definition, expressionEngine),
            FlowContractTypeNames.InspectedMqttMessage => CreateFlowAssertion<InspectedMqttMessage>(address, definition, expressionEngine),
            FlowContractTypeNames.MqttMetricsSnapshot => CreateFlowAssertion<MqttMetricsSnapshot>(address, definition, expressionEngine),
            FlowContractTypeNames.TimerTick => CreateFlowAssertion<TimerTick>(address, definition, expressionEngine),
            FlowContractTypeNames.ScheduleTick => CreateFlowAssertion<ScheduleTick>(address, definition, expressionEngine),
            FlowContractTypeNames.StateReducerResult => CreateFlowAssertion<StateReducerResult>(address, definition, expressionEngine),
            FlowContractTypeNames.FlowLogEntry => CreateFlowAssertion<FlowLogEntry>(address, definition, expressionEngine),
            FlowContractTypeNames.FlowError => CreateFlowAssertion<FlowError>(address, definition, expressionEngine),
            FlowContractTypeNames.NumberMetricReading => CreateFlowAssertion<FluxMetricReading<double>>(address, definition, expressionEngine),
            _ => throw new InvalidOperationException(
                $"Flow assertion inputType '{inputType}' is not supported yet. Supported inputType values: {FlowContractTypeNames.FormatAssertionInputTypes()}.")
        };
    }

    private static RuntimeNode CreateFlowAssertion<TInput>(
        NodeAddress address,
        NodeDefinition definition,
        IFlowExpressionEngine expressionEngine)
    {
        var expression = GetRequiredString(definition, "expression");
        var options = GetControlOptions(
            definition,
            FluxMqNodeTypes.FlowAssertion.Value,
            typeof(TInput).Name,
            expression);
        var assertionOptions = CreateAssertionOptions(definition, options);
        var component = new FlowAssertionComponent<TInput>(
            assertionOptions,
            GetAssertionExpressionEngine(assertionOptions, expressionEngine),
            new FluxMqControlContextFactory(),
            CreateAssertionNodeContext<TInput>(address, assertionOptions));

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

    private static AssertionOptions CreateAssertionOptions(
        NodeDefinition definition,
        ControlExpressionOptions options)
        => new()
        {
            Engine = options.Engine,
            Expression = options.Expression,
            ExpressionId = options.ExpressionId,
            ExpressionName = options.ExpressionName,
            InputType = options.InputType,
            BoundedCapacity = options.BoundedCapacity,
            Description = GetNullableString(definition, "name") ??
                          GetNullableString(definition, "assertionName") ??
                          "Flow assertion",
            FailureMessage = GetNullableString(definition, "failureMessage"),
            EmitPassedInput = true,
            EmitFailedInput = true
        };

    private static AssertionNodeContext CreateAssertionNodeContext<TInput>(
        NodeAddress address,
        AssertionOptions options)
        => new()
        {
            Address = address,
            Options = options,
            InputType = typeof(TInput)
        };

    internal static RuntimeNode CreateMqttMetrics(NodeAddress address, NodeDefinition definition)
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

    internal static RuntimeNode CreateFlowLogger(NodeAddress address, NodeDefinition definition)
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

    internal static RuntimeNode CreateJsonSchemaValidator(NodeAddress address, NodeDefinition definition)
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

    internal static RuntimeNode CreatePublisher(
        NodeAddress address,
        NodeDefinition definition,
        RuntimeNodeFactoryContext context)
    {
        var connectionRef = GetRequiredString(definition, "connection");
        var resource = context.GetResource(new NodeName(connectionRef));
        if (resource.Node is not MqttConnectionComponent connection)
        {
            throw new InvalidOperationException(
                $"Resource '{connectionRef}' must be of type '{FluxMqNodeTypes.Connection.Value}' to be used by an mqtt.publisher.");
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

    internal static RuntimeNode CreateRecorder(NodeAddress address, NodeDefinition definition, IMessageRepository? messageRepository)
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

    internal static RuntimeNode CreateFileWriter(NodeAddress address, NodeDefinition definition)
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

    private static JsonSchemaValidatorDefinition GetJsonSchemaValidatorDefinition(NodeDefinition definition)
    {
        var schema = GetOptionalJsonSchema(definition);
        var schemaPath = GetNullableString(definition, "schemaPath") ?? GetNullableString(definition, "schemaFile");
        if (schema is null && string.IsNullOrWhiteSpace(schemaPath))
        {
            throw new InvalidOperationException(
                "JSON Schema validator requires configuration value 'schema' or 'schemaPath'.");
        }

        return new JsonSchemaValidatorDefinition
        {
            SchemaJson = schema,
            SchemaPath = schemaPath,
            SchemaId = GetNullableString(definition, "schemaId") ?? schemaPath ?? "inline"
        };
    }

    private static string? GetOptionalJsonSchema(NodeDefinition definition)
    {
        if (!definition.Configuration.TryGetValue("schema", out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            var schema = value.GetString();
            return string.IsNullOrWhiteSpace(schema) ? null : schema;
        }

        return value.GetRawText();
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
            PasswordSecret = SecretReferenceJson.ReadOptional(profileElement, "passwordSecret"),
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

    private static string ReadTopicFilter(string? value, string propertyName = "topicFilter")
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Configuration value '{propertyName}' must not be empty.");
        }

        return value;
    }

    private sealed class TopicFilterControlContextFactory(IReadOnlyList<string> patterns) : IControlContextFactory
    {
        private readonly FluxMqControlContextFactory _inner = new();

        public FlowMapContext Create(object? input, ControlNodeContext context)
        {
            var baseContext = _inner.Create(input, context);
            var variables = new Dictionary<string, object?>(baseContext.Variables, StringComparer.Ordinal)
            {
                ["topicMatches"] = input is MqttEnvelope envelope &&
                                   patterns.Count > 0 &&
                                   MqttTopicFilterMatcher.MatchesAny(patterns, envelope.Topic)
            };

            return baseContext with { Variables = variables };
        }
    }

}
