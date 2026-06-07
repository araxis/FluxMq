using FluxMq.Core.Metrics;
using FluxFlow.Engine.Mapping;

namespace FluxMq.Components.Mapping;

public sealed class MetricReadingFlowMapContextFactory : IFlowMapContextFactory<FluxMetricReading<double>>
{
    public FlowMapContext Create(FluxMetricReading<double> input)
        => new()
        {
            Variables = new Dictionary<string, object?>
            {
                ["input"] = input,
                ["reading"] = input,
                ["metricId"] = input.MetricId,
                ["timestamp"] = input.Timestamp,
                ["value"] = input.Value
            }
        };
}
