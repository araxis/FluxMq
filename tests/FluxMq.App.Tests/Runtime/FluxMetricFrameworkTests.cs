using FluxFlow.Engine.Components;
using FluxMq.App.Metrics;
using FluxMq.Core.Metrics;
using FluxMq.Scenarios;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System.Diagnostics.Metrics;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.App.Tests.Runtime;

public sealed class FluxMetricFrameworkTests
{
    [Fact]
    public void FluxMetricCatalog_ProvidesReusableMetricDefaults()
    {
        var catalog = FluxMetricCatalog.Shared;

        catalog.Sources.Select(static source => source.Id).ShouldContain(FluxMetricCatalog.RuntimeEventsSource);
        catalog.Measures.Select(static measure => measure.Id).ShouldContain(FluxMetricCatalog.MeasureCount);
        catalog.Measures.Select(static measure => measure.Id).ShouldContain(FluxMetricCatalog.MeasureRate);
        catalog.EventTypes.Select(static eventType => eventType.Value).ShouldContain(FlowEventTypes.MqttMessagePublished);

        var metric = catalog.CreateDefaultDefinition("messages");

        metric.Name.ShouldBe("messages");
        metric.Mode.ShouldBe(MetricDefinitionMode.Builder);
        metric.Source.ShouldBe(FluxMetricCatalog.RuntimeEventsSource);
        metric.Measure.ShouldBe(FluxMetricCatalog.MeasureCount);
        metric.Window.ShouldBe("60s");
        metric.ExportPolicy.Enabled.ShouldBeFalse();
    }

    [Fact]
    public void AddFluxMqMetricStreams_RegistersAllNumberMeasureModulesAsKeyedServices()
    {
        using var services = new ServiceCollection()
            .AddFluxMqMetricStreams()
            .BuildServiceProvider();

        foreach (var measure in FluxMetricCatalog.Shared.Measures)
        {
            var module = services.GetRequiredKeyedService<IFluxMetricStreamModule<double>>(measure.Id);

            module.MeasureId.ShouldBe(measure.Id);
        }
    }

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
    public void FluxMetricQueryDraft_CreatesDefinitionFromReusableMetricAndLegacyFilters()
    {
        var metric = new FluxMetricDefinition(
            "publishedMetric",
            FluxMetricCatalog.RuntimeEventsSource,
            FluxMetricCatalog.MeasureRate,
            "300s",
            eventType: FlowEventTypes.MqttMessagePublished,
            topicStartsWith: "metric/",
            status: "published",
            format: FluxMetricCatalog.FormatNumber);
        var legacyFilters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [FluxMetricCatalog.TopicStartsWithKey] = "legacy/",
            [FluxMetricCatalog.AttributeFilterKey("qos")] = "1",
            [FluxMetricCatalog.AttributeFilterKey("retain")] = "false"
        };

        var draft = FluxMetricQueryDraft.Create(metric, legacyFilters);
        var definition = draft.BuildDefinition("publishedMetric");

