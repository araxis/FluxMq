namespace FluxMq.Pipeline.Scenarios;

public static class ScenarioStepTypes
{
    public const string MqttPublisher = "mqtt.publisher";
    public const string MqttPublish = "mqtt.publish";
    public const string ExpectEvent = "expect.event";

    public static string? ToCanonical(string? type)
        => string.Equals(type, MqttPublish, StringComparison.Ordinal)
            ? MqttPublisher
            : type;
}
