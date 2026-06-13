using FluxMq.Core.Metrics;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.App.Metrics;

/// <summary>
/// A running metric instance, independent of its value type. It subscribes to the runtime event
/// stream and produces readings; the host starts, completes, and awaits it uniformly. The concrete
/// value kind is declared on the metric's <see cref="MetricDescriptor"/>, not here.
/// </summary>
public interface IFluxMetricSource
{
    string MetricId { get; }

    Task StartAsync(CancellationToken cancellationToken = default);

    void Complete();

    Task Completion { get; }
}

/// <summary>
/// A running metric instance that emits typed <see cref="FluxMetricReading{TValue}"/> values through an
/// <see cref="ISourceBlock{T}"/>. Each metric class implements this directly and owns its own filter and
/// calculation — there is no shared base class.
/// </summary>
public interface IFluxMetricSource<TValue> : IFluxMetricSource
{
    FluxMetricReading<TValue>? Latest { get; }

    ISourceBlock<FluxMetricReading<TValue>> Output { get; }
}
