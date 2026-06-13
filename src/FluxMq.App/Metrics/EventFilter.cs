using FluxFlow.Engine.Components;

namespace FluxMq.App.Metrics;

/// <summary>
/// Builds the runtime-event match predicate shared by event-based metric types. This is the single home for
/// <see cref="FlowEvent"/> field knowledge (type, topic, status, qos, retain) that previously lived in the
/// central metric catalog. Each metric source composes one of these from its resolved parameters.
/// </summary>
internal sealed class EventFilter
{
    private readonly string? _eventType;
    private readonly string? _topicStartsWith;
    private readonly string? _topicNotStartsWith;
    private readonly string? _status;
    private readonly string? _qos;
    private readonly string? _retain;

    private EventFilter(
        string? eventType,
        string? topicStartsWith,
        string? topicNotStartsWith,
        string? status,
        string? qos,
        string? retain)
    {
        _eventType = eventType;
        _topicStartsWith = topicStartsWith;
        _topicNotStartsWith = topicNotStartsWith;
        _status = status;
        _qos = qos;
        _retain = retain;
    }

    public static EventFilter FromParameters(IReadOnlyDictionary<string, string> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        return new EventFilter(
            Read(parameters, MetricParameterKeys.EventType),
            Read(parameters, MetricParameterKeys.TopicStartsWith),
            Read(parameters, MetricParameterKeys.TopicNotStartsWith),
            Read(parameters, MetricParameterKeys.Status),
            Read(parameters, MetricParameterKeys.Qos),
            Read(parameters, MetricParameterKeys.Retain));
    }

    public bool Matches(FlowEvent flowEvent)
    {
        ArgumentNullException.ThrowIfNull(flowEvent);

        if (_eventType is not null && !string.Equals(flowEvent.Type, _eventType, StringComparison.Ordinal))
        {
            return false;
        }

        if (_topicStartsWith is not null && !StartsWith(flowEvent.Channel, _topicStartsWith))
        {
            return false;
        }

        if (_topicNotStartsWith is not null && StartsWith(flowEvent.Channel, _topicNotStartsWith))
        {
            return false;
        }

        if (_status is not null && !string.Equals(flowEvent.Status, _status, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (_qos is not null && !AttributeMatches(flowEvent.GetAttribute("qos"), _qos))
        {
            return false;
        }

        return _retain is null || AttributeMatches(flowEvent.GetAttribute("retain"), _retain);
    }

    private static string? Read(IReadOnlyDictionary<string, string> parameters, string key)
        => parameters.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;

    private static bool StartsWith(string? actual, string expectedPrefix)
        => !string.IsNullOrWhiteSpace(actual) && actual.StartsWith(expectedPrefix, StringComparison.Ordinal);

    private static bool AttributeMatches(string? actual, string expected)
    {
        if (bool.TryParse(actual, out var actualBoolean) && bool.TryParse(expected, out var expectedBoolean))
        {
            return actualBoolean == expectedBoolean;
        }

        return !string.IsNullOrWhiteSpace(actual) && actual.StartsWith(expected, StringComparison.Ordinal);
    }
}
