using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FluxMq.App.Metrics;

/// <summary>
/// Registers the metric types and the type registry. Adding a metric means adding one
/// <see cref="IFluxMetricType{TValue}"/> class and one line here.
/// </summary>
public static class FluxMetricTypeServiceCollectionExtensions
{
    public static IServiceCollection AddFluxMqMetricTypes(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddFluxMqMetricType<EventCountMetricType>();

        services.TryAddSingleton<IFluxMetricTypeRegistry, FluxMetricTypeRegistry>();
        return services;
    }

    private static IServiceCollection AddFluxMqMetricType<TType>(this IServiceCollection services)
        where TType : class, IFluxMetricType<double>
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IFluxMetricType<double>, TType>());
        return services;
    }
}
