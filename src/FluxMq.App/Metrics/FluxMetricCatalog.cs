using Microsoft.Extensions.DependencyInjection;

namespace FluxMq.App.Metrics;

/// <summary>One registered metric kind: how to describe it and how to create a running instance.</summary>
public sealed record MetricRegistration(
    MetricDescriptor Descriptor,
    Func<MetricSourceContext, IFluxMetricSource> Factory);

/// <summary>
/// The list of metric kinds the app can create from runtime events, and the factory to build a running
/// instance of one. Populated by the per-metric DI registrations; the UI lists <see cref="Descriptors"/>
/// to build its add-metric form.
/// </summary>
public interface IFluxMetricCatalog
{
    IReadOnlyList<MetricDescriptor> Descriptors { get; }

    bool TryGet(string typeId, out MetricRegistration registration);

    MetricDescriptor? Describe(string typeId);

    IFluxMetricSource Create(string typeId, MetricSourceContext context);
}

public sealed class FluxMetricCatalog : IFluxMetricCatalog
{
    private readonly Dictionary<string, MetricRegistration> _byId = new(StringComparer.Ordinal);

    public FluxMetricCatalog(IEnumerable<MetricRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        foreach (var registration in registrations)
        {
            _byId[registration.Descriptor.TypeId] = registration;
        }

        Descriptors = _byId.Values.Select(static registration => registration.Descriptor).ToArray();
    }

    public IReadOnlyList<MetricDescriptor> Descriptors { get; }

    public bool TryGet(string typeId, out MetricRegistration registration)
    {
        if (!string.IsNullOrWhiteSpace(typeId) && _byId.TryGetValue(typeId.Trim(), out var match))
        {
            registration = match;
            return true;
        }

        registration = default!;
        return false;
    }

    public MetricDescriptor? Describe(string typeId)
        => TryGet(typeId, out var registration) ? registration.Descriptor : null;

    public IFluxMetricSource Create(string typeId, MetricSourceContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return TryGet(typeId, out var registration)
            ? registration.Factory(context)
            : throw new InvalidOperationException($"Metric type '{typeId}' is not registered.");
    }

    // The default catalog is immutable after construction and its factories are static (they capture no provider),
    // so a single shared instance is safe to reuse. Built once to avoid spinning up and discarding a ServiceProvider
    // on every call (the default ctors of the dashboard registry and validator both ask for it).
    private static readonly IFluxMetricCatalog SharedDefault = BuildDefault();

    public static IFluxMetricCatalog CreateDefault() => SharedDefault;

    private static IFluxMetricCatalog BuildDefault()
    {
        var services = new ServiceCollection();
        services.AddFluxMetrics();
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IFluxMetricCatalog>();
    }
}
