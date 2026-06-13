using FluxFlow.Engine.Components;

namespace FluxMq.App.Metrics;

/// <summary>
/// Matching runtime events per second over a rolling window.
/// </summary>
public sealed class EventRateMetricType : IFluxMetricType<double>
{
    public const string Id = "event.rate";

    public string TypeId => Id;

    public string DisplayName => "Event rate";

    public string Description => "Matching runtime events per second over a rolling window.";

    public string Unit => "/s";

    public string Format => MetricFormats.Number;

    public IReadOnlyList<FluxMetricParameterDescriptor> Parameters { get; } = EventFilterParameters.EventFilters();

    public FluxMetricValidationResult Validate(FluxMetricResourceDefinition resource)
        => MetricResourceValidation.Validate(Parameters, resource);

    public string Summarize(FluxMetricResourceDefinition resource)
        => MetricResourceSummary.Describe(this, resource);

    public IFluxMetricSource<double> CreateSource(FluxMetricSourceContext context)
        => new EventRateMetricSource(context);
}

internal sealed class EventRateMetricSource(FluxMetricSourceContext context)
    : EventWindowMetricSource(
        context.MetricId,
        EventFilter.FromParameters(context.Parameters).Matches,
        MetricWindow.Parse(context.Parameters.GetValueOrDefault(MetricParameterKeys.Window)),
        context.Events,
        context.BoundedCapacity,
        context.TimeProvider)
{
    protected override double Calculate(IReadOnlyList<FlowEvent> window, DateTimeOffset now)
        => window.Count / Window.TotalSeconds;
}
