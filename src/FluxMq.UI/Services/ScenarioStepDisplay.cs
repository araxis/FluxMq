using FluxMq.Scenarios;
using FluxMq.UI.Models;

namespace FluxMq.UI.Services;

public static class ScenarioStepDisplay
{
    public static string StepCardClass(ScenarioStepRunStatus? status)
        => status switch
        {
            ScenarioStepRunStatus.Passed => "test-step-card passed",
            ScenarioStepRunStatus.Skipped => "test-step-card skipped",
            ScenarioStepRunStatus.Failed => "test-step-card failed",
            ScenarioStepRunStatus.TimedOut => "test-step-card timed-out",
            ScenarioStepRunStatus.Canceled => "test-step-card canceled",
            _ => "test-step-card"
        };

    public static string StepTypeClass(ScenarioStepSnapshot step, ScenarioStepCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(catalog);

        return step.Type switch
        {
            ScenarioStepTypes.MqttPublisher => "test-step-type publish",
            ScenarioStepTypes.MqttTrigger => "test-step-type trigger",
            ScenarioStepTypes.WhenEvent => "test-step-type when",
            ScenarioStepTypes.ExpectEvent => "test-step-type expect",
            _ => catalog.Find(step.Type)?.EditorKind switch
            {
                ScenarioStepEditorKind.MqttPublish => "test-step-type publish",
                ScenarioStepEditorKind.MqttTrigger => "test-step-type trigger",
                ScenarioStepEditorKind.ExpectEvent => "test-step-type expect",
                _ => "test-step-type"
            }
        };
    }

    public static string StepStatusClass(ScenarioStepRunStatus status)
        => status switch
        {
            ScenarioStepRunStatus.Passed => "test-step-state passed",
            ScenarioStepRunStatus.Skipped => "test-step-state skipped",
            ScenarioStepRunStatus.Failed => "test-step-state failed",
            ScenarioStepRunStatus.TimedOut => "test-step-state timed-out",
            ScenarioStepRunStatus.Canceled => "test-step-state canceled",
            _ => "test-step-state"
        };

    public static string BuildStepSummary(ScenarioStepSnapshot step, ScenarioStepCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(catalog);

        var descriptor = catalog.Find(step.Type);
        if (descriptor is null)
        {
            return string.IsNullOrWhiteSpace(step.Type) ? "No step type" : step.Type;
        }

        return step.Type switch
        {
            ScenarioStepTypes.MqttPublisher => PublishSummary(step),
            ScenarioStepTypes.MqttTrigger => TriggerSummary(step),
            ScenarioStepTypes.WhenEvent or ScenarioStepTypes.ExpectEvent => EventFilterSummary(step),
            _ => descriptor.EditorKind switch
            {
                ScenarioStepEditorKind.MqttPublish => PublishSummary(step),
                ScenarioStepEditorKind.MqttTrigger => TriggerSummary(step),
                ScenarioStepEditorKind.ExpectEvent => EventFilterSummary(step),
                _ => string.IsNullOrWhiteSpace(step.Type) ? "No step type" : step.Type
            }
        };
    }

    public static IEnumerable<KeyValuePair<string, string>> VisibleConfiguration(
        ScenarioStepSnapshot step,
        ScenarioStepCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(catalog);

        var descriptor = catalog.Find(step.Type);
        if (descriptor is null)
        {
            foreach (var item in step.Configuration.Where(static item => !string.IsNullOrWhiteSpace(item.Value)))
            {
                yield return item;
            }

            yield break;
        }

        var handledKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in descriptor.Fields)
        {
            handledKeys.Add(field.Key);
            if (step.Configuration.TryGetValue(field.Key, out var value) &&
                !string.IsNullOrWhiteSpace(value))
            {
                yield return new KeyValuePair<string, string>(field.Key, value);
            }
        }

