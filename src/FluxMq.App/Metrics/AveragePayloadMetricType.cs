using FluxFlow.Engine.Components;

namespace FluxMq.App.Metrics;

/// <summary>
/// Average payload size for the matching messages within a rolling window.
/// </summary>
public sealed class AveragePayloadMetricType : IFluxMetricType<double>
{
    public const string Id = "payload.average";

    public string TypeId => Id;

    public string DisplayName => "Average payload";

    public string Description => "Averages payload size for the matching messages.";

    public string Unit => "bytes";

    public string Format => MetricFormats.Bytes;

    public IReadOnlyList<FluxMetricParameterDescriptor> Parameters { get; } = EventFilterParameters.TopicFilters();

    public FluxMetricValidationResult Validate(FluxMetricResourceDefinition resource)
        => MetricResourceValidation.Validate(Parameters, resource);

    public string Summarize(FluxMetricResourceDefinition resource)
        => MetricResourceSummary.Describe(this, resource);

    public IFluxMetricSource<double> CreateSource(FluxMetricSourceContext context)
        => new AveragePayloadMetricSource(context);
}

internal sealed class AveragePayloadMetricSource(FluxMetricSourceContext context)
    : EventWindowMetricSource(
        context.MetricId,
        EventFilter.FromParameters(context.Parameters).Matches,
        MetricWindow.Parse(context.Parameters.GetValueOrDefault(MetricParameterKeys.Window)),
        context.Events,
        context.BoundedCapacity,
        context.TimeProvider)
{
    protected override double Calculate(IReadOnlyList<FlowEvent> window, DateTimeOffset now)
        => window.Count == 0
            ? 0
            : window.Sum(static flowEvent => (double)(flowEvent.PayloadBytes ?? 0)) / window.Count;
}
