using FluxFlow.Engine.Components;
using FluxMq.App.Metrics;
using FluxMq.Scenarios;
using Shouldly;
using System.Diagnostics.Metrics;

namespace FluxMq.App.Tests.Runtime;

public sealed class FluxMetricFrameworkTests
{
    [Fact]
    public void FluxMetricNaming_CreatesStableDashboardScopedMetricIds()
    {
        FluxMetricNaming.ToDashboardScopedId("ops", "messageCount")
            .ShouldBe("ops.messageCount");
        FluxMetricNaming.ToDashboardScopedId("ops", "ops.messageCount")
            .ShouldBe("ops.messageCount");
        FluxMetricNaming.ToDashboardScopedId("Ops Dashboard", "message count")
            .ShouldBe("Ops-Dashboard.message-count");

        FluxMetricNaming.RemoveDashboardScope("ops", "ops.messageCount")
            .ShouldBe("messageCount");
        FluxMetricNaming.RemoveDashboardScope("ops", "messageCount")
            .ShouldBe("messageCount");
        FluxMetricNaming.HasDashboardScope("ops", "ops.messageCount")
            .ShouldBeTrue();
    }

    [Fact]
    public void DashboardRuntimeMetrics_EmitsLowCardinalityDiagnosticMeasurements()
    {
        var measurements = new List<RecordedMeasurement>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (string.Equals(instrument.Meter.Name, DashboardRuntimeMetrics.MeterName, StringComparison.Ordinal))
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            measurements.Add(new RecordedMeasurement(
                instrument.Name,
                measurement,
                tags.ToArray()));
        });
        listener.Start();

        using var metrics = new DashboardRuntimeMetrics();
        metrics.Record(Event(
            FlowEventTypes.MqttMessagePublished,
            DateTimeOffset.Parse("2026-06-07T12:00:00Z"),
            "factory/line-a/device-1",
            status: "published",
            payloadBytes: 128,
            attributes: new Dictionary<string, string>
            {
                ["qos"] = "1",
                ["retain"] = "true"
            }));

        measurements.Select(static item => item.InstrumentName).ShouldContain("fluxmq.runtime.events");
        measurements.Select(static item => item.InstrumentName).ShouldContain("fluxmq.mqtt.messages");
        measurements.Select(static item => item.InstrumentName).ShouldContain("fluxmq.payload.bytes");
        measurements
            .SelectMany(static item => item.Tags)
            .Select(static tag => tag.Value?.ToString() ?? string.Empty)
            .ShouldNotContain("factory/line-a/device-1");

        var mqttMeasurement = measurements.Single(item => item.InstrumentName == "fluxmq.mqtt.messages");
        mqttMeasurement.Value.ShouldBe(1);
        mqttMeasurement.Tags.ShouldContain(tag => tag.Key == "topic.root" && Equals(tag.Value, "factory"));
        mqttMeasurement.Tags.ShouldContain(tag => tag.Key == "mqtt.direction" && Equals(tag.Value, "published"));
        mqttMeasurement.Tags.ShouldContain(tag => tag.Key == "qos" && Equals(tag.Value, "1"));
        mqttMeasurement.Tags.ShouldContain(tag => tag.Key == "retain" && Equals(tag.Value, "true"));
    }

    private static FlowEvent Event(
        string type,
        DateTimeOffset timestamp,
        string? topic = null,
        string? subject = null,
        string? status = null,
        int? payloadBytes = null,
        IReadOnlyDictionary<string, string>? attributes = null)
        => new()
        {
            Timestamp = timestamp,
            Type = type,
            Source = "test",
            Channel = topic,
            Subject = subject,
            Status = status,
            PayloadBytes = payloadBytes,
            Attributes = attributes ?? new Dictionary<string, string>(StringComparer.Ordinal)
        };

    private sealed record RecordedMeasurement(
        string InstrumentName,
        long Value,
        KeyValuePair<string, object?>[] Tags);
}
