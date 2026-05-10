namespace FluxMq.Pipeline.Components;

/// <summary>
/// MQTT 5 topic-filter matcher (single + / multi # wildcards).
/// Used by triggers that share a session: each trigger filters
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
        if (string.IsNullOrEmpty(topicFilter) || string.IsNullOrEmpty(topicName))
        {
            return false;
        }

        // $-prefixed topics may only be matched by filters that explicitly start with '$'.
        if (topicName[0] == '$' && topicFilter[0] != '$')
        {
            return false;
        }

        var filterSegments = topicFilter.Split('/');
        var topicSegments = topicName.Split('/');

        for (var i = 0; i < filterSegments.Length; i++)
        {
            var segment = filterSegments[i];

            if (segment == "#")
            {
                // '#' must be the final segment and matches the rest of the topic.
                return i == filterSegments.Length - 1;
            }

            if (i >= topicSegments.Length)
            {
                return false;
            }

            if (segment == "+")
            {
                // '+' matches exactly one segment.
                continue;
            }

            if (!string.Equals(segment, topicSegments[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return filterSegments.Length == topicSegments.Length;
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
