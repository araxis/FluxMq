using FluxFlow.Engine.Components;
using FluxMq.Scenarios;

namespace FluxMq.App.Metrics;

public sealed record FluxMetricSourceDescriptor(
    string Id,
    string Label,
    string Description);

public sealed record FluxMetricMeasureDescriptor(
    string Id,
    string Label,
    string Description,
    string Unit,
    string DefaultSource,
    string DefaultEventType,
    string DefaultFormat,
    IReadOnlySet<string> CompatibleSources,
    string Explanation,
    string Calculation,
    string BestFor);

public sealed record FluxMetricFilterOption(string Value, string Label);

public sealed record FluxMetricEventTypeDescriptor(
    string Value,
    string Label,
    IReadOnlyList<FluxMetricFilterFieldDescriptor> Fields,
    IReadOnlyList<FluxMetricFilterOption> StatusOptions);

public sealed record FluxMetricFilterFieldDescriptor(
    string Key,
    string Label,
    string? Placeholder,
    string HelperText,
    Func<FlowEvent, string?> ReadValue,
    string? AttributeName = null);

public sealed class FluxMetricCatalog
{
    public const string RuntimeEventsSource = "runtimeEvents";
    public const string TopicProjectionSource = "topicProjection";
    public const string MqttSnapshotsSource = "mqttSnapshots";
    public const string PayloadInspectionSource = "payloadInspection";

    public const string MeasureCount = "count";
    public const string MeasureRate = "rate";
    public const string MeasureTopics = "topics";
    public const string MeasurePayloadBytes = "payloadBytes";
    public const string MeasureAveragePayload = "averagePayload";
    public const string MeasureRetained = "retained";

    public const string FormatAuto = "auto";
    public const string FormatNumber = "number";
    public const string FormatBytes = "bytes";
    public const string FormatPercent = "percent";

    public const string EventTypeKey = "eventType";
    public const string TopicStartsWithKey = "topicStartsWith";
    public const string TopicNotStartsWithKey = ScenarioStepConfigurationKeys.TopicNotStartsWith;
    public const string SubjectStartsWithKey = "subjectStartsWith";
    public const string StatusKey = "status";

    private const string AnyValue = "";

    private static readonly IReadOnlyList<FluxMetricFilterOption> AllStatusOptions =
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

    public static FluxMetricCatalog Shared { get; } = new();

