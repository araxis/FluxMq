using FluxMq.Core.Metrics;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.App.Metrics;

/// <summary>
/// A running metric instance. It subscribes to the runtime event stream and emits
/// <see cref="FluxMetricReading{TValue}"/> values through an <see cref="ISourceBlock{T}"/>.
/// Each source owns its own match predicate, window, and calculation.
/// </summary>
public interface IFluxMetricSource<TValue>
{
    string MetricId { get; }

    FluxMetricReading<TValue>? Latest { get; }

    ISourceBlock<FluxMetricReading<TValue>> Output { get; }

    Task StartAsync(CancellationToken cancellationToken = default);

    void Complete();

    Task Completion { get; }
}
