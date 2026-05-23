namespace FluxMq.Components.MqttPublisher;

public sealed record MqttPublishRequestMapDefinition
{
    public string? Expression { get; init; }
    public string? TopicExpression { get; init; }
    public string? PayloadExpression { get; init; }
    public string? QualityOfServiceExpression { get; init; }
    public string? RetainExpression { get; init; }
}
