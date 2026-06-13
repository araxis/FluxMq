using FluxFlow.Engine.Components;

namespace FluxMq.App.Metrics;

/// <summary>
/// Number of matching messages with the MQTT retain flag set within a rolling window.
/// </summary>
public sealed class RetainedCountMetricType : IFluxMetricType<double>
{
    public const string Id = "message.retained";

    public string TypeId => Id;

    public string DisplayName => "Retained messages";

    public string Description => "Counts matching messages with the retain flag set.";

    public string Unit => "messages";

    public string Format => MetricFormats.Number;

    public IReadOnlyList<FluxMetricParameterDescriptor> Parameters { get; } = EventFilterParameters.TopicFilters();

    public FluxMetricValidationResult Validate(FluxMetricResourceDefinition resource)
        => MetricResourceValidation.Validate(Parameters, resource);

    public string Summarize(FluxMetricResourceDefinition resource)
        => MetricResourceSummary.Describe(this, resource);

    public IFluxMetricSource<double> CreateSource(FluxMetricSourceContext context)
        => new RetainedCountMetricSource(context);
}

internal sealed class RetainedCountMetricSource(FluxMetricSourceContext context)
    : EventWindowMetricSource(
        context.MetricId,
        EventFilter.FromParameters(context.Parameters).Matches,
        MetricWindow.Parse(context.Parameters.GetValueOrDefault(MetricParameterKeys.Window)),
        context.Events,
        context.BoundedCapacity,
        context.TimeProvider)
{
    protected override double Calculate(IReadOnlyList<FlowEvent> window, DateTimeOffset now)
        => window.Count(static flowEvent =>
            bool.TryParse(flowEvent.GetAttribute("retain"), out var retained) && retained);
}
