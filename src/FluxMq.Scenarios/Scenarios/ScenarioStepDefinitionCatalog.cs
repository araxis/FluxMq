namespace FluxMq.Scenarios;

public sealed class ScenarioStepDefinitionCatalog
{
    public const string ConnectionKey = ScenarioStepConfigurationKeys.Connection;
    public const string TopicKey = ScenarioStepConfigurationKeys.Topic;
    public const string PayloadKey = ScenarioStepConfigurationKeys.Payload;
    public const string PayloadEncodingKey = ScenarioStepConfigurationKeys.PayloadEncoding;
    public const string QosKey = ScenarioStepConfigurationKeys.Qos;
    public const string RetainKey = ScenarioStepConfigurationKeys.Retain;
    public const string SubscriptionsKey = ScenarioStepConfigurationKeys.Subscriptions;
    public const string ReceiveRetainedKey = ScenarioStepConfigurationKeys.ReceiveRetained;
    public const string RetainAsPublishedKey = ScenarioStepConfigurationKeys.RetainAsPublished;
    public const string EventTypeKey = ScenarioStepConfigurationKeys.EventType;
    public const string TopicStartsWithKey = ScenarioStepConfigurationKeys.TopicStartsWith;
    public const string TopicNotStartsWithKey = ScenarioStepConfigurationKeys.TopicNotStartsWith;
    public const string SubjectStartsWithKey = ScenarioStepConfigurationKeys.SubjectStartsWith;
    public const string StatusKey = ScenarioStepConfigurationKeys.Status;
    public const string SourceKey = ScenarioStepConfigurationKeys.Source;
    public const string PayloadContainsKey = ScenarioStepConfigurationKeys.PayloadContains;
    public const string TimeoutMsKey = ScenarioStepConfigurationKeys.TimeoutMs;

    public static readonly string QosAttributeKey = ScenarioStepConfigurationKeys.AttributeFilterKey("qos");
    public static readonly string RetainAttributeKey = ScenarioStepConfigurationKeys.AttributeFilterKey("retain");
    public static readonly string SchemaIdAttributeKey = ScenarioStepConfigurationKeys.AttributeFilterKey("schemaId");

    private static readonly IReadOnlyList<ScenarioStepFieldOption> PayloadEncodingOptions =
    [
        new("json", "JSON"),
        new("text", "Text"),
        new("base64", "Base64"),
        new("bytes", "Bytes")
    ];

    private static readonly IReadOnlyList<ScenarioStepFieldOption> QosOptions =
    [
        new("0", "0"),
        new("1", "1"),
        new("2", "2")
    ];

    private static readonly IReadOnlyList<ScenarioStepFieldOption> EventTypeOptions =
    [
        new(string.Empty, "Any event"),
        new(FlowEventTypes.MqttMessageReceived, "MQTT message received"),
        new(FlowEventTypes.MqttMessagePublished, "MQTT message published"),
        new(FlowEventTypes.MqttMessageRecorded, "MQTT message recorded"),
        new(FlowEventTypes.FileWritten, "File written"),
        new(FlowEventTypes.JsonSchemaValidated, "JSON schema validated"),
        new(FlowEventTypes.AssertionEvaluated, "Assertion evaluated")
    ];

    private static readonly IReadOnlyList<ScenarioStepFieldOption> EventStatusOptions =
    [
        new(string.Empty, "Any status"),
        new("received", "Received"),
        new("published", "Published"),
        new("recorded", "Recorded"),
        new("written", "Written"),
        new("valid", "Valid"),
        new("invalid", "Invalid"),
        new("passed", "Passed"),
        new("failed", "Failed")
    ];

    private static readonly IReadOnlyList<ScenarioStepFieldOption> ExpectQosOptions =
    [
        new(string.Empty, "Any"),
        new("0", "0"),
        new("1", "1"),
        new("2", "2")
    ];

    private static readonly IReadOnlyList<ScenarioStepFieldOption> ExpectRetainOptions =
    [
        new(string.Empty, "Any"),
        new("true", "Retain"),
        new("false", "No retain")
    ];