    public FluxMetricCatalog()
    {
        Sources =
        [
            new(RuntimeEventsSource, "Runtime events", "Events emitted by the active app runtime."),
            new(TopicProjectionSource, "Topic projection", "Topic and message activity derived from runtime events."),
            new(MqttSnapshotsSource, "MQTT snapshots", "Broker throughput, QoS, retain, and payload metrics."),
            new(PayloadInspectionSource, "Payload inspection", "Payload size and content inspection summaries.")
        ];

        Measures =
        [
            new(
                MeasureCount,
                "Count",
                "Matching event count",
                "events",
                RuntimeEventsSource,
                AnyValue,
                FormatNumber,
                Compatible(RuntimeEventsSource, TopicProjectionSource, MqttSnapshotsSource),
                "Counts the events that pass the selected match rules.",
                "Adds one for each matching event inside the selected window.",
                "Message totals, error totals, assertion totals, and simple health counters."),
            new(
                MeasureRate,
                "Rate",
                "Events per second",
                "/s",
                RuntimeEventsSource,
                AnyValue,
                FormatNumber,
                Compatible(RuntimeEventsSource, TopicProjectionSource, MqttSnapshotsSource),
                "Calculates event throughput over the selected time range.",
                "Divides matching events by the selected window length.",
                "Traffic speed, publish/receive throughput, and activity bursts."),
            new(
                MeasureTopics,
                "Topics",
                "Unique topics",
                "topics",
                TopicProjectionSource,
                AnyValue,
                FormatNumber,
                Compatible(TopicProjectionSource, RuntimeEventsSource),
                "Counts distinct topics represented by the matching traffic.",
                "Counts each distinct topic name once after match rules are applied.",
                "Topic spread, namespace coverage, and discovery checks."),
            new(
                MeasurePayloadBytes,
                "Payload bytes",
                "Total payload size",
                "bytes",
                PayloadInspectionSource,
                AnyValue,
                FormatBytes,
                Compatible(PayloadInspectionSource, RuntimeEventsSource),
                "Sums payload bytes for the matching messages.",
                "Adds payload sizes for all matching messages in the window.",
                "Bandwidth pressure, large transfer detection, and payload volume."),
            new(
                MeasureAveragePayload,
                "Average payload",
                "Average payload size",
                "bytes",
                PayloadInspectionSource,
                AnyValue,
                FormatBytes,
                Compatible(PayloadInspectionSource, RuntimeEventsSource),
                "Shows the average payload size for the matching messages.",
                "Divides total matching payload bytes by matching message count.",
                "Typical message size, payload drift, and unexpected growth."),
            new(
                MeasureRetained,
                "Retained",
                "Retained messages",
                "messages",
                MqttSnapshotsSource,
                AnyValue,
                FormatNumber,
                Compatible(MqttSnapshotsSource, RuntimeEventsSource),
                "Counts matching messages with the retain flag set.",
                "Counts matching messages where retain is true.",
                "Retained-message behavior, retained topic checks, and broker hygiene.")
        ];

        EventTypes =
        [
            new(AnyValue, "All runtime events", [], [new(AnyValue, "Any status")]),
            new(FlowEventTypes.MqttMessageReceived, "MQTT message received", MqttEventFields("received"), StatusOptions("received")),
            new(FlowEventTypes.MqttMessagePublished, "MQTT message published", MqttEventFields("published"), StatusOptions("published")),
            new(FlowEventTypes.MqttMessageRecorded, "MQTT message recorded", MqttEventFields("recorded"), StatusOptions("recorded")),
            new(FlowEventTypes.FileWritten, "File written", [SubjectField("File path", "logs/", "Filters by written file path prefix.")], StatusOptions("written")),
            new(FlowEventTypes.JsonSchemaValidated, "JSON schema validated", [
                TopicField("Topic prefix", "factory/line-a/", "Filters by validated message topic."),
                AttributeField("schemaId", "Schema id", "temperature", "Filters by schema id.")
            ], StatusOptions("valid", "invalid")),
            new(FlowEventTypes.AssertionEvaluated, "Assertion evaluated", [
                SubjectField("Assertion", null, "Filters by assertion name.")
            ], StatusOptions("passed", "failed"))
        ];

        FilterKeys = EventTypes
            .SelectMany(static eventType => eventType.Fields)
            .Select(static field => field.Key)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<FluxMetricSourceDescriptor> Sources { get; }

    public IReadOnlyList<FluxMetricMeasureDescriptor> Measures { get; }

    public IReadOnlyList<FluxMetricEventTypeDescriptor> EventTypes { get; }

    public IReadOnlyList<string> FilterKeys { get; }

    public FluxMetricSourceDescriptor? FindSource(string? source)
        => Sources.FirstOrDefault(item => string.Equals(item.Id, source, StringComparison.Ordinal));

    public FluxMetricMeasureDescriptor FindMeasure(string? measure)
        => Measures.FirstOrDefault(item => string.Equals(item.Id, measure, StringComparison.Ordinal)) ?? Measures[0];

    public FluxMetricEventTypeDescriptor FindEventType(string? eventType)
        => EventTypes.FirstOrDefault(option => string.Equals(option.Value, eventType ?? AnyValue, StringComparison.Ordinal)) ??
           EventTypes[0];

    public bool IsSourceCompatible(string? measure, string? source)
        => !string.IsNullOrWhiteSpace(source) &&
           FindMeasure(measure).CompatibleSources.Contains(source);

    public string NormalizeSource(string? measure, string? source)
    {
        var descriptor = FindMeasure(measure);
        return IsSourceCompatible(descriptor.Id, source)
            ? source!.Trim()
            : descriptor.DefaultSource;
    }

    public string NormalizeFormat(string? measure, string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return FindMeasure(measure).DefaultFormat;
        }

        return format.Trim() switch
        {
            FormatAuto or FormatNumber or FormatBytes or FormatPercent => format.Trim(),
            _ => FindMeasure(measure).DefaultFormat
        };
    }

    public FluxMetricDefinition CreateDefaultDefinition(string name)
    {
        var measure = FindMeasure(MeasureCount);
        return new FluxMetricDefinition(
            name,
            measure.DefaultSource,
            measure.Id,
            "60s",
            format: measure.DefaultFormat);
    }

    public static string AttributeFilterKey(string attributeName)
        => ScenarioStepConfigurationKeys.AttributeFilterKey(attributeName);

    public static bool TryGetAttributeName(string key, out string attributeName)
        => ScenarioStepConfigurationKeys.TryGetAttributeName(key, out attributeName);

