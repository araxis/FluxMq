using FluxFlow.Engine.Definitions;
using FluxFlow.Engine.Mapping;
using FluxFlow.Engine.Runtime;
using FluxMq.App.Metrics;
using FluxMq.Components.Storage.Repositories;
using FluxMq.Core.Models;
using FluxMq.Core.Mqtt;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Collections.ObjectModel;

namespace FluxMq.App;

public sealed record FluxMqRuntimeNodeBuildContext(
    RuntimeNodeFactoryContext EngineContext,
    Func<MqttConnectionProfile, IMqttBrokerClient> ClientFactory,
    IMessageRepository? MessageRepository,
    IFlowExpressionEngine ExpressionEngine)
{
    public NodeAddress Address => EngineContext.Address;

    public NodeDefinition Definition => EngineContext.Definition;
}

public interface IFluxMqRuntimeNodeModule
{
    NodeType Type { get; }

    RuntimeNode Build(FluxMqRuntimeNodeBuildContext context);
}

public static class FluxMqRuntimeNodeModuleTypes
{
    public static IReadOnlyList<NodeType> All { get; } = new ReadOnlyCollection<NodeType>(
    [
        FluxMqNodeTypes.Connection,
        FluxMqNodeTypes.Trigger,
        FluxMqNodeTypes.ConnectionStateTrigger,
        FluxMqNodeTypes.StoredSessionSource,
        FluxMqNodeTypes.ReplaySource,
        FluxMqNodeTypes.GeneratedSource,
        FluxMqNodeTypes.PayloadInspector,
        FluxMqNodeTypes.MqttMetrics,
        FluxMqNodeTypes.FlowLogger,
        FluxMqNodeTypes.MessageFilter,
        FluxMqNodeTypes.ConditionRouter,
        FluxMqNodeTypes.FlowAssertion,
        FluxMqNodeTypes.JsonSchemaValidator,
        FluxMqNodeTypes.MqttPublisher,
        FluxMqNodeTypes.MqttRecorder,
        FluxMqNodeTypes.FileWriter
    ]);
}

public static class FluxMqRuntimeNodeServiceCollectionExtensions
{
    public static IServiceCollection AddFluxMqRuntimeNodes(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddFluxMqRuntimeNodeModule<MqttConnectionRuntimeNodeModule>(FluxMqNodeTypes.Connection);
        services.AddFluxMqRuntimeNodeModule<MqttTriggerRuntimeNodeModule>(FluxMqNodeTypes.Trigger);
        services.AddFluxMqRuntimeNodeModule<ConnectionStateTriggerRuntimeNodeModule>(FluxMqNodeTypes.ConnectionStateTrigger);
        services.AddFluxMqRuntimeNodeModule<StoredSessionSourceRuntimeNodeModule>(FluxMqNodeTypes.StoredSessionSource);
        services.AddFluxMqRuntimeNodeModule<ReplaySourceRuntimeNodeModule>(FluxMqNodeTypes.ReplaySource);
        services.AddFluxMqRuntimeNodeModule<GeneratedMqttSourceRuntimeNodeModule>(FluxMqNodeTypes.GeneratedSource);
        services.AddFluxMqRuntimeNodeModule<PayloadInspectorRuntimeNodeModule>(FluxMqNodeTypes.PayloadInspector);
        services.AddFluxMqRuntimeNodeModule<MqttMetricsRuntimeNodeModule>(FluxMqNodeTypes.MqttMetrics);
        services.AddFluxMqRuntimeNodeModule<FlowLoggerRuntimeNodeModule>(FluxMqNodeTypes.FlowLogger);
        services.AddFluxMqRuntimeNodeModule<MessageFilterRuntimeNodeModule>(FluxMqNodeTypes.MessageFilter);
        services.AddFluxMqRuntimeNodeModule<ConditionRouterRuntimeNodeModule>(FluxMqNodeTypes.ConditionRouter);
        services.AddFluxMqRuntimeNodeModule<FlowAssertionRuntimeNodeModule>(FluxMqNodeTypes.FlowAssertion);
        services.AddFluxMqRuntimeNodeModule<JsonSchemaValidatorRuntimeNodeModule>(FluxMqNodeTypes.JsonSchemaValidator);
        services.AddFluxMqRuntimeNodeModule<MqttPublisherRuntimeNodeModule>(FluxMqNodeTypes.MqttPublisher);
        services.AddFluxMqRuntimeNodeModule<MqttRecorderRuntimeNodeModule>(FluxMqNodeTypes.MqttRecorder);
        services.AddFluxMqRuntimeNodeModule<FileWriterRuntimeNodeModule>(FluxMqNodeTypes.FileWriter);

        return services;
    }

    private static IServiceCollection AddFluxMqRuntimeNodeModule<TModule>(
        this IServiceCollection services,
        NodeType type)
        where TModule : class, IFluxMqRuntimeNodeModule
    {
        services.AddSingleton<TModule>();
        services.AddSingleton<IFluxMqRuntimeNodeModule>(static provider => provider.GetRequiredService<TModule>());
        services.AddKeyedSingleton<IFluxMqRuntimeNodeModule>(
            type.Value,
            static (provider, _) => provider.GetRequiredService<TModule>());
        return services;
    }
}

public static class FluxMqAppRuntimeServiceCollectionExtensions
{
    public static IServiceCollection AddFluxMqAppRuntime(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services
            .AddFluxMetrics()
            .AddFluxMqRuntimeNodes();
        services.TryAddSingleton<FluxMetricStreamCoordinator>();

        return services;
    }
}
