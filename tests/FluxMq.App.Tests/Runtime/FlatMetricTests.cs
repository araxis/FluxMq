using FluxFlow.Engine.Components;
using FluxMq.App.Metrics;
using FluxMq.Core.Metrics;
using FluxMq.Scenarios;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.App.Tests.Runtime;

public sealed class FlatMetricTests
{
    [Fact]
    public async Task TopicCountMetric_CountsDistinctMatchingTopics()
    {
        var events = new BufferBlock<FlowEvent>();
        var metric = new TopicCountMetric("topics", TopicFilter.Parse("factory/#"), qos: 0, events);

        var readings = await RunAsync(metric, events,
            Event("factory/a", qos: "0"),
            Event("factory/b", qos: "0"),
            Event("other/c", qos: "0"),     // wrong topic
            Event("factory/d", qos: "1"),    // wrong qos
            Event("factory/a", qos: "0"));   // duplicate topic

        readings.Select(static reading => reading.Value).ShouldBe([1, 2, 2]);
        metric.Latest!.Value.ShouldBe(2);
        readings[^1].MetricId.ShouldBe("topics");
    }

    [Fact]
    public async Task MessageCountMetric_CountsMatchingMessages()
    {
        var events = new BufferBlock<FlowEvent>();
        var metric = new MessageCountMetric("messages", TopicFilter.Parse("factory/#"), qos: 1, events);

        var readings = await RunAsync(metric, events,
            Event("factory/a", qos: "1"),
            Event("factory/b", qos: "1"),
            Event("factory/c", qos: "0"),    // wrong qos
            Event("other/d", qos: "1"));     // wrong topic

        readings.Select(static reading => reading.Value).ShouldBe([1, 2]);
        metric.Latest!.Value.ShouldBe(2);
    }

    [Fact]
    public async Task WindowedMessageCountMetric_CountsEventsWithinWindow()
    {
        var events = new BufferBlock<FlowEvent>();
        var metric = new WindowedMessageCountMetric(
            "windowed", TopicFilter.Parse("factory/#"), qos: 0, TimeSpan.FromMinutes(5), events);

        var readings = await RunAsync(metric, events,
            Event("factory/a", qos: "0"),
            Event("factory/b", qos: "0"));

        metric.Latest!.Value.ShouldBe(2);
        readings.Count.ShouldBe(2);
    }

    [Fact]
    public void SlidingEventWindow_PrunesEventsOlderThanWindow()
    {
        var start = DateTimeOffset.Parse("2026-06-13T12:00:00Z");
        var window = new SlidingEventWindow(TimeSpan.FromSeconds(10));

        window.Add(EventAt("factory/a", start), start);
        window.Add(EventAt("factory/b", start.AddSeconds(5)), start.AddSeconds(5));
        window.Count.ShouldBe(2);
        window.DistinctTopicCount.ShouldBe(2);

        // 12s in: the first event (t0) is now older than the 10s window and is dropped.
        window.Add(EventAt("factory/b", start.AddSeconds(12)), start.AddSeconds(12));
        window.Count.ShouldBe(2);            // b@5s and b@12s
        window.DistinctTopicCount.ShouldBe(1); // both are factory/b
    }

    [Theory]
    [InlineData("factory/+/temperature", "factory/line-a/temperature", true)]
    [InlineData("factory/+/temperature", "factory/line-a/humidity", false)]
    [InlineData("factory/+/temperature", "factory/line-a/cell-1/temperature", false)]
    [InlineData("factory/#", "factory", true)]
    [InlineData("factory/#", "factory/line-a/cell-1", true)]
    [InlineData("factory/#", "warehouse/line-a", false)]
    [InlineData("#", "anything/at/all", true)]
    [InlineData("a/b", "a/b", true)]
    [InlineData("a/b", "a/b/c", false)]
    public void TopicFilter_MatchesMqttWildcards(string filter, string topic, bool expected)
        => TopicFilter.Parse(filter).Matches(topic).ShouldBe(expected);

    [Fact]
    public void FluxMetricCatalog_ListsMetricsAndCreatesById()
    {
        var catalog = FluxMetricCatalog.CreateDefault();

        catalog.Descriptors.Select(static descriptor => descriptor.TypeId).ShouldBe(
            [TopicCountMetric.TypeId, MessageCountMetric.TypeId, WindowedTopicCountMetric.TypeId, WindowedMessageCountMetric.TypeId],
            ignoreOrder: true);

        var topicCount = catalog.Describe(TopicCountMetric.TypeId).ShouldNotBeNull();
        topicCount.ValueKind.ShouldBe(MetricValueKind.Int);
        topicCount.Parameters.Select(static parameter => parameter.Key).ShouldBe(["topic", "qos"]);
        topicCount.Parameters[0].Kind.ShouldBe(MetricParamKind.Topic);
        topicCount.Parameters[0].Required.ShouldBeTrue();
        topicCount.Parameters[1].Kind.ShouldBe(MetricParamKind.Integer);

        var source = catalog.Create(TopicCountMetric.TypeId, new MetricSourceContext(
            "topics",
            new Dictionary<string, string>(StringComparer.Ordinal) { ["topic"] = "factory/#", ["qos"] = "0" },
            new BufferBlock<FlowEvent>()));
        source.ShouldBeOfType<TopicCountMetric>();
        source.MetricId.ShouldBe("topics");

        catalog.TryGet("does.not.exist", out _).ShouldBeFalse();
    }

    [Fact]
    public void AddFluxMetrics_RegistersCatalogWithAllBuiltInMetrics()
    {
        using var provider = new ServiceCollection().AddFluxMetrics().BuildServiceProvider();

        var catalog = provider.GetRequiredService<IFluxMetricCatalog>();

        catalog.Descriptors.Count.ShouldBe(4);
        catalog.TryGet(MessageCountMetric.TypeId, out var registration).ShouldBeTrue();
        registration.Descriptor.DisplayName.ShouldBe("Message count");
    }

    private static async Task<List<FluxMetricReading<int>>> RunAsync(
        IFluxMetricSource<int> metric,
        BufferBlock<FlowEvent> events,
        params FlowEvent[] inputs)
    {
        var readings = new List<FluxMetricReading<int>>();
        var sink = new ActionBlock<FluxMetricReading<int>>(readings.Add);
        metric.Output.LinkTo(sink, new DataflowLinkOptions { PropagateCompletion = true });

        await metric.StartAsync();
        foreach (var input in inputs)
        {
            events.Post(input);
        }

        events.Complete();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await Task.WhenAll(metric.Completion, sink.Completion).WaitAsync(cancellation.Token);
        return readings;
    }

    private static FlowEvent Event(string topic, string qos)
        => EventAt(topic, DateTimeOffset.UtcNow, qos);

    private static FlowEvent EventAt(string topic, DateTimeOffset timestamp, string qos = "0")
        => new()
        {
            Timestamp = timestamp,
            Type = FlowEventTypes.MqttMessageReceived,
            Source = "test",
            Channel = topic,
            Attributes = new Dictionary<string, string>(StringComparer.Ordinal) { ["qos"] = qos }
        };
}
