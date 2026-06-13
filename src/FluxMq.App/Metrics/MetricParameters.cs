using FluxMq.Scenarios;

namespace FluxMq.App.Metrics;

/// <summary>
/// Flat parameter keys shared by the event-based metric types. These are the on-disk parameter names; there is
/// no <c>attribute:</c> prefix — qos and retain are first-class flat keys.
/// </summary>
public static class MetricParameterKeys
{
    public const string Window = "window";
    public const string EventType = "eventType";
    public const string TopicStartsWith = "topicStartsWith";
    public const string TopicNotStartsWith = "topicNotStartsWith";
    public const string Status = "status";
    public const string Qos = "qos";
    public const string Retain = "retain";
}

/// <summary>
/// The runtime event types a metric can filter by. This list is owned by the metric model so the central
/// catalog can be retired.
/// </summary>
internal static class MetricEventTypes
{
    public static IReadOnlyList<FluxMetricParameterOption> Options { get; } =
    [
        new(string.Empty, "Any event"),
        new(FlowEventTypes.MqttMessageReceived, "MQTT message received"),
        new(FlowEventTypes.MqttMessagePublished, "MQTT message published"),
        new(FlowEventTypes.MqttMessageRecorded, "MQTT message recorded"),
        new(FlowEventTypes.JsonSchemaValidated, "JSON schema validated"),
        new(FlowEventTypes.FileWritten, "File written"),
        new(FlowEventTypes.AssertionEvaluated, "Assertion evaluated")
    ];
}

/// <summary>
/// Prebuilt parameter descriptors reused by the event-based metric types so each type only lists the rows it exposes.
/// </summary>
internal static class EventFilterParameters
{
    public static FluxMetricParameterDescriptor Window(string defaultValue = MetricWindow.Default) => new()
    {
        Key = MetricParameterKeys.Window,
        Label = "Window",
        Kind = FluxMetricParameterKinds.Duration,
        DefaultValue = defaultValue,
        HelperText = "Rolling window, for example 30s, 1m, or 2h.",
        Placeholder = "60s"
    };

    public static FluxMetricParameterDescriptor EventType { get; } = new()
    {
        Key = MetricParameterKeys.EventType,
        Label = "Event type",
        Kind = FluxMetricParameterKinds.Select,
        HelperText = "Restrict to a single runtime event type.",
        Options = MetricEventTypes.Options
    };

    public static FluxMetricParameterDescriptor TopicStartsWith { get; } = new()
    {
        Key = MetricParameterKeys.TopicStartsWith,
        Label = "Topic starts with",
        Kind = FluxMetricParameterKinds.TopicPrefix,
        HelperText = "Match only topics with this prefix.",
        Placeholder = "factory/line-a/"
    };

    public static FluxMetricParameterDescriptor TopicNotStartsWith { get; } = new()
    {
        Key = MetricParameterKeys.TopicNotStartsWith,
        Label = "Exclude topic prefix",
        Kind = FluxMetricParameterKinds.TopicPrefix,
        HelperText = "Exclude topics that start with this prefix.",
        Placeholder = "$SYS/"
    };

    public static FluxMetricParameterDescriptor Status { get; } = new()
    {
        Key = MetricParameterKeys.Status,
        Label = "Status",
        Kind = FluxMetricParameterKinds.Text,
        HelperText = "Match a single event status, for example received or published.",
        Placeholder = "received"
    };

    public static FluxMetricParameterDescriptor Qos { get; } = new()
    {
        Key = MetricParameterKeys.Qos,
        Label = "QoS",
        Kind = FluxMetricParameterKinds.Text,
        HelperText = "Match a single MQTT QoS level.",
        Placeholder = "1"
    };

    public static FluxMetricParameterDescriptor Retain { get; } = new()
    {
        Key = MetricParameterKeys.Retain,
        Label = "Retain",
        Kind = FluxMetricParameterKinds.Toggle,
        HelperText = "Match the MQTT retain flag.",
        Placeholder = "false"
    };

    /// <summary>Window plus the full MQTT event filter set (event type, topic, status, qos, retain).</summary>
    public static IReadOnlyList<FluxMetricParameterDescriptor> EventFilters(string windowDefault = MetricWindow.Default) =>
    [
        Window(windowDefault),
        EventType,
        TopicStartsWith,
        TopicNotStartsWith,
        Status,
        Qos,
        Retain
    ];

    /// <summary>Window plus the topic-scope filters (event type and topic prefixes) used by topic/payload metrics.</summary>
    public static IReadOnlyList<FluxMetricParameterDescriptor> TopicFilters(string windowDefault = MetricWindow.Default) =>
    [
        Window(windowDefault),
        EventType,
        TopicStartsWith,
        TopicNotStartsWith
    ];
}
