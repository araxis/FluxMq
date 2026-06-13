namespace FluxMq.App.Metrics;

/// <summary>
/// Matches MQTT topic names against a topic filter using standard wildcards: <c>+</c> matches exactly one
/// level and <c>#</c> matches the remaining levels. A blank filter (or <c>#</c>) matches every topic.
/// Small and self-contained — a metric composes one from its topic parameter.
/// </summary>
public sealed class TopicFilter
{
    private readonly string[] _levels;
    private readonly bool _matchAll;

    private TopicFilter(string[] levels, bool matchAll)
    {
        _levels = levels;
        _matchAll = matchAll;
    }

    public static TopicFilter Parse(string? filter)
    {
        var trimmed = filter?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed == "#")
        {
            return new TopicFilter([], matchAll: true);
        }

        return new TopicFilter(trimmed.Split('/'), matchAll: false);
    }

    public bool Matches(string? topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            return false;
        }

        if (_matchAll)
        {
            return true;
        }

        var levels = topic.Split('/');
        for (var i = 0; i < _levels.Length; i++)
        {
            var filterLevel = _levels[i];
            if (filterLevel == "#")
            {
                return true;
            }

            if (i >= levels.Length)
            {
                return false;
            }

            if (filterLevel == "+")
            {
                continue;
            }

            if (!string.Equals(filterLevel, levels[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return _levels.Length == levels.Length;
    }
}
