namespace FluxMq.Scenarios;

public static class ScenarioStepTypes
{
    public const string MqttPublisher = "mqtt.publisher";
    public const string MqttTrigger = "mqtt.trigger";
    public const string WhenEvent = "when.event";
    public const string ExpectEvent = "expect.event";
    public const string WaitForEvent = "wait.event";
    public const string ConditionalEvent = "conditional.event";
    public const string PayloadAssertion = "assert.payload";
    public const string JsonSchemaAssertion = "assert.json.schema";
    public const string MetricThresholdAssertion = "assert.metric.threshold";
    public const string Delay = "delay.wait";
    public const string CleanupAction = "cleanup.action";
}
