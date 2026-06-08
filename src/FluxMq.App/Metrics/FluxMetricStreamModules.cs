using FluxFlow.Engine.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.App.Metrics;

public sealed record FluxMetricStreamBuildContext<TValue>(
    string Id,
    FluxMetricDefinition Definition,
    ISourceBlock<FlowEvent> Events,
    int BoundedCapacity);

public interface IFluxMetricStreamModule<TValue>
{
    string MeasureId { get; }

    IFluxMetric<TValue> Build(FluxMetricStreamBuildContext<TValue> context);
}

public interface IFluxMetricStreamModuleResolver
{
    IFluxMetricStreamModule<double> GetNumberModule(string measureId);
}

public sealed class FluxMetricStreamModuleResolver(IServiceProvider services) : IFluxMetricStreamModuleResolver
{
    private readonly IServiceProvider _services = services ?? throw new ArgumentNullException(nameof(services));

    public IFluxMetricStreamModule<double> GetNumberModule(string measureId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(measureId);

        return _services.GetKeyedService<IFluxMetricStreamModule<double>>(measureId.Trim()) ??
            throw new InvalidOperationException($"Metric measure '{measureId}' is not registered.");
    }

    public static IFluxMetricStreamModuleResolver CreateDefault(
        FluxMetricCatalog? catalog = null,
        TimeProvider? timeProvider = null)
    {
        var services = new ServiceCollection();
        services.AddFluxMqMetricStreams(catalog, timeProvider);
        return services
            .BuildServiceProvider()
            .GetRequiredService<IFluxMetricStreamModuleResolver>();
    }
}

public static class FluxMetricStreamServiceCollectionExtensions
{
    public static IServiceCollection AddFluxMqMetricStreams(
        this IServiceCollection services,
        FluxMetricCatalog? catalog = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(catalog ?? FluxMetricCatalog.Shared);
        services.TryAddSingleton(timeProvider ?? TimeProvider.System);
        services.TryAddSingleton<FluxMetricResolver>();
        services.TryAddSingleton<IFluxMetricStreamModuleResolver, FluxMetricStreamModuleResolver>();

        services.AddFluxMetricStreamModule<FluxEventCountMetricModule>(FluxMetricCatalog.MeasureCount);
        services.AddFluxMetricStreamModule<FluxEventRateMetricModule>(FluxMetricCatalog.MeasureRate);
        services.AddFluxMetricStreamModule<FluxUniqueTopicCountMetricModule>(FluxMetricCatalog.MeasureTopics);
        services.AddFluxMetricStreamModule<FluxPayloadBytesMetricModule>(FluxMetricCatalog.MeasurePayloadBytes);
        services.AddFluxMetricStreamModule<FluxAveragePayloadMetricModule>(FluxMetricCatalog.MeasureAveragePayload);
        services.AddFluxMetricStreamModule<FluxRetainedCountMetricModule>(FluxMetricCatalog.MeasureRetained);

        return services;
    }

    private static IServiceCollection AddFluxMetricStreamModule<TModule>(
        this IServiceCollection services,
        string measureId)
        where TModule : class, IFluxMetricStreamModule<double>
    {
        services.AddKeyedSingleton<IFluxMetricStreamModule<double>, TModule>(measureId);
        return services;
    }
}

internal abstract class FluxRuntimeEventNumberMetricModule(
    FluxMetricCatalog catalog,
    TimeProvider timeProvider)
    : IFluxMetricStreamModule<double>
{
    protected FluxMetricCatalog Catalog { get; } = catalog ?? FluxMetricCatalog.Shared;

    protected TimeProvider TimeProvider { get; } = timeProvider ?? TimeProvider.System;

    public abstract string MeasureId { get; }

    public abstract IFluxMetric<double> Build(FluxMetricStreamBuildContext<double> context);
}

internal sealed class FluxEventCountMetricModule(FluxMetricCatalog catalog, TimeProvider timeProvider)
    : FluxRuntimeEventNumberMetricModule(catalog, timeProvider)
{
    public override string MeasureId => FluxMetricCatalog.MeasureCount;

    public override IFluxMetric<double> Build(FluxMetricStreamBuildContext<double> context)
        => new FluxEventCountMetric(context.Id, context.Definition, context.Events, Catalog, context.BoundedCapacity, TimeProvider);
}

internal sealed class FluxEventRateMetricModule(FluxMetricCatalog catalog, TimeProvider timeProvider)
    : FluxRuntimeEventNumberMetricModule(catalog, timeProvider)
{
    public override string MeasureId => FluxMetricCatalog.MeasureRate;

    public override IFluxMetric<double> Build(FluxMetricStreamBuildContext<double> context)
        => new FluxEventRateMetric(context.Id, context.Definition, context.Events, Catalog, context.BoundedCapacity, TimeProvider);
}

internal sealed class FluxUniqueTopicCountMetricModule(FluxMetricCatalog catalog, TimeProvider timeProvider)
    : FluxRuntimeEventNumberMetricModule(catalog, timeProvider)
{
    public override string MeasureId => FluxMetricCatalog.MeasureTopics;

    public override IFluxMetric<double> Build(FluxMetricStreamBuildContext<double> context)
        => new FluxUniqueTopicCountMetric(context.Id, context.Definition, context.Events, Catalog, context.BoundedCapacity, TimeProvider);
}

internal sealed class FluxPayloadBytesMetricModule(FluxMetricCatalog catalog, TimeProvider timeProvider)
    : FluxRuntimeEventNumberMetricModule(catalog, timeProvider)
{
    public override string MeasureId => FluxMetricCatalog.MeasurePayloadBytes;

    public override IFluxMetric<double> Build(FluxMetricStreamBuildContext<double> context)
        => new FluxPayloadBytesMetric(context.Id, context.Definition, context.Events, Catalog, context.BoundedCapacity, TimeProvider);
}

internal sealed class FluxAveragePayloadMetricModule(FluxMetricCatalog catalog, TimeProvider timeProvider)
    : FluxRuntimeEventNumberMetricModule(catalog, timeProvider)
{
    public override string MeasureId => FluxMetricCatalog.MeasureAveragePayload;

    public override IFluxMetric<double> Build(FluxMetricStreamBuildContext<double> context)
        => new FluxAveragePayloadMetric(context.Id, context.Definition, context.Events, Catalog, context.BoundedCapacity, TimeProvider);
}

internal sealed class FluxRetainedCountMetricModule(FluxMetricCatalog catalog, TimeProvider timeProvider)
    : FluxRuntimeEventNumberMetricModule(catalog, timeProvider)
{
    public override string MeasureId => FluxMetricCatalog.MeasureRetained;

    public override IFluxMetric<double> Build(FluxMetricStreamBuildContext<double> context)
        => new FluxRetainedCountMetric(context.Id, context.Definition, context.Events, Catalog, context.BoundedCapacity, TimeProvider);
}