        foreach (var item in step.Configuration.Where(item =>
                     !handledKeys.Contains(item.Key) &&
                     !string.IsNullOrWhiteSpace(item.Value)))
        {
            yield return item;
        }
    }

    public static string FormatConfigurationKey(
        ScenarioStepSnapshot step,
        string key,
        ScenarioStepCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(catalog);

        var field = catalog.Find(step.Type)?.Fields
            .FirstOrDefault(field => string.Equals(field.Key, key, StringComparison.Ordinal));

        return field?.Label ?? FormatUnknownConfigurationKey(key);
    }

    private static string FormatUnknownConfigurationKey(string key)
    {
        if (DashboardEventFilterCatalog.TryGetAttributeName(key, out var attributeName))
        {
            return attributeName switch
            {
                "qos" => "QoS",
                "retain" => "retain",
                _ => attributeName
            };
        }

        return key switch
        {
            ScenarioStepConfigurationKeys.EventType => "event type",
            ScenarioStepConfigurationKeys.TopicStartsWith => "topic prefix",
            ScenarioStepConfigurationKeys.SubjectStartsWith => "subject prefix",
            ScenarioStepConfigurationKeys.PayloadContains => "payload contains",
            ScenarioStepConfigurationKeys.PayloadEncoding => "payload encoding",
            ScenarioStepConfigurationKeys.TimeoutMs => "timeout ms",
            ScenarioStepConfigurationKeys.Subscriptions => "topic filter",
            ScenarioStepConfigurationKeys.ReceiveRetained => "receive retained",
            ScenarioStepConfigurationKeys.RetainAsPublished => "retain as published",
            ScenarioStepConfigurationKeys.Qos => "QoS",
            _ => key
        };
    }

    private static string PublishSummary(ScenarioStepSnapshot step)
        => JoinParts(
            step.ReadString(ScenarioStepConfigurationKeys.Connection),
            step.ReadString(ScenarioStepConfigurationKeys.Topic),
            FormatQos(step.ReadString(ScenarioStepConfigurationKeys.Qos)),
            FormatRetain(step.ReadString(ScenarioStepConfigurationKeys.Retain)));

    private static string TriggerSummary(ScenarioStepSnapshot step)
        => JoinParts(
            step.ReadString(ScenarioStepConfigurationKeys.Connection),
            PrefixSummary("topic", step.ReadString(ScenarioStepConfigurationKeys.Subscriptions)),
            FormatQos(step.ReadString(ScenarioStepConfigurationKeys.Qos)),
            ReceiveRetainedSummary(step.ReadString(ScenarioStepConfigurationKeys.ReceiveRetained)),
            RetainAsPublishedSummary(step.ReadString(ScenarioStepConfigurationKeys.RetainAsPublished)));

    private static string EventFilterSummary(ScenarioStepSnapshot step)
        => JoinParts(
            step.ReadString(ScenarioStepConfigurationKeys.EventType),
            PrefixSummary("topic", step.ReadString(ScenarioStepConfigurationKeys.TopicStartsWith)),
            PrefixSummary("subject", step.ReadString(ScenarioStepConfigurationKeys.SubjectStartsWith)),
            PrefixSummary("QoS", step.ReadString(DashboardEventFilterCatalog.AttributeFilterKey("qos"))),
            RetainExpectationSummary(step.ReadString(DashboardEventFilterCatalog.AttributeFilterKey("retain"))),
            PrefixSummary("schema", step.ReadString(DashboardEventFilterCatalog.AttributeFilterKey("schemaId"))),
            StatusSummary(step.ReadString(ScenarioStepConfigurationKeys.Status)),
            TimeoutSummary(step.ReadString(ScenarioStepConfigurationKeys.TimeoutMs)));

    private static string JoinParts(params string?[] parts)
    {
        var value = string.Join(" / ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        return string.IsNullOrWhiteSpace(value) ? "No summary" : value;
    }

    private static string? PrefixSummary(string label, string? value)
        => string.IsNullOrWhiteSpace(value) ? null : $"{label}: {value}";

    private static string? StatusSummary(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : $"status: {value}";

    private static string? TimeoutSummary(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : $"{value} ms";

    private static string? FormatQos(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : $"QoS {value}";

    private static string? FormatRetain(string? value)
        => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            ? "retain"
            : null;

    private static string? ReceiveRetainedSummary(string? value)
        => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            ? "receive retained"
            : null;

    private static string? RetainAsPublishedSummary(string? value)
        => string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)
            ? "clear retain flag"
            : null;

    private static string? RetainExpectationSummary(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return bool.TryParse(value, out var retain) && retain
            ? "retain"
            : "no retain";
    }
}
