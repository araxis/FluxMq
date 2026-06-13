using FluxFlow.Engine.Components;
using FluxMq.Core.Metrics;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.App.Metrics;

/// <summary>
/// Events per second on traffic matching a topic filter and QoS, measured over a rolling window.
/// A double-valued flat metric.
/// </summary>
/// <remarks>
/// Known limitation (carried over from the previous framework): the value is recomputed only when a matching
/// event arrives, so after traffic stops the last rate persists rather than decaying toward zero. Adding a
/// periodic prune+recompute tick is a future improvement.
/// </remarks>
public sealed class EventRateMetric : IFluxMetricSource<double>
{
    public const string TypeId = "event.rate";
    private const string TopicKey = "topic";
    private const string QosKey = "qos";
    private const string WindowKey = "window";

    private readonly MetricEventPump<double> _pump;
    private readonly SlidingEventWindow _window;
    private readonly double _windowSeconds;
    private readonly TopicFilter _topic;
    private readonly int _qos;

    public EventRateMetric(
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
        _windowSeconds = window <= TimeSpan.Zero ? 60d : window.TotalSeconds;
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
        return MetricSample<double>.Of(_window.Count / _windowSeconds);
    }

    public string MetricId => _pump.MetricId;
    public FluxMetricReading<double>? Latest => _pump.Latest;
    public ISourceBlock<FluxMetricReading<double>> Output => _pump.Output;
    public Task StartAsync(CancellationToken cancellationToken = default) => _pump.StartAsync(cancellationToken);
    public void Complete() => _pump.Complete();
    public Task Completion => _pump.Completion;

    public static MetricDescriptor Descriptor { get; } = new(
        TypeId,
        "Event rate",
        "Events per second on matching traffic over a rolling window.",
        MetricValueKind.Double,
        "/s",
        MetricFormats.Number,
        [
            MetricParam.Topic(TopicKey, "Topic filter", required: true, placeholder: "factory/#"),
            MetricParam.Integer(QosKey, "QoS", min: 0, max: 2, defaultValue: "0"),
            MetricParam.Duration(WindowKey, "Window", defaultValue: "60s")
        ]);

    public static IFluxMetricSource Create(MetricSourceContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var reader = new MetricParamReader(context.Parameters);
        return new EventRateMetric(
            context.MetricId,
            TopicFilter.Parse(reader.GetString(TopicKey)),
            reader.GetInt(QosKey, 0),
            MetricWindow.Parse(reader.GetString(WindowKey)),
            context.Events,
            context.BoundedCapacity,
            context.Clock);
    }
}

public static class EventRateMetricRegistration
{
    public static IServiceCollection AddEventRateMetric(this IServiceCollection services)
        => services.AddFluxMetric(EventRateMetric.Descriptor, EventRateMetric.Create);
}
