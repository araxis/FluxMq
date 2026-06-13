using FluxFlow.Engine.Components;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.App.Metrics;

/// <summary>
/// Everything a metric source needs to start: the resolved (resource + override) parameter values, the shared
/// runtime event stream, the bounded capacity, and the time provider chosen by the host.
/// </summary>
public sealed record FluxMetricSourceContext(
    string MetricId,
    IReadOnlyDictionary<string, string> Parameters,
    ISourceBlock<FlowEvent> Events,
    int BoundedCapacity,
    TimeProvider TimeProvider);

/// <summary>
/// A focused metric type — one class per metric. It owns its type id, display metadata, parameter rows,
/// validation, summary text, and the creation of its runtime source. Types are resolved from DI by
/// <see cref="TypeId"/>; adding a metric means adding one class and registering it.
/// </summary>
public interface IFluxMetricType<TValue>
{
    string TypeId { get; }

    string DisplayName { get; }

    string Description { get; }

    /// <summary>The natural unit of the produced reading, for example "events", "/s", or "bytes".</summary>
    string Unit { get; }

    /// <summary>The default value format (see <see cref="MetricFormats"/>).</summary>
    string Format { get; }

    IReadOnlyList<FluxMetricParameterDescriptor> Parameters { get; }

    FluxMetricValidationResult Validate(FluxMetricResourceDefinition resource);

    string Summarize(FluxMetricResourceDefinition resource);

    IFluxMetricSource<TValue> CreateSource(FluxMetricSourceContext context);
}
