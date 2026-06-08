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
    IFlowExpressionEngine ExpressionEngine,
    FluxMetricRuntimeHost? MetricRuntimeHost)
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
        FluxMqNodeTypes.MetricSource,
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
        services.AddFluxMqRuntimeNodeModule<MetricSourceRuntimeNodeModule>(FluxMqNodeTypes.MetricSource);
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
    public static IServiceCollection AddFluxMqAppRuntime(
        this IServiceCollection services,
        FluxMetricCatalog? metricCatalog = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services
            .AddFluxMqMetricStreams(metricCatalog, timeProvider)
            .AddFluxMqRuntimeNodes();
        services.TryAddSingleton<FluxMetricRuntimeHost>();

        return services;
    }
}

internal sealed class MqttConnectionRuntimeNodeModule : IFluxMqRuntimeNodeModule
{
    public NodeType Type => FluxMqNodeTypes.Connection;

    public RuntimeNode Build(FluxMqRuntimeNodeBuildContext context)
        => RuntimeNodeFactoryRegistryExtensions.CreateConnection(context.Address, context.Definition, context.ClientFactory);
}

internal sealed class MqttTriggerRuntimeNodeModule : IFluxMqRuntimeNodeModule
{
    public NodeType Type => FluxMqNodeTypes.Trigger;

    public RuntimeNode Build(FluxMqRuntimeNodeBuildContext context)
        => RuntimeNodeFactoryRegistryExtensions.CreateTrigger(context.Address, context.Definition, context.EngineContext);
}

internal sealed class ConnectionStateTriggerRuntimeNodeModule : IFluxMqRuntimeNodeModule
{
    public NodeType Type => FluxMqNodeTypes.ConnectionStateTrigger;

    public RuntimeNode Build(FluxMqRuntimeNodeBuildContext context)
        => RuntimeNodeFactoryRegistryExtensions.CreateConnectionStateTrigger(context.Address, context.Definition, context.EngineContext);
}

internal sealed class StoredSessionSourceRuntimeNodeModule : IFluxMqRuntimeNodeModule
{
    public NodeType Type => FluxMqNodeTypes.StoredSessionSource;

    public RuntimeNode Build(FluxMqRuntimeNodeBuildContext context)
        => RuntimeNodeFactoryRegistryExtensions.CreateStoredSessionSource(context.Address, context.Definition, context.MessageRepository);
}

internal sealed class ReplaySourceRuntimeNodeModule : IFluxMqRuntimeNodeModule
{
    public NodeType Type => FluxMqNodeTypes.ReplaySource;

    public RuntimeNode Build(FluxMqRuntimeNodeBuildContext context)
        => RuntimeNodeFactoryRegistryExtensions.CreateReplaySource(context.Address, context.Definition, context.MessageRepository);
}

internal sealed class GeneratedMqttSourceRuntimeNodeModule : IFluxMqRuntimeNodeModule
{
    public NodeType Type => FluxMqNodeTypes.GeneratedSource;

    public RuntimeNode Build(FluxMqRuntimeNodeBuildContext context)
        => RuntimeNodeFactoryRegistryExtensions.CreateGeneratedMqttSource(context.Address, context.Definition);
}

internal sealed class MetricSourceRuntimeNodeModule : IFluxMqRuntimeNodeModule
{
    public NodeType Type => FluxMqNodeTypes.MetricSource;

    public RuntimeNode Build(FluxMqRuntimeNodeBuildContext context)
        => RuntimeNodeFactoryRegistryExtensions.CreateMetricSource(context.Address, context.Definition, context.MetricRuntimeHost);
}

internal sealed class PayloadInspectorRuntimeNodeModule : IFluxMqRuntimeNodeModule
{
    public NodeType Type => FluxMqNodeTypes.PayloadInspector;

    public RuntimeNode Build(FluxMqRuntimeNodeBuildContext context)
        => RuntimeNodeFactoryRegistryExtensions.CreatePayloadInspector(context.Address, context.Definition);
}

internal sealed class MqttMetricsRuntimeNodeModule : IFluxMqRuntimeNodeModule
{
    public NodeType Type => FluxMqNodeTypes.MqttMetrics;

    public RuntimeNode Build(FluxMqRuntimeNodeBuildContext context)
        => RuntimeNodeFactoryRegistryExtensions.CreateMqttMetrics(context.Address, context.Definition);
}

internal sealed class FlowLoggerRuntimeNodeModule : IFluxMqRuntimeNodeModule
{
    public NodeType Type => FluxMqNodeTypes.FlowLogger;

    public RuntimeNode Build(FluxMqRuntimeNodeBuildContext context)
        => RuntimeNodeFactoryRegistryExtensions.CreateFlowLogger(context.Address, context.Definition);
}

internal sealed class MessageFilterRuntimeNodeModule : IFluxMqRuntimeNodeModule
{
    public NodeType Type => FluxMqNodeTypes.MessageFilter;

    public RuntimeNode Build(FluxMqRuntimeNodeBuildContext context)
        => RuntimeNodeFactoryRegistryExtensions.CreateMessageFilter(context.Address, context.Definition, context.ExpressionEngine);
}

internal sealed class ConditionRouterRuntimeNodeModule : IFluxMqRuntimeNodeModule
{
    public NodeType Type => FluxMqNodeTypes.ConditionRouter;

    public RuntimeNode Build(FluxMqRuntimeNodeBuildContext context)
        => RuntimeNodeFactoryRegistryExtensions.CreateConditionRouter(context.Address, context.Definition, context.ExpressionEngine);
}

internal sealed class FlowAssertionRuntimeNodeModule : IFluxMqRuntimeNodeModule
{
    public NodeType Type => FluxMqNodeTypes.FlowAssertion;

    public RuntimeNode Build(FluxMqRuntimeNodeBuildContext context)
        => RuntimeNodeFactoryRegistryExtensions.CreateFlowAssertion(context.Address, context.Definition, context.ExpressionEngine);
}

internal sealed class JsonSchemaValidatorRuntimeNodeModule : IFluxMqRuntimeNodeModule
{
    public NodeType Type => FluxMqNodeTypes.JsonSchemaValidator;

    public RuntimeNode Build(FluxMqRuntimeNodeBuildContext context)
        => RuntimeNodeFactoryRegistryExtensions.CreateJsonSchemaValidator(context.Address, context.Definition);
}

internal sealed class MqttPublisherRuntimeNodeModule : IFluxMqRuntimeNodeModule
{
    public NodeType Type => FluxMqNodeTypes.MqttPublisher;

    public RuntimeNode Build(FluxMqRuntimeNodeBuildContext context)
        => RuntimeNodeFactoryRegistryExtensions.CreatePublisher(context.Address, context.Definition, context.EngineContext);
}

internal sealed class MqttRecorderRuntimeNodeModule : IFluxMqRuntimeNodeModule
{
    public NodeType Type => FluxMqNodeTypes.MqttRecorder;

    public RuntimeNode Build(FluxMqRuntimeNodeBuildContext context)
        => RuntimeNodeFactoryRegistryExtensions.CreateRecorder(context.Address, context.Definition, context.MessageRepository);
}

internal sealed class FileWriterRuntimeNodeModule : IFluxMqRuntimeNodeModule
{
    public NodeType Type => FluxMqNodeTypes.FileWriter;

    public RuntimeNode Build(FluxMqRuntimeNodeBuildContext context)
        => RuntimeNodeFactoryRegistryExtensions.CreateFileWriter(context.Address, context.Definition);
}
