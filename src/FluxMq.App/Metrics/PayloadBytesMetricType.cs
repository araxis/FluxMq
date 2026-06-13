using FluxFlow.Engine.Components;

namespace FluxMq.App.Metrics;

/// <summary>
/// Total payload bytes for the matching messages within a rolling window.
/// </summary>
public sealed class PayloadBytesMetricType : IFluxMetricType<double>
{
    public const string Id = "payload.bytes";

    public string TypeId => Id;

    public string DisplayName => "Payload bytes";

    public string Description => "Sums payload bytes for the matching messages.";

    public string Unit => "bytes";

    public string Format => MetricFormats.Bytes;

    public IReadOnlyList<FluxMetricParameterDescriptor> Parameters { get; } = EventFilterParameters.TopicFilters();

    public FluxMetricValidationResult Validate(FluxMetricResourceDefinition resource)
        => MetricResourceValidation.Validate(Parameters, resource);

    public string Summarize(FluxMetricResourceDefinition resource)
        => MetricResourceSummary.Describe(this, resource);

    public IFluxMetricSource<double> CreateSource(FluxMetricSourceContext context)
        => new PayloadBytesMetricSource(context);
}

internal sealed class PayloadBytesMetricSource(FluxMetricSourceContext context)
    : EventWindowMetricSource(
        context.MetricId,
        EventFilter.FromParameters(context.Parameters).Matches,
        MetricWindow.Parse(context.Parameters.GetValueOrDefault(MetricParameterKeys.Window)),
        context.Events,
        context.BoundedCapacity,
        context.TimeProvider)
{
    protected override double Calculate(IReadOnlyList<FlowEvent> window, DateTimeOffset now)
        => window.Sum(static flowEvent => (double)(flowEvent.PayloadBytes ?? 0));
}
