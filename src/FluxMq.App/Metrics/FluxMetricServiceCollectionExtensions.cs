using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FluxMq.App.Metrics;

/// <summary>
/// Registers metric kinds and the catalog. Adding a metric is one new class plus its own
/// <c>Add&lt;Name&gt;Metric</c> call here — no central switch, no shared base class.
/// </summary>
public static class FluxMetricServiceCollectionExtensions
{
    /// <summary>Registers one metric kind (its descriptor + factory) and ensures the catalog is available.</summary>
    public static IServiceCollection AddFluxMetric(
        this IServiceCollection services,
        MetricDescriptor descriptor,
        Func<MetricSourceContext, IFluxMetricSource> factory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(factory);

        services.AddSingleton(new MetricRegistration(descriptor, factory));
        services.TryAddSingleton<IFluxMetricCatalog, FluxMetricCatalog>();
        return services;
    }

    /// <summary>Registers all built-in metric kinds.</summary>
    public static IServiceCollection AddFluxMetrics(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services
            .AddTopicCountMetric()
            .AddMessageCountMetric()
            .AddWindowedTopicCountMetric()
            .AddWindowedMessageCountMetric()
            .AddEventRateMetric()
            .AddPayloadBytesMetric()
            .AddRetainedCountMetric();
    }
}