    public static bool ShouldExposeStatus(FluxMetricEventTypeDescriptor eventType)
    {
        ArgumentNullException.ThrowIfNull(eventType);

        return eventType.StatusOptions.Count(static option => !string.IsNullOrWhiteSpace(option.Value)) > 1;
    }

    public static bool IsRedundantStatus(FluxMetricEventTypeDescriptor eventType, string? status)
    {
        ArgumentNullException.ThrowIfNull(eventType);

        return !string.IsNullOrWhiteSpace(status) &&
               !ShouldExposeStatus(eventType) &&
               eventType.StatusOptions.Any(option => string.Equals(option.Value, status.Trim(), StringComparison.Ordinal));
    }

    public bool Matches(FluxMetricDefinition definition, FlowEvent flowEvent)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(flowEvent);

        if (!string.IsNullOrWhiteSpace(definition.EventType) &&
            !string.Equals(flowEvent.Type, definition.EventType, StringComparison.Ordinal))
        {
            return false;
        }

        var descriptor = FindEventType(definition.EventType);
        if (!MatchesField(TopicStartsWithKey, definition.TopicStartsWith, descriptor, flowEvent))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(definition.TopicNotStartsWith) &&
            StartsWith(flowEvent.Channel, definition.TopicNotStartsWith))
        {
            return false;
        }

        foreach (var (key, value) in definition.AdditionalFilters)
        {
            if (!MatchesField(key, value, descriptor, flowEvent))
            {
                return false;
            }
        }

        return string.IsNullOrWhiteSpace(definition.Status) ||
               string.Equals(flowEvent.Status, definition.Status, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesField(
        string key,
        string? expected,
        FluxMetricEventTypeDescriptor descriptor,
        FlowEvent flowEvent)
    {
        if (string.IsNullOrWhiteSpace(expected))
        {
            return true;
        }

        var field = descriptor.Fields.FirstOrDefault(field => string.Equals(field.Key, key, StringComparison.Ordinal));
        var actual = field?.ReadValue(flowEvent) ?? ReadFallbackValue(key, flowEvent);
        return StartsWith(actual, expected);
    }

    private static string? ReadFallbackValue(string key, FlowEvent flowEvent)
    {
        if (string.Equals(key, TopicStartsWithKey, StringComparison.Ordinal) ||
            string.Equals(key, TopicNotStartsWithKey, StringComparison.Ordinal))
        {
            return flowEvent.Channel;
        }

        if (string.Equals(key, SubjectStartsWithKey, StringComparison.Ordinal))
        {
            return flowEvent.Subject;
        }

        return TryGetAttributeName(key, out var attributeName)
            ? flowEvent.GetAttribute(attributeName)
            : null;
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

    private static IReadOnlySet<string> Compatible(params string[] sourceIds)
        => new HashSet<string>(sourceIds, StringComparer.Ordinal);

    private static DashboardEventFieldList MqttEventFields(string eventVerb)
        =>
        [
            TopicField("Topic prefix", "factory/line-a/", $"Filters by {eventVerb} message topic."),
            TopicExcludeField("Exclude topic prefix", "$SYS/", $"Excludes {eventVerb} messages whose topic starts with this prefix."),
            AttributeField("qos", "QoS", "1", "Filters by MQTT QoS."),
            AttributeField("retain", "Retain", "false", "Filters by MQTT retain flag.")
        ];

    private static FluxMetricFilterFieldDescriptor TopicField(string label, string? placeholder, string helperText)
        => new(TopicStartsWithKey, label, placeholder, helperText, static flowEvent => flowEvent.Channel);

    private static FluxMetricFilterFieldDescriptor SubjectField(string label, string? placeholder, string helperText)
        => new(SubjectStartsWithKey, label, placeholder, helperText, static flowEvent => flowEvent.Subject);

    private static FluxMetricFilterFieldDescriptor TopicExcludeField(string label, string? placeholder, string helperText)
        => new(TopicNotStartsWithKey, label, placeholder, helperText, static flowEvent => flowEvent.Channel);

    private static FluxMetricFilterFieldDescriptor AttributeField(
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

    private static IReadOnlyList<FluxMetricFilterOption> StatusOptions(params string[] statuses)
        => [new(AnyValue, "Any status"), .. AllStatusOptions.Where(option => statuses.Contains(option.Value, StringComparer.Ordinal))];

    private sealed class DashboardEventFieldList : List<FluxMetricFilterFieldDescriptor>
    {
        public static implicit operator DashboardEventFieldList(FluxMetricFilterFieldDescriptor[] fields)
            => [.. fields];
    }
}
