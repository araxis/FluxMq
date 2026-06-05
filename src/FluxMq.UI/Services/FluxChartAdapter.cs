using FluxMq.UI.Models;
using System.Text;

namespace FluxMq.UI.Services;

public sealed class FluxChartAdapter : IFluxChartAdapter
{
    public FluxChartSeries CreateBucketSeries(DashboardEventSnapshot snapshot)
        => new(
            "Events",
            snapshot.BucketCounts.Select(static count => (double)count).ToArray(),
            Enumerable.Range(1, snapshot.BucketCounts.Count)
                .Select(static index => index.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .ToArray());

    public FluxChartSeries CreateTopicSeries(DashboardEventSnapshot snapshot, int take = 8)
    {
        var topics = snapshot.TopicCounts.Take(Math.Max(1, take)).ToArray();
        return new FluxChartSeries(
            "Topics",
            topics.Select(static topic => (double)topic.Count).ToArray(),
            topics.Select(static topic => topic.Topic).ToArray());
    }

    public FluxChartSeries CreatePayloadDistributionSeries(DashboardEventSnapshot snapshot)
    {
        var buckets = new[] { "0-256 B", "257 B-1 KB", "1-8 KB", ">8 KB" };
        var counts = new double[buckets.Length];
        foreach (var flowEvent in snapshot.Events)
        {
            var bytes = Encoding.UTF8.GetByteCount(flowEvent.PayloadPreview ?? string.Empty);
            var index = bytes switch
            {
                <= 256 => 0,
                <= 1024 => 1,
                <= 8192 => 2,
                _ => 3
            };
            counts[index]++;
        }

        return new FluxChartSeries("Payload size", counts, buckets);
    }

    public FluxChartSeries CreateQosSeries(DashboardEventSnapshot snapshot)
    {
        var groups = snapshot.Events
            .GroupBy(static flowEvent => flowEvent.GetAttribute("qos") ?? "unknown", StringComparer.Ordinal)
            .OrderBy(static group => group.Key, StringComparer.Ordinal)
            .ToArray();

        return new FluxChartSeries(
            "QoS",
            groups.Select(static group => (double)group.Count()).ToArray(),
            groups.Select(static group => group.Key).ToArray());
    }

    public FluxChartSeries CreateRetainSeries(DashboardEventSnapshot snapshot)
    {
        var retained = snapshot.Events.Count(static flowEvent =>
            bool.TryParse(flowEvent.GetAttribute("retain"), out var value) && value);
        var notRetained = Math.Max(0, snapshot.Events.Count - retained);

        return new FluxChartSeries(
            "Retain",
            [retained, notRetained],
            ["Retained", "Not retained"]);
    }
}
