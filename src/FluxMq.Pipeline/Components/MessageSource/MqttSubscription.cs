using MQTTnet.Protocol;

namespace FluxMq.Pipeline.Components.MessageSource;

public sealed record MqttSubscription(string TopicFilter, MqttQualityOfServiceLevel QualityOfService);
