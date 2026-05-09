using MQTTnet.Protocol;

namespace FluxMq.Pipeline.Components;

public sealed record MqttSubscription(string TopicFilter, MqttQualityOfServiceLevel QualityOfService);
