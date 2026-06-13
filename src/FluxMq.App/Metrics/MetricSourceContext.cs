using FluxFlow.Engine.Components;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.App.Metrics;

/// <summary>
/// Everything a metric needs to start a running instance: its id, the stored parameter values, the shared
/// runtime event stream, the bounded buffer size, and the time source. A metric's static <c>Create</c>
/// turns this into a configured <see cref="IFluxMetricSource"/>.
/// </summary>
public sealed record MetricSourceContext(
    string MetricId,
    IReadOnlyDictionary<string, string> Parameters,
    ISourceBlock<FlowEvent> Events,
    int BoundedCapacity = 256,
    TimeProvider? TimeProvider = null)
{
    public TimeProvider Clock => TimeProvider ?? System.TimeProvider.System;
}