        definition.Measure.ShouldBe(FluxMetricCatalog.MeasureRate);
        definition.Window.ShouldBe("300s");
        definition.EventType.ShouldBe(FlowEventTypes.MqttMessagePublished);
        definition.TopicStartsWith.ShouldBe("metric/");
        definition.Status.ShouldBeNull();
        definition.AdditionalFilters[FluxMetricCatalog.AttributeFilterKey("qos")].ShouldBe("1");
        definition.AdditionalFilters[FluxMetricCatalog.AttributeFilterKey("retain")].ShouldBe("false");
    }

    [Fact]
    public void FluxMetricValidator_ValidatesMeasureSourceWindowAndMode()
    {
        var validator = new FluxMetricValidator();
        var valid = new FluxMetricDefinition(
            "payloadBytes",
            FluxMetricCatalog.PayloadInspectionSource,
            FluxMetricCatalog.MeasurePayloadBytes,
            "5m",
            format: FluxMetricCatalog.FormatBytes);
        var invalid = new FluxMetricDefinition(
            "bad",
            FluxMetricCatalog.PayloadInspectionSource,
            FluxMetricCatalog.MeasureCount,
            "0s",
            mode: MetricDefinitionMode.Expression);

        validator.Validate(valid).IsValid.ShouldBeTrue();

        var result = validator.Validate(invalid);
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain("Expression metrics are reserved and are not enabled yet.");
        result.Errors.ShouldContain("Window must be a positive duration between 1s and 24h.");
        result.Errors.ShouldContain("Source 'payloadInspection' is not compatible with measure 'count'.");
    }

    [Fact]
    public void FluxMetricEvaluationEngine_EvaluatesFilteredRuntimeEvents()
    {
        var now = DateTimeOffset.Parse("2026-06-07T12:00:00Z");
        var definition = new FluxMetricDefinition(
            "factoryReceived",
            FluxMetricCatalog.RuntimeEventsSource,
            FluxMetricCatalog.MeasureCount,
            "60s",
            eventType: FlowEventTypes.MqttMessageReceived,
            topicStartsWith: "factory/",
            additionalFilters: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [FluxMetricCatalog.AttributeFilterKey("qos")] = "1"
            });
        var engine = new FluxMetricEvaluationEngine();
        var events = new[]
        {
            Event(FlowEventTypes.MqttMessageReceived, now.AddSeconds(-5), "factory/line-a", payloadBytes: 12, attributes: new Dictionary<string, string> { ["qos"] = "1" }),
            Event(FlowEventTypes.MqttMessageReceived, now.AddSeconds(-15), "factory/line-b", payloadBytes: 18, attributes: new Dictionary<string, string> { ["qos"] = "1" }),
            Event(FlowEventTypes.MqttMessageReceived, now.AddSeconds(-20), "other/line-c", payloadBytes: 20, attributes: new Dictionary<string, string> { ["qos"] = "1" }),
            Event(FlowEventTypes.MqttMessageReceived, now.AddSeconds(-30), "factory/line-d", payloadBytes: 20, attributes: new Dictionary<string, string> { ["qos"] = "0" }),
            Event(FlowEventTypes.MqttMessageReceived, now.AddMinutes(-5), "factory/old", payloadBytes: 20, attributes: new Dictionary<string, string> { ["qos"] = "1" })
        };

        var snapshot = engine.CreateSnapshot(definition, events, now);
        var value = engine.Evaluate(definition, snapshot);

        snapshot.Count.ShouldBe(3);
        snapshot.RecentCount.ShouldBe(2);
        snapshot.TotalPayloadBytes.ShouldBe(50);
        snapshot.UniqueTopicCount.ShouldBe(3);
        value.Value.ShouldBe(2);
        value.FormattedValue.ShouldBe("2");
    }

    [Fact]
    public void FluxMetricPreviewService_UsesLiveDataBeforeSampleFallback()
    {
        var definition = FluxMetricCatalog.Shared.CreateDefaultDefinition("messages");
        var liveSnapshot = new FluxMetricRuntimeSnapshot(
            Count: 3,
            LatestEvent: null,
            RecentCount: 3,
            RateWindow: TimeSpan.FromSeconds(60),
            EventsPerSecond: 0.05,
            Events: [],
            TopicCounts: [],
            TotalPayloadBytes: 20,
            UniqueTopicCount: 0,
            RetainedCount: 0);
        var sampleSnapshot = new FluxMetricRuntimeSnapshot(
            Count: 8,
            LatestEvent: null,
            RecentCount: 8,
            RateWindow: TimeSpan.FromSeconds(60),
            EventsPerSecond: 0.13,
            Events: [],
            TopicCounts: [],
            TotalPayloadBytes: 80,
            UniqueTopicCount: 0,
            RetainedCount: 0);

        var preview = new FluxMetricPreviewService().Create(definition, liveSnapshot, () => sampleSnapshot);

        preview.IsLive.ShouldBeTrue();
        preview.WindowEventCount.ShouldBe(3);
        preview.TotalMatchCount.ShouldBe(3);
        preview.MatchingEventCount.ShouldBe(3);
        preview.Value.Value.ShouldBe(3);
        preview.EmptyReason.ShouldBeEmpty();
    }

    [Fact]
    public void FluxMetricPreviewService_UsesSampleWhenOnlyOldLiveMatchesExist()
    {
        var definition = FluxMetricCatalog.Shared.CreateDefaultDefinition("messages");
        var liveSnapshot = new FluxMetricRuntimeSnapshot(
            Count: 5,
            LatestEvent: null,
            RecentCount: 0,
            RateWindow: TimeSpan.FromSeconds(60),
            EventsPerSecond: 0,
            Events: [],
            TopicCounts: [],
            TotalPayloadBytes: 120,
            UniqueTopicCount: 2,
            RetainedCount: 1);
        var sampleSnapshot = new FluxMetricRuntimeSnapshot(
            Count: 8,
            LatestEvent: null,
            RecentCount: 8,
            RateWindow: TimeSpan.FromSeconds(60),
            EventsPerSecond: 0.13,
            Events: [],
            TopicCounts: [],
            TotalPayloadBytes: 80,
            UniqueTopicCount: 0,
            RetainedCount: 0);

        var preview = new FluxMetricPreviewService().Create(definition, liveSnapshot, () => sampleSnapshot);

        preview.IsLive.ShouldBeFalse();
        preview.WindowEventCount.ShouldBe(8);
        preview.TotalMatchCount.ShouldBe(8);
        preview.Value.Value.ShouldBe(8);
        preview.EmptyReason.ShouldBe("No live events match this query in the selected window. Showing generated sample data.");
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

    [Fact]
    public async Task FluxEventCountMetric_EmitsNumberReadings()
    {
        var events = CreateRuntimeEventSource();
        var metric = new FluxEventCountMetric(
            "messageCount",
            new FluxMetricDefinition(
                "messageCount",
                FluxMetricCatalog.RuntimeEventsSource,
                FluxMetricCatalog.MeasureCount,
                "60s",
                eventType: FlowEventTypes.MqttMessageReceived),
            events);

        await metric.StartAsync();
        events.Post(Event(FlowEventTypes.MqttMessageReceived, DateTimeOffset.UtcNow, "factory/a"));

        var reading = await ReceiveAsync(metric.Output);

        reading.MetricId.ShouldBe("messageCount");
        reading.Value.ShouldBe(1);
        metric.Latest.ShouldBe(reading);
    }

    [Fact]
    public async Task FluxEventRateMetric_PrunesOldEventsFromWindow()
    {
        var events = CreateRuntimeEventSource();
        var metric = new FluxEventRateMetric(
            "messageRate",
            new FluxMetricDefinition(
                "messageRate",
                FluxMetricCatalog.RuntimeEventsSource,
                FluxMetricCatalog.MeasureRate,
                "60s",
                eventType: FlowEventTypes.MqttMessageReceived),
            events);

        await metric.StartAsync();
        events.Post(Event(FlowEventTypes.MqttMessageReceived, DateTimeOffset.UtcNow.AddMinutes(-2), "factory/old"));
        events.Post(Event(FlowEventTypes.MqttMessageReceived, DateTimeOffset.UtcNow, "factory/current"));

        var first = await ReceiveAsync(metric.Output);
        var second = first.Value > 0 ? first : await ReceiveAsync(metric.Output);

        second.Value.ShouldBe(1d / 60d, tolerance: 0.0001);
    }

    [Fact]
    public async Task FluxMetricRuntimeHost_StartsConfiguredMetricStreams()
    {
        var events = CreateRuntimeEventSource();
        var host = new FluxMetricRuntimeHost();
        host.Configure(
            new Dictionary<string, FluxMetricArtifactDefinition>(StringComparer.Ordinal)
            {
                ["messageCount"] = new()
                {
                    DisplayName = "Message count",
                    Definition = new FluxMetricDefinition(
                        "messageCount",
                        FluxMetricCatalog.RuntimeEventsSource,
                        FluxMetricCatalog.MeasureCount,
                        "60s",
                        eventType: FlowEventTypes.MqttMessagePublished)
                }
            },
            events);
        var stream = host.GetNumberStream("messageCount");

        await host.StartAsync();
        events.Post(Event(FlowEventTypes.MqttMessagePublished, DateTimeOffset.UtcNow, "factory/a"));

        var reading = await ReceiveAsync(stream);

        reading.MetricId.ShouldBe("messageCount");
        reading.Value.ShouldBe(1);
        host.TryGetLatestNumber("messageCount", null, out var latest).ShouldBeTrue();
        latest.ShouldBe(reading);
    }

    [Fact]
    public async Task MetricSourceComponent_RelaysExistingMetricStream()
    {
        var events = CreateRuntimeEventSource();
        var host = new FluxMetricRuntimeHost();
        host.Configure(
            new Dictionary<string, FluxMetricArtifactDefinition>(StringComparer.Ordinal)
            {
                ["retainedMessages"] = new()
                {
                    DisplayName = "Retained messages",
                    Definition = new FluxMetricDefinition(
                        "retainedMessages",
                        FluxMetricCatalog.RuntimeEventsSource,
                        FluxMetricCatalog.MeasureRetained,
                        "60s",
                        eventType: FlowEventTypes.MqttMessagePublished)
                }
            },
            events);
        var source = new MetricSourceComponent(host, "retainedMessages");

        await host.StartAsync();
        await source.StartAsync();
        events.Post(Event(
            FlowEventTypes.MqttMessagePublished,
            DateTimeOffset.UtcNow,
            "factory/a",
            attributes: new Dictionary<string, string> { ["retain"] = "true" }));

        var reading = await ReceiveAsync(source.Output);

        reading.MetricId.ShouldBe("retainedMessages");
        reading.Value.ShouldBe(1);
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

    private static BroadcastBlock<FlowEvent> CreateRuntimeEventSource()
        => new(static flowEvent => flowEvent);

    private static async Task<FluxMetricReading<double>> ReceiveAsync(
        ISourceBlock<FluxMetricReading<double>> source)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        return await source.ReceiveAsync(cancellation.Token);
    }

    private sealed record RecordedMeasurement(
        string InstrumentName,
        long Value,
        KeyValuePair<string, object?>[] Tags);
}
