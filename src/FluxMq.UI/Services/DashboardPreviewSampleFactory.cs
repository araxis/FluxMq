using FluxFlow.Engine.Components;
using FluxMq.Components;
using FluxMq.Core.Models;
using FluxMq.UI.Models;
using MQTTnet.Protocol;
using System.Text;

namespace FluxMq.UI.Services;

public sealed record DashboardPreviewSample(
    DashboardEventSnapshot Snapshot,
    IReadOnlyList<MqttEnvelope> TopicMessages);

public static class DashboardPreviewSampleFactory
{
    private static readonly string[] TopicSegments =
    [
        "temperature",
        "pressure",
        "humidity",
        "speed",
        "vibration",
        "energy",
        "status",
        "alarm"
    ];

    public static DashboardPreviewSample Create(DashboardWidgetSnapshot widget)
    {
        ArgumentNullException.ThrowIfNull(widget);

        var random = Random.Shared;
        var now = DateTimeOffset.UtcNow;
        var eventType = widget.ReadString(DashboardEventFilterCatalog.EventTypeKey) ??
            DefaultEventType(widget.Type);
        var status = widget.ReadString(DashboardEventFilterCatalog.StatusKey) ??
            DefaultStatus(eventType);
        var topicPrefix = NormalizeTopicPrefix(widget.ReadString(DashboardEventFilterCatalog.TopicStartsWithKey));
        var qos = NormalizeQos(widget.ReadString(DashboardEventFilterCatalog.AttributeFilterKey("qos")));
        var retain = NormalizeRetain(widget.ReadString(DashboardEventFilterCatalog.AttributeFilterKey("retain")));
        var sampleCount = random.Next(34, 86);

        var events = Enumerable.Range(0, sampleCount)
            .Select(index => CreateEvent(random, now, eventType, status, topicPrefix, qos, retain, index))
            .OrderBy(static flowEvent => flowEvent.Timestamp)
            .ToArray();
        var topicCounts = events
            .GroupBy(static flowEvent => flowEvent.Channel ?? flowEvent.Subject ?? "unknown", StringComparer.Ordinal)
            .Select(static group => new DashboardTopicMetric(group.Key, group.Count()))
            .OrderByDescending(static topic => topic.Count)
            .ThenBy(static topic => topic.Topic, StringComparer.Ordinal)
            .ToArray();
        var bucketCounts = BuildBuckets(events, now, TimeSpan.FromMinutes(1), 12);
        var recentCount = bucketCounts.Sum();
        var snapshot = new DashboardEventSnapshot(
            events.Length,
            events.LastOrDefault(),
            recentCount,
            TimeSpan.FromMinutes(1),
            recentCount / 60d,
            events.Reverse().Take(12).ToArray(),
            bucketCounts,
            topicCounts,
            events.Sum(static flowEvent => (long)(flowEvent.PayloadBytes ?? 0)),
            topicCounts.Length,
            events.Count(static flowEvent => string.Equals(flowEvent.GetAttribute("retain"), bool.TrueString, StringComparison.OrdinalIgnoreCase)));

        return new DashboardPreviewSample(
            snapshot,
            events
                .Reverse()
                .Take(80)
                .Select(ToEnvelope)
                .ToArray());
    }

