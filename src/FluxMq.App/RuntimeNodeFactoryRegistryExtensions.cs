using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Core.Mqtt;
using FluxMq.Core.Secrets;
using FluxMq.Components.Mapping;
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
using FluxFlow.Engine.Mapping;
using FluxFlow.Engine.Runtime;

namespace FluxMq.App;

public static class RuntimeNodeFactoryRegistryExtensions
{
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
            FluxMqRuntimeNodeModuleCatalog.Resolve(runtimeServices),
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
}
