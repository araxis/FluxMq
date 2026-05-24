using MQTTnet.Protocol;

namespace FluxMq.Components.MessageSource;

public sealed record MqttSubscription(
    string TopicFilter,
    MqttQualityOfServiceLevel QualityOfService,
    bool ReceiveRetainedMessages = true,
    bool RetainAsPublished = true);
