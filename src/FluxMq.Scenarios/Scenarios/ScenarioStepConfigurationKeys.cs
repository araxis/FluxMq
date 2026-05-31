namespace FluxMq.Scenarios;

public static class ScenarioStepConfigurationKeys
{
    public const string Connection = "connection";
    public const string Topic = "topic";
    public const string Payload = "payload";
    public const string PayloadEncoding = "payloadEncoding";
    public const string Qos = "qos";
    public const string Retain = "retain";
    public const string Subscriptions = "subscriptions";
    public const string ReceiveRetained = "receiveRetained";
    public const string RetainAsPublished = "retainAsPublished";

    public const string EventType = "eventType";
    public const string TopicStartsWith = "topicStartsWith";
    public const string SubjectStartsWith = "subjectStartsWith";
    public const string Status = "status";
    public const string Source = "source";
    public const string PayloadContains = "payloadContains";
    public const string Attributes = "attributes";
    public const string TimeoutMs = "timeoutMs";

    public const string AttributeKeyPrefix = "attribute:";

    public static string AttributeFilterKey(string attributeName)
        => $"{AttributeKeyPrefix}{attributeName}";

    public static bool TryGetAttributeName(string key, out string attributeName)
    {
        if (key.StartsWith(AttributeKeyPrefix, StringComparison.Ordinal) &&
            key.Length > AttributeKeyPrefix.Length)
        {
            attributeName = key[AttributeKeyPrefix.Length..];
            return true;
        }

        attributeName = string.Empty;
        return false;
    }
}
