using FluxMq.Pipeline.Components;
using FluxMq.UI.Models;

namespace FluxMq.UI.Services;

public sealed class DashboardEventFilterCatalog
{
    public const string EventTypeKey = "eventType";
    public const string TopicStartsWithKey = "topicStartsWith";
    public const string SubjectStartsWithKey = "subjectStartsWith";
    public const string StatusKey = "status";

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
            new(AnyValue, "Any event", [], AllStatusOptions),
            new(FlowEventTypes.MqttMessageReceived, "MQTT message received", [TopicField("Topic prefix", "factory/line-a/", "Filters by received message topic.")], StatusOptions("received")),
            new(FlowEventTypes.MqttMessagePublished, "MQTT message published", [TopicField("Topic prefix", "factory/line-a/", "Filters by published message topic.")], StatusOptions("published")),
            new(FlowEventTypes.MqttMessageRecorded, "MQTT message recorded", [TopicField("Topic prefix", "factory/line-a/", "Filters by recorded message topic.")], StatusOptions("recorded")),
            new(FlowEventTypes.FileWritten, "File written", [SubjectField("Path prefix", null, "Filters by written file path.")], StatusOptions("written")),
            new(FlowEventTypes.JsonSchemaValidated, "JSON schema validated", [TopicField("Topic prefix", "factory/line-a/", "Filters by validated message topic.")], StatusOptions("valid", "invalid")),
            new(FlowEventTypes.AssertionEvaluated, "Assertion evaluated", [
                TopicField("Topic prefix", "factory/line-a/", "Filters by assertion event topic when present."),
                SubjectField("Assertion name prefix", null, "Filters by assertion name.")
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
            if (!string.IsNullOrWhiteSpace(expectedPrefix) &&
                !StartsWith(field.ReadValue(flowEvent), expectedPrefix))
            {
                return false;
            }
        }

        var status = widget.ReadString(StatusKey);
        return string.IsNullOrWhiteSpace(status) ||
               string.Equals(flowEvent.Status, status, StringComparison.OrdinalIgnoreCase);
    }

    private static bool StartsWith(string? actual, string expectedPrefix)
        => !string.IsNullOrWhiteSpace(actual) &&
           actual.StartsWith(expectedPrefix, StringComparison.Ordinal);

    private static DashboardEventFilterFieldDescriptor TopicField(string label, string? placeholder, string helperText)
        => new(TopicStartsWithKey, label, placeholder, helperText, static flowEvent => flowEvent.Topic);

    private static DashboardEventFilterFieldDescriptor SubjectField(string label, string? placeholder, string helperText)
        => new(SubjectStartsWithKey, label, placeholder, helperText, static flowEvent => flowEvent.Subject);

    private static IReadOnlyList<DashboardEventFilterOption> StatusOptions(params string[] statuses)
        => [new(AnyValue, "Any status"), .. AllStatusOptions.Where(option => statuses.Contains(option.Value, StringComparer.Ordinal))];
}
