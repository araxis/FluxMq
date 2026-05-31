using FluxMq.Core.Models;
using FluxMq.Core.Mqtt;
using MQTTnet.Protocol;
using System.Threading.Channels;

namespace FluxMq.Components.Tests.Components;

internal sealed class TestMqttBrokerClient : IMqttBrokerClient
{
    private readonly Channel<MqttEnvelope> _messages = Channel.CreateUnbounded<MqttEnvelope>();
    private readonly List<(string TopicFilter, MqttQualityOfServiceLevel QualityOfService)> _subscriptions = [];
    private readonly List<(string TopicFilter, MqttQualityOfServiceLevel QualityOfService, bool ReceiveRetainedMessages, bool RetainAsPublished)> _subscriptionOptions = [];
    private readonly Task? _connectDelay;

    public TestMqttBrokerClient(string profileName = "test", Task? connectDelay = null)
    {
        Profile = new MqttConnectionProfile { Name = profileName };
        _connectDelay = connectDelay;
    }

    public MqttConnectionProfile Profile { get; }
    public MqttClientState State { get; private set; } = MqttClientState.Disconnected;
    public ChannelReader<MqttEnvelope> Messages => _messages.Reader;
    public int ConnectCalls { get; private set; }
    public int DisposeCalls { get; private set; }
    public bool RequireConnectedForSubscribe { get; set; }
    public IReadOnlyList<(string TopicFilter, MqttQualityOfServiceLevel QualityOfService)> Subscriptions => _subscriptions;
    public IReadOnlyList<(string TopicFilter, MqttQualityOfServiceLevel QualityOfService, bool ReceiveRetainedMessages, bool RetainAsPublished)> SubscriptionOptions => _subscriptionOptions;

    public event EventHandler<MqttClientState>? StateChanged
    {
        add { }
        remove { }
    }

    public ValueTask WriteAsync(MqttEnvelope message) => _messages.Writer.WriteAsync(message);

    public void CompleteMessages(Exception? exception = null) => _messages.Writer.Complete(exception);

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        ConnectCalls++;
        State = MqttClientState.Connecting;
        if (_connectDelay is not null)
        {
            await _connectDelay.WaitAsync(ct);
        }

        State = MqttClientState.Connected;
    }

    public Task DisconnectAsync(CancellationToken ct = default)
    {
        State = MqttClientState.Disconnected;
        _messages.Writer.TryComplete();
        return Task.CompletedTask;
    }

    public Task SubscribeAsync(string topicFilter, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce, CancellationToken ct = default)
        => SubscribeAsync(topicFilter, qos, receiveRetainedMessages: true, retainAsPublished: true, ct);

    public Task SubscribeAsync(
        string topicFilter,
        MqttQualityOfServiceLevel qos,
        bool receiveRetainedMessages,
        bool retainAsPublished = true,
        CancellationToken ct = default)
    {
        if (RequireConnectedForSubscribe && State is not MqttClientState.Connected)
        {
            throw new InvalidOperationException("MQTT client is not connected.");
        }

        _subscriptions.Add((topicFilter, qos));
        _subscriptionOptions.Add((topicFilter, qos, receiveRetainedMessages, retainAsPublished));
        return Task.CompletedTask;
    }

    public Task UnsubscribeAsync(string topicFilter, CancellationToken ct = default) => Task.CompletedTask;
    public Task PublishAsync(string topic, byte[] payload, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce, bool retain = false, CancellationToken ct = default) => Task.CompletedTask;

    public ValueTask DisposeAsync()
    {
        DisposeCalls++;
        _messages.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }

    public static MqttEnvelope Message(string topic) => new()
    {
        Topic = topic,
        Payload = []
    };
}
