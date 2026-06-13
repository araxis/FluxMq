using FluxFlow.Engine.Components;
using FluxMq.Core.Metrics;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.App.Metrics;

/// <summary>
/// Counts retained messages on traffic matching a topic filter and QoS, cumulatively since start.
/// </summary>
public sealed class RetainedCountMetric : IFluxMetricSource<int>
{
    public const string TypeId = "message.retained";
    private const string TopicKey = "topic";
    private const string QosKey = "qos";

    private readonly MetricEventPump<int> _pump;
    private readonly TopicFilter _topic;
    private readonly int _qos;
    private int _count;

    public RetainedCountMetric(
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
        if (!_topic.Matches(flowEvent.Channel) ||
            !MetricQos.Matches(flowEvent, _qos) ||
            !MetricRetain.IsRetained(flowEvent))
        {
            return MetricSample<int>.None;
        }

        _count++;
        return MetricSample<int>.Of(_count);
    }

    public string MetricId => _pump.MetricId;
    public FluxMetricReading<int>? Latest => _pump.Latest;
    public ISourceBlock<FluxMetricReading<int>> Output => _pump.Output;
    public Task StartAsync(CancellationToken cancellationToken = default) => _pump.StartAsync(cancellationToken);
    public void Complete() => _pump.Complete();
    public Task Completion => _pump.Completion;

    public static MetricDescriptor Descriptor { get; } = new(
        TypeId,
        "Retained messages",
        "Counts retained messages on matching traffic.",
        MetricValueKind.Int,
        "messages",
        MetricFormats.Number,
        [
            MetricParam.Topic(TopicKey, "Topic filter", required: true, placeholder: "factory/#"),
            MetricParam.Integer(QosKey, "QoS", min: 0, max: 2, defaultValue: "0")
        ]);

    public static IFluxMetricSource Create(MetricSourceContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var reader = new MetricParamReader(context.Parameters);
        return new RetainedCountMetric(
            context.MetricId,
            TopicFilter.Parse(reader.GetString(TopicKey)),
            reader.GetInt(QosKey, 0),
            context.Events,
            context.BoundedCapacity,
            context.Clock);
    }
}

public static class RetainedCountMetricRegistration
{
    public static IServiceCollection AddRetainedCountMetric(this IServiceCollection services)
        => services.AddFluxMetric(RetainedCountMetric.Descriptor, RetainedCountMetric.Create);
}
