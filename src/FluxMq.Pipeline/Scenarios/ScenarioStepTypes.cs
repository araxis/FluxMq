namespace FluxMq.Pipeline.Scenarios;

public static class ScenarioStepTypes
{
    public const string MqttPublisher = "mqtt.publisher";
    public const string MqttTrigger = "mqtt.trigger";
    public const string ExpectEvent = "expect.event";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        MqttPublisher,
        MqttTrigger,
        ExpectEvent
    };
}
