using FluxMq.Core.Models;
using FluxMq.Pipeline.Components.MqttMetrics;

namespace FluxMq.UI.Services;

/// <summary>
/// Computes an <see cref="MqttMetricsSnapshot"/> from a sequence of envelopes,
/// plus a per-topic breakdown for the gorgeous live widget. Pure / no state —
/// the widget calls this on every Live.Changed tick.
/// </summary>
public static class LiveMetricsCalculator
{
    public static MqttMetricsSnapshot Compute(IReadOnlyList<MqttEnvelope> envelopes)
    {
        if (envelopes.Count == 0)
        {
            return new MqttMetricsSnapshot();
        }

        long totalBytes = 0;
        long retained = 0;
        long minBytes = long.MaxValue;
        long maxBytes = 0;
        var topics = new HashSet<string>(StringComparer.Ordinal);
        DateTimeOffset? latestAt = null;
        string? latestTopic = null;

        for (var i = 0; i < envelopes.Count; i++)
        {
            var envelope = envelopes[i];
            var size = envelope.Payload.LongLength;
            totalBytes += size;
            if (size < minBytes) minBytes = size;
            if (size > maxBytes) maxBytes = size;
            if (envelope.Retain) retained++;
            topics.Add(envelope.Topic);

            // The Live workspace inserts newest first; track the first one we see.
            if (latestAt is null || envelope.ReceivedAt > latestAt)
            {
                latestAt = envelope.ReceivedAt;
                latestTopic = envelope.Topic;
            }
        }

        return new MqttMetricsSnapshot
        {
            MessageCount = envelopes.Count,
            TotalPayloadBytes = totalBytes,
            MinPayloadBytes = minBytes == long.MaxValue ? 0 : minBytes,
            MaxPayloadBytes = maxBytes,
            RetainedMessageCount = retained,
            UniqueTopicCount = topics.Count,
            LastTopic = latestTopic,
            LastReceivedAt = latestAt
        };
    }

    /// <summary>Top N topics by message count, descending.</summary>
    public static IReadOnlyList<TopicCount> TopTopics(IReadOnlyList<MqttEnvelope> envelopes, int take = 3)
    {
        if (envelopes.Count == 0)
        {
            return [];
        }

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < envelopes.Count; i++)
        {
            var topic = envelopes[i].Topic;
            counts[topic] = counts.TryGetValue(topic, out var existing) ? existing + 1 : 1;
        }

        return counts
            .OrderByDescending(kv => kv.Value)
            .Take(take)
            .Select(kv => new TopicCount(kv.Key, kv.Value))
            .ToArray();
    }

    public readonly record struct TopicCount(string Topic, int Count);
}
