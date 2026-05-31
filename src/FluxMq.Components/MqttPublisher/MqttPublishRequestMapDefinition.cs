namespace FluxMq.Components.MqttPublisher;

public sealed record MqttPublishRequestMapDefinition
{
    public required string Expression { get; init; }
}
