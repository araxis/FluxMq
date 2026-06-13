using FluxFlow.Engine.Components;
using FluxMq.App.Metrics;
using FluxMq.Core.Metrics;
using FluxMq.Scenarios;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.App.Tests.Runtime;

public sealed class MetricSourceMathTests
{
    [Fact]
    public void AddFluxMqMetricTypes_RegistersAllSixNumberTypes()
    {
        using var services = new ServiceCollection().AddFluxMqMetricTypes().BuildServiceProvider();

        var ids = services.GetRequiredService<IFluxMetricTypeRegistry>()
            .NumberTypes.Select(static type => type.TypeId)
            .ToArray();

        ids.ShouldBe(
            [
                EventCountMetricType.Id,
                EventRateMetricType.Id,
                UniqueTopicCountMetricType.Id,
                PayloadBytesMetricType.Id,
                AveragePayloadMetricType.Id,
                RetainedCountMetricType.Id
            ],
            ignoreOrder: true);
    }

    [Fact]
    public async Task EventRateMetricSource_DividesWindowCountByWindowSeconds()
    {
        var events = CreateRuntimeEventSource();
        var source = Source(new EventRateMetricType(), events);

        var readings = await RunAsync(
            source,
            events,
            Event(FlowEventTypes.MqttMessageReceived, DateTimeOffset.UtcNow, "factory/a"));

        readings[^1].Value.ShouldBe(1d / 60d, tolerance: 0.0001);
    }

    [Fact]
    public async Task UniqueTopicCountMetricSource_CountsDistinctTopics()
    {
        var events = CreateRuntimeEventSource();
        var source = Source(new UniqueTopicCountMetricType(), events);

        var readings = await RunAsync(
            source,
            events,
            Event(FlowEventTypes.MqttMessageReceived, DateTimeOffset.UtcNow, "factory/a"),
            Event(FlowEventTypes.MqttMessageReceived, DateTimeOffset.UtcNow, "factory/b"),
            Event(FlowEventTypes.MqttMessageReceived, DateTimeOffset.UtcNow, "factory/a"));

        readings[^1].Value.ShouldBe(2);
    }

    [Fact]
    public async Task PayloadBytesMetricSource_SumsPayloadBytes()
    {
        var events = CreateRuntimeEventSource();
        var source = Source(new PayloadBytesMetricType(), events);

        var readings = await RunAsync(
            source,
            events,
            Event(FlowEventTypes.MqttMessageReceived, DateTimeOffset.UtcNow, "factory/a", payloadBytes: 12),
            Event(FlowEventTypes.MqttMessageReceived, DateTimeOffset.UtcNow, "factory/b", payloadBytes: 18));

        readings[^1].Value.ShouldBe(30);
    }

    [Fact]
    public async Task AveragePayloadMetricSource_AveragesPayloadBytes()
    {
        var events = CreateRuntimeEventSource();
        var source = Source(new AveragePayloadMetricType(), events);

        var readings = await RunAsync(
            source,
            events,
            Event(FlowEventTypes.MqttMessageReceived, DateTimeOffset.UtcNow, "factory/a", payloadBytes: 12),
            Event(FlowEventTypes.MqttMessageReceived, DateTimeOffset.UtcNow, "factory/b", payloadBytes: 18));

        readings[^1].Value.ShouldBe(15);
    }

    [Fact]
    public async Task RetainedCountMetricSource_CountsRetainedMessages()
    {
        var events = CreateRuntimeEventSource();
        var source = Source(new RetainedCountMetricType(), events);

        var readings = await RunAsync(
            source,
            events,
            Event(FlowEventTypes.MqttMessagePublished, DateTimeOffset.UtcNow, "factory/a", attributes: Retain("true")),
            Event(FlowEventTypes.MqttMessagePublished, DateTimeOffset.UtcNow, "factory/b", attributes: Retain("false")));

        readings[^1].Value.ShouldBe(1);
    }

    private static IFluxMetricSource<double> Source(
        IFluxMetricType<double> type,
        BroadcastBlock<FlowEvent> events,
        params (string Key, string Value)[] parameters)
        => type.CreateSource(new FluxMetricSourceContext(
            "metric",
            parameters.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal),
            events,
            BoundedCapacity: 1000,
            TimeProvider.System));

    private static async Task<List<FluxMetricReading<double>>> RunAsync(
        IFluxMetricSource<double> source,
        BroadcastBlock<FlowEvent> events,
        params FlowEvent[] posts)
    {
        var readings = new List<FluxMetricReading<double>>();
        var sink = new ActionBlock<FluxMetricReading<double>>(readings.Add);
        source.Output.LinkTo(sink, new DataflowLinkOptions { PropagateCompletion = true });

        await source.StartAsync();
        foreach (var post in posts)
        {
            events.Post(post);
        }

        events.Complete();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await Task.WhenAll(source.Completion, sink.Completion).WaitAsync(cancellation.Token);
        return readings;
    }

    private static Dictionary<string, string> Retain(string value)
        => new(StringComparer.Ordinal) { ["retain"] = value };

    private static FlowEvent Event(
        string type,
        DateTimeOffset timestamp,
        string? topic = null,
        int? payloadBytes = null,
        IReadOnlyDictionary<string, string>? attributes = null)
        => new()
        {
            Timestamp = timestamp,
            Type = type,
            Source = "test",
            Channel = topic,
            PayloadBytes = payloadBytes,
            Attributes = attributes ?? new Dictionary<string, string>(StringComparer.Ordinal)
        };

    private static BroadcastBlock<FlowEvent> CreateRuntimeEventSource()
        => new(static flowEvent => flowEvent);
}
