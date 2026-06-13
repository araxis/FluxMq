using FluxFlow.Engine.Components;
using FluxMq.App.Metrics;
using FluxMq.Core.Metrics;
using FluxMq.Scenarios;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.App.Tests.Runtime;

public sealed class MetricTypeFrameworkTests
{
    [Fact]
    public void EventCountMetricType_ExposesParametersAndDefaults()
    {
        var type = new EventCountMetricType();

        type.TypeId.ShouldBe("event.count");
        type.Unit.ShouldBe("events");
        type.Format.ShouldBe(MetricFormats.Number);

        var keys = type.Parameters.Select(static parameter => parameter.Key).ToArray();
        keys.ShouldContain(MetricParameterKeys.Window);
        keys.ShouldContain(MetricParameterKeys.EventType);
        keys.ShouldContain(MetricParameterKeys.TopicStartsWith);
        keys.ShouldContain(MetricParameterKeys.Qos);
        keys.ShouldContain(MetricParameterKeys.Retain);

        var window = type.Parameters.Single(static parameter => parameter.Key == MetricParameterKeys.Window);
        window.DefaultValue.ShouldBe("60s");
        window.Kind.ShouldBe(FluxMetricParameterKinds.Duration);
    }

    [Fact]
    public void AddFluxMqMetricTypes_RegistersTypeRegistryResolvableById()
    {
        using var services = new ServiceCollection()
            .AddFluxMqMetricTypes()
            .BuildServiceProvider();

        var registry = services.GetRequiredService<IFluxMetricTypeRegistry>();

        registry.NumberTypes.Select(static type => type.TypeId).ShouldContain(EventCountMetricType.Id);
        registry.GetNumberType(EventCountMetricType.Id).ShouldBeOfType<EventCountMetricType>();
        registry.TryGetNumberType("does.not.exist", out _).ShouldBeFalse();
    }

    [Fact]
    public void EventCountMetricType_ValidatesWindowAndAllowedOptions()
    {
        var type = new EventCountMetricType();

        type.Validate(Resource(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [MetricParameterKeys.Window] = "5m",
            [MetricParameterKeys.EventType] = FlowEventTypes.MqttMessageReceived
        })).IsValid.ShouldBeTrue();

        var badWindow = type.Validate(Resource(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [MetricParameterKeys.Window] = "0s"
        }));
        badWindow.IsValid.ShouldBeFalse();
        badWindow.Errors.ShouldContain("Window must be a positive duration between 1s and 24h.");

        var badEventType = type.Validate(Resource(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [MetricParameterKeys.EventType] = "nonsense"
        }));
        badEventType.IsValid.ShouldBeFalse();
        badEventType.Errors.ShouldContain("Metric parameter 'Event type' has unsupported value 'nonsense'.");
    }

    [Fact]
    public void EventCountMetricType_SummarizesActiveParameters()
    {
        var summary = new EventCountMetricType().Summarize(Resource(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [MetricParameterKeys.Window] = "60s",
            [MetricParameterKeys.TopicStartsWith] = "factory/"
        }));

        summary.ShouldBe("Event count over 60s where topic starts with factory/");
    }

    [Fact]
    public async Task EventCountMetricSource_CountsOnlyMatchingEvents()
    {
        var events = CreateRuntimeEventSource();
        var source = new EventCountMetricType().CreateSource(new FluxMetricSourceContext(
            "messageCount",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [MetricParameterKeys.EventType] = FlowEventTypes.MqttMessageReceived,
                [MetricParameterKeys.TopicStartsWith] = "factory/",
                [MetricParameterKeys.Qos] = "1"
            },
            events,
            BoundedCapacity: 1000,
            TimeProvider.System));

        var readings = new List<FluxMetricReading<double>>();
        var sink = new ActionBlock<FluxMetricReading<double>>(readings.Add);
        source.Output.LinkTo(sink, new DataflowLinkOptions { PropagateCompletion = true });

        await source.StartAsync();
        events.Post(Event(FlowEventTypes.MqttMessageReceived, DateTimeOffset.UtcNow, "factory/a", attributes: Qos("1")));
        events.Post(Event(FlowEventTypes.MqttMessageReceived, DateTimeOffset.UtcNow, "other/b", attributes: Qos("1")));
        events.Post(Event(FlowEventTypes.MqttMessageReceived, DateTimeOffset.UtcNow, "factory/c", attributes: Qos("1")));
        events.Complete();

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await Task.WhenAll(source.Completion, sink.Completion).WaitAsync(cancellation.Token);

        readings.Select(static reading => reading.Value).ShouldBe([1, 2]);
        readings[^1].MetricId.ShouldBe("messageCount");
        source.Latest!.Value.ShouldBe(2);
    }

    [Fact]
    public async Task FluxMetricStreamHost_StartsConfiguredResourceStream()
    {
        var events = CreateRuntimeEventSource();
        var host = new FluxMetricStreamHost();
        host.Configure(
            new Dictionary<string, FluxMetricResourceDefinition>(StringComparer.Ordinal)
            {
                ["messageCount"] = new()
                {
                    Id = "messageCount",
                    TypeId = EventCountMetricType.Id,
                    DisplayName = "Message count",
                    Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        [MetricParameterKeys.EventType] = FlowEventTypes.MqttMessagePublished
                    }
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
    public void FluxMetricStreamHost_ReusesStreamPerParametersWithoutRecalculating()
    {
        var events = CreateRuntimeEventSource();
        var host = new FluxMetricStreamHost();
        host.Configure(
            new Dictionary<string, FluxMetricResourceDefinition>(StringComparer.Ordinal)
            {
                ["messageCount"] = new()
                {
                    Id = "messageCount",
                    TypeId = EventCountMetricType.Id,
                    Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        [MetricParameterKeys.TopicStartsWith] = "factory/"
                    }
                }
            },
            events);

        var first = host.GetNumberStream("messageCount");
        var second = host.GetNumberStream("messageCount");
        var overridden = host.GetNumberStream(
            "messageCount",
            new Dictionary<string, string>(StringComparer.Ordinal) { [MetricParameterKeys.Window] = "30s" });

        second.ShouldBeSameAs(first);
        overridden.ShouldNotBeSameAs(first);
        host.TryGetLatestNumber("messageCount", null, out _).ShouldBeFalse();
    }

    [Fact]
    public void FluxMetricStreamHost_ThrowsForUnconfiguredOrUnknownMetric()
    {
        var host = new FluxMetricStreamHost();

        Should.Throw<InvalidOperationException>(() => host.GetNumberStream("missing"))
            .Message.ShouldBe("Metric stream host is not configured for this app run.");

        host.Configure(
            new Dictionary<string, FluxMetricResourceDefinition>(StringComparer.Ordinal),
            CreateRuntimeEventSource());

        Should.Throw<InvalidOperationException>(() => host.GetNumberStream("missing"))
            .Message.ShouldBe("Metric 'missing' does not exist.");
    }

    private static FluxMetricResourceDefinition Resource(Dictionary<string, string> parameters)
        => new()
        {
            Id = "metric",
            TypeId = EventCountMetricType.Id,
            DisplayName = "Metric",
            Parameters = parameters
        };

    private static Dictionary<string, string> Qos(string value)
        => new(StringComparer.Ordinal) { ["qos"] = value };

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
}
