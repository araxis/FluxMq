using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Core.Mqtt;
using FluxMq.Core.Secrets;
using FluxMq.Components.JsonSchema;
using FluxMq.Components.Logging;
using FluxMq.Components.Mapping;
using FluxMq.Components.MqttMetrics;
using FluxMq.Components.MqttPayloadInspector;
using FluxMq.Components.Storage.Repositories;
using FluxMq.App.Metrics;
using FluxFlow.Components.Http;
using FluxFlow.Components.Mapping;
using FluxFlow.Components.Mapping.Options;
using FluxFlow.Components.Payloads;
using FluxFlow.Components.Routing;
using FluxFlow.Components.Serialization;
using FluxFlow.Components.Secrets;
using FluxFlow.Components.State;
using FluxFlow.Components.State.Options;
using FluxFlow.Components.Storage;
using FluxFlow.Components.Storage.FileSystem;
using FluxFlow.Components.Storage.Options;
using FluxFlow.Components.Timers;
using FluxFlow.Components.Timers.Options;
using FluxFlow.Engine.Components;
using FluxFlow.Engine.Definitions;
using FluxFlow.Engine.Mapping;
using FluxFlow.Engine.Runtime;
using FluxMq.Core.Metrics;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using static FluxMq.App.FluxMqRuntimeNodeConfigurationReader;

namespace FluxMq.App;

public static class RuntimeNodeFactoryRegistryExtensions
{
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
                engine => FluxMqControlNodeConfiguration.GetExpressionEngine(engine, expressionEngine)));

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

}
