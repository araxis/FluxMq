using Microsoft.Extensions.DependencyInjection;

namespace FluxMq.App.Metrics;

/// <summary>
/// Resolves the registered numeric metric types by id and enumerates them for the UI.
/// </summary>
public interface IFluxMetricTypeRegistry
{
    IReadOnlyList<IFluxMetricType<double>> NumberTypes { get; }

    bool TryGetNumberType(string typeId, out IFluxMetricType<double> type);

    IFluxMetricType<double> GetNumberType(string typeId);
}

public sealed class FluxMetricTypeRegistry : IFluxMetricTypeRegistry
{
    private readonly Dictionary<string, IFluxMetricType<double>> _byId = new(StringComparer.Ordinal);

    public FluxMetricTypeRegistry(IEnumerable<IFluxMetricType<double>> types)
    {
        ArgumentNullException.ThrowIfNull(types);

        NumberTypes = types.ToArray();
        foreach (var type in NumberTypes)
        {
            _byId[type.TypeId] = type;
        }
    }

    public IReadOnlyList<IFluxMetricType<double>> NumberTypes { get; }

    public bool TryGetNumberType(string typeId, out IFluxMetricType<double> type)
    {
        if (!string.IsNullOrWhiteSpace(typeId) && _byId.TryGetValue(typeId.Trim(), out var match))
        {
            type = match;
            return true;
        }

        type = default!;
        return false;
    }

    public IFluxMetricType<double> GetNumberType(string typeId)
        => TryGetNumberType(typeId, out var type)
            ? type
            : throw new InvalidOperationException($"Metric type '{typeId}' is not registered.");

    public static IFluxMetricTypeRegistry CreateDefault()
    {
        var services = new ServiceCollection();
        services.AddFluxMqMetricTypes();
        return services.BuildServiceProvider().GetRequiredService<IFluxMetricTypeRegistry>();
    }
}
