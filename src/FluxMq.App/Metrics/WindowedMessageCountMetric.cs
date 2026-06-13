using FluxFlow.Engine.Components;
using FluxMq.Core.Metrics;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.App.Metrics;

/// <summary>
/// Counts the messages seen within a rolling window on traffic matching a topic filter and QoS.
/// </summary>
public sealed class WindowedMessageCountMetric : IFluxMetricSource<int>
{
    public const string TypeId = "message.count.windowed";
    private const string TopicKey = "topic";
    private const string QosKey = "qos";
    private const string WindowKey = "window";

    private readonly MetricEventPump<int> _pump;
    private readonly SlidingEventWindow _window;
    private readonly TopicFilter _topic;
    private readonly int _qos;

    public WindowedMessageCountMetric(
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
        _pump = new MetricEventPump<int>(metricId, events, Observe, boundedCapacity, timeProvider, Recompute);
    }

    private MetricSample<int> Observe(FlowEvent flowEvent, DateTimeOffset now)
    {
        if (!MetricEvents.IsMqttMessage(flowEvent) ||
            !_topic.Matches(flowEvent.Channel) ||
            !MetricQos.Matches(flowEvent, _qos))
        {
            _window.Prune(now);
            return MetricSample<int>.None;
        }

        _window.Add(flowEvent, now);
        return MetricSample<int>.Of(_window.Count);
    }

    // Idle decay tick: prune aged-out events and re-report the message count (settling to 0 on idle).
    private MetricSample<int> Recompute(DateTimeOffset now)
    {
        _window.Prune(now);
        return MetricSample<int>.Of(_window.Count);
    }

    public string MetricId => _pump.MetricId;
    public FluxMetricReading<int>? Latest => _pump.Latest;
    public ISourceBlock<FluxMetricReading<int>> Output => _pump.Output;
    public Task StartAsync(CancellationToken cancellationToken = default) => _pump.StartAsync(cancellationToken);
    public void Complete() => _pump.Complete();
    public Task Completion => _pump.Completion;

    public static MetricDescriptor Descriptor { get; } = new(
        TypeId,
        "Message count (windowed)",
        "Counts the messages seen within a rolling window on matching traffic.",
        MetricValueKind.Int,
        "messages",
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
        return new WindowedMessageCountMetric(
            context.MetricId,
            TopicFilter.Parse(reader.GetString(TopicKey)),
            reader.GetInt(QosKey, 0),
            MetricWindow.Parse(reader.GetString(WindowKey)),
            context.Events,
            context.BoundedCapacity,
            context.Clock);
    }
}

public static class WindowedMessageCountMetricRegistration
{
    public static IServiceCollection AddWindowedMessageCountMetric(this IServiceCollection services)
        => services.AddFluxMetric(WindowedMessageCountMetric.Descriptor, WindowedMessageCountMetric.Create);
}
