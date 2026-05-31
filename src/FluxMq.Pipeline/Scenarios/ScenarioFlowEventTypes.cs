namespace FluxMq.Pipeline.Scenarios;

public static class FlowEventTypes
{
    public const string MqttMessageReceived = "mqtt.message.received";
    public const string MqttMessagePublished = "mqtt.message.published";
    public const string MqttMessageRecorded = "mqtt.message.recorded";
    public const string FileWritten = "file.written";
    public const string JsonSchemaValidated = "json.schema.validated";
    public const string AssertionEvaluated = "flow.assertion.evaluated";
}
