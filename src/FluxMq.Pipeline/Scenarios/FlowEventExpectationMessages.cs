using FluxMq.Pipeline.Components;

namespace FluxMq.Pipeline.Scenarios;

internal static class FlowEventExpectationMessages
{
    public static string DescribeExpectTimeout(
        FlowEventExpectation expectation,
        IReadOnlyList<FlowEvent> observedEvents)
    {
        var guidance =
            "Finished runs do not keep listening; rerun the test and produce a matching app runtime event before the timeout.";

        if (string.Equals(expectation.EventType, FlowEventTypes.MqttMessagePublished, StringComparison.Ordinal))
        {
            guidance += " A mqtt.message.published expectation must be emitted by a running app MQTT publisher node.";
        }

        return $"Expected event was not observed within {expectation.Timeout.TotalMilliseconds:0} ms ({DescribeCriteria(expectation)}). {DescribeObservedEvents(observedEvents)} {guidance}";
    }

    public static string DescribeWhenSkipped(
        FlowEventExpectation expectation,
        IReadOnlyList<FlowEvent> observedEvents)
        => $"When condition was not met within {expectation.Timeout.TotalMilliseconds:0} ms ({DescribeCriteria(expectation)}). Remaining steps were skipped. {DescribeObservedEvents(observedEvents)}";

    private static string DescribeCriteria(FlowEventExpectation expectation)
    {
        var parts = new List<string>();
        Add(parts, "type", expectation.EventType);
        Add(parts, "topic", expectation.TopicStartsWith);
        Add(parts, "subject", expectation.SubjectStartsWith);
        Add(parts, "status", expectation.Status);
        Add(parts, "source", expectation.Source);
        Add(parts, "payload", expectation.PayloadContains);

        foreach (var attribute in expectation.Attributes)
        {
            Add(parts, $"attribute {attribute.Key}", attribute.Value);
        }

        return parts.Count == 0
            ? "any event"
            : string.Join(", ", parts);
    }

    private static string DescribeObservedEvents(IReadOnlyList<FlowEvent> observedEvents)
        => observedEvents.Count == 0
            ? "Observed 0 app runtime events while waiting."
            : $"Observed while waiting: {string.Join("; ", observedEvents.Select(DescribeObservedEvent))}.";

    private static string DescribeObservedEvent(FlowEvent flowEvent)
    {
        var parts = new List<string> { flowEvent.Type };
        Add(parts, "topic", flowEvent.Topic);
        Add(parts, "status", flowEvent.Status);
        Add(parts, "source", flowEvent.Source);

        if (!string.IsNullOrWhiteSpace(flowEvent.PayloadPreview))
        {
            Add(parts, "payload", Shorten(flowEvent.PayloadPreview));
        }

        return string.Join(", ", parts);
    }

    private static void Add(List<string> parts, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parts.Add($"{label} '{value}'");
        }
    }

    private static string Shorten(string value)
        => value.Length <= 80 ? value : $"{value[..77]}...";
}
