using FluxFlow.Engine.Components;
using FluxMq.Scenarios;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace FluxMq.App.Metrics;

public sealed class DashboardRuntimeMetrics : IDisposable
{
    public const string MeterName = "fluxmq.runtime";
    public const string MeterVersion = "1.0.0";

    private readonly Meter _meter;
    private readonly bool _ownsMeter;
    private readonly Counter<long> _runtimeEvents;
    private readonly Counter<long> _mqttMessages;
    private readonly Histogram<long> _payloadBytes;
    private readonly Counter<long> _assertions;

    public DashboardRuntimeMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        _meter = meterFactory.Create(MeterName, MeterVersion);
        (_runtimeEvents, _mqttMessages, _payloadBytes, _assertions) = CreateInstruments(_meter);
    }

    public DashboardRuntimeMetrics()
    {
        _meter = new Meter(MeterName, MeterVersion);
        _ownsMeter = true;
        (_runtimeEvents, _mqttMessages, _payloadBytes, _assertions) = CreateInstruments(_meter);
    }

    public void Record(FlowEvent flowEvent)
    {
        ArgumentNullException.ThrowIfNull(flowEvent);

        var tags = EventTags(flowEvent);
        _runtimeEvents.Add(1, tags);

        if (IsMqttEvent(flowEvent.Type))
        {
            _mqttMessages.Add(1, tags);
        }

        if (flowEvent.PayloadBytes is int payloadBytes && payloadBytes > 0)
        {
            _payloadBytes.Record(payloadBytes, tags);
        }

        if (string.Equals(flowEvent.Type, FlowEventTypes.AssertionEvaluated, StringComparison.Ordinal))
        {
            _assertions.Add(1, tags);
        }
    }

    public void Dispose()
    {
        if (_ownsMeter)
        {
            _meter.Dispose();
        }
    }

    private static (
        Counter<long> RuntimeEvents,
        Counter<long> MqttMessages,
        Histogram<long> PayloadBytes,
        Counter<long> Assertions) CreateInstruments(Meter meter)
        => (
            meter.CreateCounter<long>(
                "fluxmq.runtime.events",
                unit: "{event}",
                description: "Runtime events accepted by FluxMQ."),
            meter.CreateCounter<long>(
                "fluxmq.mqtt.messages",
                unit: "{message}",
                description: "MQTT message events accepted by FluxMQ."),
            meter.CreateHistogram<long>(
                "fluxmq.payload.bytes",
                unit: "By",
                description: "Payload byte sizes observed in runtime events."),
            meter.CreateCounter<long>(
                "fluxmq.assertions",
                unit: "{assertion}",
                description: "Assertion result events accepted by FluxMQ."));

    private static TagList EventTags(FlowEvent flowEvent)
    {
        var tags = new TagList
        {
            { "event.type", NormalizeTag(flowEvent.Type, "unknown") },
            { "status", NormalizeTag(flowEvent.Status, "unknown") },
            { "mqtt.direction", MqttDirection(flowEvent.Type) },
            { "qos", NormalizeQos(flowEvent.GetAttribute("qos")) },
            { "retain", NormalizeRetain(flowEvent.GetAttribute("retain")) },
            { "topic.root", TopicRoot(flowEvent.Channel) }
        };
        return tags;
    }

    private static bool IsMqttEvent(string? eventType)
        => string.Equals(eventType, FlowEventTypes.MqttMessageReceived, StringComparison.Ordinal) ||
           string.Equals(eventType, FlowEventTypes.MqttMessagePublished, StringComparison.Ordinal) ||
           string.Equals(eventType, FlowEventTypes.MqttMessageRecorded, StringComparison.Ordinal);

    private static string MqttDirection(string? eventType)
        => eventType switch
        {
            FlowEventTypes.MqttMessageReceived => "received",
            FlowEventTypes.MqttMessagePublished => "published",
            FlowEventTypes.MqttMessageRecorded => "recorded",
            _ => "none"
        };

    private static string NormalizeQos(string? qos)
        => qos is "0" or "1" or "2" ? qos : "none";

    private static string NormalizeRetain(string? retain)
        => bool.TryParse(retain, out var retained)
            ? retained ? "true" : "false"
            : "none";

    private static string TopicRoot(string? topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            return "none";
        }

        var trimmed = topic.Trim();
        if (trimmed.StartsWith("$SYS/", StringComparison.Ordinal) ||
            string.Equals(trimmed, "$SYS", StringComparison.Ordinal))
        {
            return "$SYS";
        }

        var slash = trimmed.IndexOf('/');
        var root = slash <= 0 ? trimmed : trimmed[..slash];
        return NormalizeTag(root, "unknown");
    }

    private static string NormalizeTag(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= 48 ? trimmed : trimmed[..48];
    }
}