    private static FlowEvent CreateEvent(
        Random random,
        DateTimeOffset now,
        string eventType,
        string status,
        string topicPrefix,
        int qos,
        bool retain,
        int index)
    {
        var topic = $"{topicPrefix}{TopicSegments[index % TopicSegments.Length]}/{random.Next(1, 6)}";
        var payloadBytes = random.Next(52, 8_600);
        return new FlowEvent
        {
            Timestamp = now.AddSeconds(-random.Next(0, 60)),
            Type = eventType,
            Source = "Dashboard preview",
            Subject = topic,
            Status = status,
            Channel = topic,
            PayloadBytes = payloadBytes,
            PayloadPreview = CreatePayloadPreview(topic, payloadBytes, random),
            Attributes = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["qos"] = qos.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["retain"] = retain.ToString()
            }
        };
    }

    private static IReadOnlyList<int> BuildBuckets(
        IReadOnlyList<FlowEvent> events,
        DateTimeOffset now,
        TimeSpan window,
        int bucketCount)
    {
        var buckets = new int[bucketCount];
        var windowStart = now.Subtract(window);
        var bucketSeconds = window.TotalSeconds / bucketCount;

        foreach (var flowEvent in events)
        {
            if (flowEvent.Timestamp < windowStart)
            {
                continue;
            }

            var elapsedSeconds = Math.Clamp((flowEvent.Timestamp - windowStart).TotalSeconds, 0, window.TotalSeconds - 0.001);
            var bucketIndex = Math.Clamp((int)(elapsedSeconds / bucketSeconds), 0, bucketCount - 1);
            buckets[bucketIndex]++;
        }

        return buckets;
    }

    private static MqttEnvelope ToEnvelope(FlowEvent flowEvent)
        => new()
        {
            Topic = flowEvent.Channel ?? flowEvent.Subject ?? "preview/topic",
            Payload = Encoding.UTF8.GetBytes(flowEvent.PayloadPreview ?? "{}"),
            ReceivedAt = flowEvent.Timestamp,
            QualityOfService = NormalizeQosLevel(flowEvent.GetAttribute("qos")),
            Retain = string.Equals(flowEvent.GetAttribute("retain"), bool.TrueString, StringComparison.OrdinalIgnoreCase)
        };

    private static string DefaultEventType(string widgetType)
        => widgetType switch
        {
            DashboardWidgetCatalog.PayloadDistributionType => FluxMqEventTypes.MqttMessageReceived,
            DashboardWidgetCatalog.QosRetainBreakdownType or
            DashboardWidgetCatalog.QosBreakdownType or
            DashboardWidgetCatalog.RetainBreakdownType => FluxMqEventTypes.MqttMessagePublished,
            DashboardWidgetCatalog.TopicTreeType or DashboardWidgetCatalog.TopicActivityType => FluxMqEventTypes.MqttMessageReceived,
            _ => FluxMqEventTypes.MqttMessagePublished
        };

    private static string DefaultStatus(string eventType)
        => eventType switch
        {
            FluxMqEventTypes.MqttMessageReceived => "received",
            FluxMqEventTypes.MqttMessageRecorded => "recorded",
            FluxMqEventTypes.FileWritten => "written",
            FluxMqEventTypes.JsonSchemaValidated => "valid",
            FluxMqEventTypes.AssertionEvaluated => "passed",
            _ => "published"
        };

    private static string NormalizeTopicPrefix(string? value)
    {
        var prefix = string.IsNullOrWhiteSpace(value)
            ? "factory/line-a/"
            : value.Trim();
        return prefix.EndsWith("/", StringComparison.Ordinal) ? prefix : prefix + "/";
    }

    private static int NormalizeQos(string? value)
        => int.TryParse(value, out var qos)
            ? Math.Clamp(qos, 0, 2)
            : Random.Shared.Next(0, 3);

    private static bool NormalizeRetain(string? value)
        => bool.TryParse(value, out var retain)
            ? retain
            : Random.Shared.NextDouble() > 0.72;

    private static MqttQualityOfServiceLevel NormalizeQosLevel(string? value)
        => NormalizeQos(value) switch
        {
            2 => MqttQualityOfServiceLevel.ExactlyOnce,
            1 => MqttQualityOfServiceLevel.AtLeastOnce,
            _ => MqttQualityOfServiceLevel.AtMostOnce
        };

    private static string CreatePayloadPreview(string topic, int payloadBytes, Random random)
        => $$"""{"topic":"{{topic}}","value":{{random.Next(18, 98)}},"bytes":{{payloadBytes}}}""";
}
