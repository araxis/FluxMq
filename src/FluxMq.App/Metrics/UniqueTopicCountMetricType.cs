using FluxFlow.Engine.Components;

namespace FluxMq.App.Metrics;

/// <summary>
/// Number of distinct topics represented by the matching traffic within a rolling window.
/// </summary>
public sealed class UniqueTopicCountMetricType : IFluxMetricType<double>
{
    public const string Id = "topic.unique-count";

    public string TypeId => Id;

    public string DisplayName => "Unique topics";

    public string Description => "Counts distinct topics represented by the matching traffic.";

    public string Unit => "topics";

    public string Format => MetricFormats.Number;

    public IReadOnlyList<FluxMetricParameterDescriptor> Parameters { get; } = EventFilterParameters.TopicFilters();

    public FluxMetricValidationResult Validate(FluxMetricResourceDefinition resource)
        => MetricResourceValidation.Validate(Parameters, resource);

    public string Summarize(FluxMetricResourceDefinition resource)
        => MetricResourceSummary.Describe(this, resource);

    public IFluxMetricSource<double> CreateSource(FluxMetricSourceContext context)
        => new UniqueTopicCountMetricSource(context);
}

internal sealed class UniqueTopicCountMetricSource(FluxMetricSourceContext context)
    : EventWindowMetricSource(
        context.MetricId,
        EventFilter.FromParameters(context.Parameters).Matches,
        MetricWindow.Parse(context.Parameters.GetValueOrDefault(MetricParameterKeys.Window)),
        context.Events,
        context.BoundedCapacity,
        context.TimeProvider)
{
    protected override double Calculate(IReadOnlyList<FlowEvent> window, DateTimeOffset now)
        => window
            .Where(static flowEvent => !string.IsNullOrWhiteSpace(flowEvent.Channel))
            .Select(static flowEvent => flowEvent.Channel!)
            .Distinct(StringComparer.Ordinal)
            .Count();
}
