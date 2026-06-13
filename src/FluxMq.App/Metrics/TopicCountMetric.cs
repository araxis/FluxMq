using FluxFlow.Engine.Components;
using FluxMq.Core.Metrics;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.App.Metrics;

/// <summary>
/// Counts the distinct topics seen on traffic that matches a topic filter and QoS, cumulatively since start.
/// A flat metric: it owns its two parameters and its counting logic, and composes a <see cref="MetricEventPump{T}"/>
/// for the stream plumbing.
/// </summary>
public sealed class TopicCountMetric : IFluxMetricSource<int>
{
    public const string TypeId = "topic.count";
    private const string TopicKey = "topic";
    private const string QosKey = "qos";

    private readonly MetricEventPump<int> _pump;
    private readonly HashSet<string> _topics = new(StringComparer.Ordinal);
    private readonly TopicFilter _topic;
    private readonly int _qos;

    public TopicCountMetric(
        string metricId,
        TopicFilter topic,
        int qos,
        ISourceBlock<FlowEvent> events,
        int boundedCapacity = 256,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(topic);
        _topic = topic;
        _qos = qos;
        _pump = new MetricEventPump<int>(metricId, events, Observe, boundedCapacity, timeProvider);
    }

    private MetricSample<int> Observe(FlowEvent flowEvent, DateTimeOffset now)
    {
        if (!MetricEvents.IsMqttMessage(flowEvent) ||
            !_topic.Matches(flowEvent.Channel) ||
            !MetricQos.Matches(flowEvent, _qos))
        {
            return MetricSample<int>.None;
        }

        _topics.Add(flowEvent.Channel!);
        return MetricSample<int>.Of(_topics.Count);
    }

    public string MetricId => _pump.MetricId;
    public FluxMetricReading<int>? Latest => _pump.Latest;
    public ISourceBlock<FluxMetricReading<int>> Output => _pump.Output;
    public Task StartAsync(CancellationToken cancellationToken = default) => _pump.StartAsync(cancellationToken);
    public void Complete() => _pump.Complete();
    public Task Completion => _pump.Completion;

    public static MetricDescriptor Descriptor { get; } = new(
        TypeId,
        "Topic count",
        "Counts the distinct topics seen on traffic matching the topic filter and QoS.",
        MetricValueKind.Int,
        "topics",
        MetricFormats.Number,
        [
            MetricParam.Topic(TopicKey, "Topic filter", required: true, placeholder: "factory/+/temperature"),
            MetricParam.Integer(QosKey, "QoS", min: 0, max: 2, defaultValue: "0")
        ]);

    public static IFluxMetricSource Create(MetricSourceContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var reader = new MetricParamReader(context.Parameters);
        return new TopicCountMetric(
            context.MetricId,
            TopicFilter.Parse(reader.GetString(TopicKey)),
            reader.GetInt(QosKey, 0),
            context.Events,
            context.BoundedCapacity,
            context.Clock);
    }
}

public static class TopicCountMetricRegistration
{
    public static IServiceCollection AddTopicCountMetric(this IServiceCollection services)
        => services.AddFluxMetric(TopicCountMetric.Descriptor, TopicCountMetric.Create);
}
