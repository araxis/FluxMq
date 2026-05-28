using MQTTnet;

namespace FluxMq.Components.MessageSource;

/// <summary>
/// MQTT 5 topic-filter matcher (single + / multi # wildcards).
/// Used by triggers that share a client: each trigger filters
/// the broker stream down to its own subscription set so only
/// matching envelopes flow into its output.
/// </summary>
public static class MqttTopicFilterMatcher
{
    /// <summary>
    /// Returns true if <paramref name="topicName"/> matches <paramref name="topicFilter"/>
    /// per MQTT 5 §4.7. Topic names beginning with '$' do not match a filter
    /// that starts with a wildcard segment.
    /// </summary>
    public static bool IsMatch(string topicFilter, string topicName)
    {
        var result = MqttTopicFilterComparer.Compare(topicName, topicFilter);
        return result == MqttTopicFilterCompareResult.IsMatch;
    }

    /// <summary>True if any of the given filters matches the topic.</summary>
    public static bool MatchesAny(IReadOnlyList<string> topicFilters, string topicName)
    {
        for (var i = 0; i < topicFilters.Count; i++)
        {
            if (IsMatch(topicFilters[i], topicName))
            {
                return true;
            }
        }
        return false;
    }
}
