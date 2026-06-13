using FluxFlow.Engine.Components;
using FluxMq.Core.Metrics;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.App.Metrics;

/// <summary>
/// Total payload bytes on traffic matching a topic filter and QoS within a rolling window. Double-valued.
/// </summary>
public sealed class PayloadBytesMetric : IFluxMetricSource<double>
{
    public const string TypeId = "payload.bytes";
    private const string TopicKey = "topic";
    private const string QosKey = "qos";
    private const string WindowKey = "window";

    private readonly MetricEventPump<double> _pump;
    private readonly SlidingEventWindow _window;
    private readonly TopicFilter _topic;
    private readonly int _qos;

    public PayloadBytesMetric(
        string metricId,
        TopicFilter topic,
        int qos,
        TimeSpan window,
        ISourceBlock<FlowEvent> events,
        int boundedCapacity = 256,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(topic);
        _topic = topic;
        _qos = qos;
        _window = new SlidingEventWindow(window);
        _pump = new MetricEventPump<double>(metricId, events, Observe, boundedCapacity, timeProvider);
    }

    private MetricSample<double> Observe(FlowEvent flowEvent, DateTimeOffset now)
    {
        if (!MetricEvents.IsMqttMessage(flowEvent) ||
            !_topic.Matches(flowEvent.Channel) ||
            !MetricQos.Matches(flowEvent, _qos))
        {
            _window.Prune(now);
            return MetricSample<double>.None;
        }

        _window.Add(flowEvent, now);
        return MetricSample<double>.Of(_window.TotalPayloadBytes);
    }

    public string MetricId => _pump.MetricId;
    public FluxMetricReading<double>? Latest => _pump.Latest;
    public ISourceBlock<FluxMetricReading<double>> Output => _pump.Output;
    public Task StartAsync(CancellationToken cancellationToken = default) => _pump.StartAsync(cancellationToken);
    public void Complete() => _pump.Complete();
    public Task Completion => _pump.Completion;

    public static MetricDescriptor Descriptor { get; } = new(
        TypeId,
        "Payload bytes",
        "Total payload bytes on matching traffic over a rolling window.",
        MetricValueKind.Double,
        "bytes",
        MetricFormats.Bytes,
        [
            MetricParam.Topic(TopicKey, "Topic filter", required: true, placeholder: "factory/#"),
            MetricParam.Integer(QosKey, "QoS", min: 0, max: 2, defaultValue: "0"),
            MetricParam.Duration(WindowKey, "Window", defaultValue: "60s")
        ]);

    public static IFluxMetricSource Create(MetricSourceContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var reader = new MetricParamReader(context.Parameters);
        return new PayloadBytesMetric(
            context.MetricId,
            TopicFilter.Parse(reader.GetString(TopicKey)),
            reader.GetInt(QosKey, 0),
            MetricWindow.Parse(reader.GetString(WindowKey)),
            context.Events,
            context.BoundedCapacity,
            context.Clock);
    }
}

public static class PayloadBytesMetricRegistration
{
    public static IServiceCollection AddPayloadBytesMetric(this IServiceCollection services)
        => services.AddFluxMetric(PayloadBytesMetric.Descriptor, PayloadBytesMetric.Create);
}
