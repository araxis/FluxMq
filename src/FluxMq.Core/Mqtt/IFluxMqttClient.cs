using FluxMq.Core.Models;
using MQTTnet.Protocol;
using System.Threading.Channels;

namespace FluxMq.Core.Mqtt;

public interface IFluxMqttClient : IAsyncDisposable
{
    MqttConnectionProfile Profile { get; }
    MqttClientState State { get; }
    ChannelReader<MqttEnvelope> Messages { get; }

    event EventHandler<MqttClientState>? StateChanged;

    Task ConnectAsync(CancellationToken ct = default);
    Task DisconnectAsync(CancellationToken ct = default);
    Task SubscribeAsync(string topicFilter, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce, CancellationToken ct = default);
    Task SubscribeAsync(
        string topicFilter,
        MqttQualityOfServiceLevel qos,
        bool receiveRetainedMessages,
        bool retainAsPublished = true,
        CancellationToken ct = default);
    Task UnsubscribeAsync(string topicFilter, CancellationToken ct = default);
    Task PublishAsync(string topic, byte[] payload, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce, bool retain = false, CancellationToken ct = default);
}
