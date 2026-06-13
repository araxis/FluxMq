using FluxFlow.Engine.Components;

namespace FluxMq.App.Metrics;

/// <summary>
/// Counts matching runtime events within a rolling window.
/// </summary>
public sealed class EventCountMetricType : IFluxMetricType<double>
{
    public const string Id = "event.count";

    public string TypeId => Id;

    public string DisplayName => "Event count";

    public string Description => "Counts matching runtime events within a rolling window.";

    public string Unit => "events";

    public string Format => MetricFormats.Number;

    public IReadOnlyList<FluxMetricParameterDescriptor> Parameters { get; } = EventFilterParameters.EventFilters();

    public FluxMetricValidationResult Validate(FluxMetricResourceDefinition resource)
        => MetricResourceValidation.Validate(Parameters, resource);

    public string Summarize(FluxMetricResourceDefinition resource)
        => MetricResourceSummary.Describe(this, resource);

    public IFluxMetricSource<double> CreateSource(FluxMetricSourceContext context)
        => new EventCountMetricSource(context);
}

internal sealed class EventCountMetricSource(FluxMetricSourceContext context)
    : EventWindowMetricSource(
        context.MetricId,
        EventFilter.FromParameters(context.Parameters).Matches,
        MetricWindow.Parse(context.Parameters.GetValueOrDefault(MetricParameterKeys.Window)),
        context.Events,
        context.BoundedCapacity,
        context.TimeProvider)
{
    protected override double Calculate(IReadOnlyList<FlowEvent> window, DateTimeOffset now)
        => window.Count;
}
