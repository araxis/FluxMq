using FluxMq.UI.Models;

namespace FluxMq.UI.Services;

public interface IFluxChartAdapter
{
    FluxChartSeries CreateBucketSeries(DashboardEventSnapshot snapshot);

    FluxChartSeries CreateTopicSeries(DashboardEventSnapshot snapshot, int take = 8);

    FluxChartSeries CreatePayloadDistributionSeries(DashboardEventSnapshot snapshot);

    FluxChartSeries CreateQosSeries(DashboardEventSnapshot snapshot);

    FluxChartSeries CreateRetainSeries(DashboardEventSnapshot snapshot);
}