    private static readonly IReadOnlyList<ScenarioStepFieldDescriptor> EventExpectationFields =
    [
        new(EventTypeKey, "Event type", ScenarioStepFieldKind.Select, FlowEventTypes.MqttMessagePublished, EventTypeOptions),
        new(TopicStartsWithKey, "Topic prefix", ScenarioStepFieldKind.Text, string.Empty, []),
        new(TopicNotStartsWithKey, "Exclude topic prefix", ScenarioStepFieldKind.Text, string.Empty, []),
        new(SubjectStartsWithKey, "Subject prefix", ScenarioStepFieldKind.Text, string.Empty, []),
        new(StatusKey, "Status", ScenarioStepFieldKind.Select, "published", EventStatusOptions),
        new(SourceKey, "Source", ScenarioStepFieldKind.Text, string.Empty, []),
        new(PayloadContainsKey, "Payload contains", ScenarioStepFieldKind.Text, string.Empty, []),
        new(QosAttributeKey, "QoS", ScenarioStepFieldKind.Select, string.Empty, ExpectQosOptions),
        new(RetainAttributeKey, "Retain", ScenarioStepFieldKind.Select, string.Empty, ExpectRetainOptions),
        new(SchemaIdAttributeKey, "Schema id", ScenarioStepFieldKind.Text, string.Empty, []),
        new(TimeoutMsKey, "Timeout ms", ScenarioStepFieldKind.Text, "5000", [])
    ];

    public static ScenarioStepDefinitionCatalog Shared { get; } = new();

    private readonly IReadOnlyList<ScenarioStepDefinitionDescriptor> _steps =
    [
        new(
            ScenarioStepTypes.MqttPublisher,
            "MQTT publisher",
            "Action",
            "Publish a message through an app broker.",
            "publishMessage",
            [
                new(ConnectionKey, "Broker", ScenarioStepFieldKind.Connection, string.Empty, []),
                new(TopicKey, "Topic", ScenarioStepFieldKind.Text, "fluxmq/test", []),
                new(PayloadKey, "Payload", ScenarioStepFieldKind.MultilineText, """{"hello":"fluxmq"}""", [], 6),
                new(PayloadEncodingKey, "Payload encoding", ScenarioStepFieldKind.Select, "json", PayloadEncodingOptions),
                new(QosKey, "QoS", ScenarioStepFieldKind.Select, "0", QosOptions),
                new(RetainKey, "Retain", ScenarioStepFieldKind.CheckBox, "false", [])
            ]),
        new(
            ScenarioStepTypes.MqttTrigger,
            "MQTT trigger",
            "Action",
            "Listen for MQTT messages through an app broker.",
            "triggerMqtt",
            [
                new(ConnectionKey, "Broker", ScenarioStepFieldKind.Connection, string.Empty, []),
                new(SubscriptionsKey, "Topic filter", ScenarioStepFieldKind.Text, "fluxmq/test/#", []),
                new(QosKey, "QoS", ScenarioStepFieldKind.Select, "1", QosOptions),
                new(ReceiveRetainedKey, "Receive retained", ScenarioStepFieldKind.CheckBox, "false", []),
                new(RetainAsPublishedKey, "Retain as published", ScenarioStepFieldKind.CheckBox, "true", [])
            ]),
        new(
            ScenarioStepTypes.WhenEvent,
            "When event",
            "Condition",
            "Continue only when a scenario or app event matches configured filters.",
            "whenEvent",
            EventExpectationFields),
        new(
            ScenarioStepTypes.ExpectEvent,
            "Expect event",
            "Expectation",
            "Wait for a scenario or app event that matches configured filters.",
            "expectEvent",
            EventExpectationFields)
    ];

    public IReadOnlyList<ScenarioStepDefinitionDescriptor> Steps => _steps;

    public ScenarioStepDefinitionDescriptor? Find(string? type)
        => _steps.FirstOrDefault(step => string.Equals(step.Type, type, StringComparison.Ordinal));

    public ScenarioStepDefinitionDescriptor Describe(string? type)
        => Find(type) ?? new ScenarioStepDefinitionDescriptor(
            type ?? string.Empty,
            string.IsNullOrWhiteSpace(type) ? "Untyped" : type,
            "Custom",
            "Custom test step.",
            "step",
            []);

    public IReadOnlyDictionary<string, string> CreateDefaultConfiguration(
        string? type,
        string? defaultConnection = null)
    {
        var descriptor = Find(type);
        if (descriptor is null)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var configuration = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in descriptor.Fields)
        {
            configuration[field.Key] = field.Kind == ScenarioStepFieldKind.Connection &&
                                       !string.IsNullOrWhiteSpace(defaultConnection)
                ? defaultConnection
                : field.DefaultValue;
        }

        return configuration;
    }
}
