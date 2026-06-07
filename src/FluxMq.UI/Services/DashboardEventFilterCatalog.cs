using FluxFlow.Engine.Components;
using FluxMq.Scenarios;
using FluxMq.UI.Models;

namespace FluxMq.UI.Services;

public sealed class DashboardEventFilterCatalog
{
    public const string EventTypeKey = "eventType";
    public const string TopicStartsWithKey = "topicStartsWith";
    public const string TopicNotStartsWithKey = ScenarioStepConfigurationKeys.TopicNotStartsWith;
    public const string SubjectStartsWithKey = "subjectStartsWith";
    public const string StatusKey = "status";
    public const string AttributeKeyPrefix = ScenarioStepConfigurationKeys.AttributeKeyPrefix;

    private const string AnyValue = "";

    private static readonly IReadOnlyList<DashboardEventFilterOption> AllStatusOptions =
    [
        new(AnyValue, "Any status"),
        new("received", "Received"),
        new("published", "Published"),
        new("recorded", "Recorded"),
        new("written", "Written"),
        new("valid", "Valid"),
        new("invalid", "Invalid"),
        new("passed", "Passed"),
        new("failed", "Failed")
    ];

    public static DashboardEventFilterCatalog Shared { get; } = new();

    public DashboardEventFilterCatalog()
    {
        EventTypes =
        [
            new(AnyValue, "All runtime events", [], [new(AnyValue, "Any status")]),
            new(FluxMqEventTypes.MqttMessageReceived, "MQTT message received", MqttEventFields("received"), StatusOptions("received")),
            new(FluxMqEventTypes.MqttMessagePublished, "MQTT message published", MqttEventFields("published"), StatusOptions("published")),
            new(FluxMqEventTypes.MqttMessageRecorded, "MQTT message recorded", MqttEventFields("recorded"), StatusOptions("recorded")),
            new(FluxMqEventTypes.FileWritten, "File written", [SubjectField("File path", "logs/", "Filters by written file path prefix.")], StatusOptions("written")),
            new(FluxMqEventTypes.JsonSchemaValidated, "JSON schema validated", [
                TopicField("Topic prefix", "factory/line-a/", "Filters by validated message topic."),
                AttributeField("schemaId", "Schema id", "temperature", "Filters by schema id.")
            ], StatusOptions("valid", "invalid")),
            new(FluxMqEventTypes.AssertionEvaluated, "Assertion evaluated", [
                SubjectField("Assertion", null, "Filters by assertion name.")
            ], StatusOptions("passed", "failed"))
        ];

        FilterKeys = EventTypes
            .SelectMany(static eventType => eventType.Fields)
            .Select(static field => field.Key)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<DashboardEventTypeDescriptor> EventTypes { get; }

    public IReadOnlyList<string> FilterKeys { get; }

    public DashboardEventTypeDescriptor Find(string? eventType)
        => EventTypes.FirstOrDefault(option => string.Equals(option.Value, eventType ?? AnyValue, StringComparison.Ordinal)) ??
           EventTypes[0];

    public static bool ShouldExposeStatus(DashboardEventTypeDescriptor eventType)
    {
        ArgumentNullException.ThrowIfNull(eventType);

        return eventType.StatusOptions.Count(static option => !string.IsNullOrWhiteSpace(option.Value)) > 1;
    }

    public static bool IsRedundantStatus(DashboardEventTypeDescriptor eventType, string? status)
    {
        ArgumentNullException.ThrowIfNull(eventType);

        return !string.IsNullOrWhiteSpace(status) &&
               !ShouldExposeStatus(eventType) &&
               eventType.StatusOptions.Any(option => string.Equals(option.Value, status.Trim(), StringComparison.Ordinal));
    }

    public static string AttributeFilterKey(string attributeName)
        => ScenarioStepConfigurationKeys.AttributeFilterKey(attributeName);

    public static bool TryGetAttributeName(string key, out string attributeName)
        => ScenarioStepConfigurationKeys.TryGetAttributeName(key, out attributeName);

    public IReadOnlyDictionary<string, string> CreateEmptyConfiguration()
    {
        var configuration = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [EventTypeKey] = string.Empty,
            [StatusKey] = string.Empty
        };

        foreach (var key in FilterKeys)
        {
            configuration[key] = string.Empty;
        }

        return configuration;
    }

    public bool Matches(DashboardWidgetSnapshot widget, FlowEvent flowEvent)
    {
        var eventType = widget.ReadString(EventTypeKey);
        if (!string.IsNullOrWhiteSpace(eventType) &&
            !string.Equals(flowEvent.Type, eventType, StringComparison.Ordinal))
        {
            return false;
        }

        var descriptor = Find(eventType);
        foreach (var field in descriptor.Fields)
        {
            var expectedPrefix = widget.ReadString(field.Key);
            if (string.IsNullOrWhiteSpace(expectedPrefix))
            {
                continue;
            }

            var actual = field.ReadValue(flowEvent);
            if (string.Equals(field.Key, TopicNotStartsWithKey, StringComparison.Ordinal))
            {
                if (StartsWith(actual, expectedPrefix))
                {
                    return false;
                }

                continue;
            }

            if (!StartsWith(actual, expectedPrefix))
            {
                return false;
            }
        }

        var status = widget.ReadString(StatusKey);
        return string.IsNullOrWhiteSpace(status) ||
               string.Equals(flowEvent.Status, status, StringComparison.OrdinalIgnoreCase);
    }

    private static bool StartsWith(string? actual, string expectedPrefix)
    {
        if (bool.TryParse(actual, out var actualBoolean) &&
            bool.TryParse(expectedPrefix, out var expectedBoolean))
        {
            return actualBoolean == expectedBoolean;
        }

        return !string.IsNullOrWhiteSpace(actual) &&
               actual.StartsWith(expectedPrefix, StringComparison.Ordinal);
    }

    private static DashboardEventFilterFieldDescriptor TopicField(string label, string? placeholder, string helperText)
        => new(TopicStartsWithKey, label, placeholder, helperText, static flowEvent => flowEvent.Channel);

    private static IReadOnlyList<DashboardEventFilterFieldDescriptor> MqttEventFields(string eventVerb)
        =>
        [
            TopicField("Topic prefix", "factory/line-a/", $"Filters by {eventVerb} message topic."),
            TopicExcludeField("Exclude topic prefix", "$SYS/", $"Excludes {eventVerb} messages whose topic starts with this prefix."),
            AttributeField("qos", "QoS", "1", "Filters by MQTT QoS."),
            AttributeField("retain", "Retain", "false", "Filters by MQTT retain flag.")
        ];

    private static DashboardEventFilterFieldDescriptor SubjectField(string label, string? placeholder, string helperText)
        => new(SubjectStartsWithKey, label, placeholder, helperText, static flowEvent => flowEvent.Subject);

    private static DashboardEventFilterFieldDescriptor TopicExcludeField(string label, string? placeholder, string helperText)
        => new(TopicNotStartsWithKey, label, placeholder, helperText, static flowEvent => flowEvent.Channel);

    private static DashboardEventFilterFieldDescriptor AttributeField(
        string attributeName,
        string label,
        string? placeholder,
        string helperText)
        => new(
            AttributeFilterKey(attributeName),
            label,
            placeholder,
            helperText,
            flowEvent => flowEvent.GetAttribute(attributeName),
            attributeName);

    private static IReadOnlyList<DashboardEventFilterOption> StatusOptions(params string[] statuses)
        => [new(AnyValue, "Any status"), .. AllStatusOptions.Where(option => statuses.Contains(option.Value, StringComparer.Ordinal))];
}
