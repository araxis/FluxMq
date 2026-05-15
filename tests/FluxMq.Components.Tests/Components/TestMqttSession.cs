using FluxMq.Core.Models;
using FluxMq.Core.Session;
using MQTTnet.Protocol;
using System.Threading.Channels;

namespace FluxMq.Components.Tests.Components;

internal sealed class TestMqttSession : IMqttSession
{
    private readonly Channel<MqttEnvelope> _messages = Channel.CreateUnbounded<MqttEnvelope>();
    private readonly List<(string TopicFilter, MqttQualityOfServiceLevel QualityOfService)> _subscriptions = [];

    public TestMqttSession(string profileName = "test")
    {
        Profile = new MqttConnectionProfile { Name = profileName };
    }

    public MqttConnectionProfile Profile { get; }
    public MqttSessionState State { get; private set; } = MqttSessionState.Disconnected;
    public ChannelReader<MqttEnvelope> Messages => _messages.Reader;
    public int ConnectCalls { get; private set; }
    public int DisposeCalls { get; private set; }
    public IReadOnlyList<(string TopicFilter, MqttQualityOfServiceLevel QualityOfService)> Subscriptions => _subscriptions;

    public event EventHandler<MqttSessionState>? StateChanged
    {
        add { }
        remove { }
    }

    public ValueTask WriteAsync(MqttEnvelope message) => _messages.Writer.WriteAsync(message);

    public void CompleteMessages(Exception? exception = null) => _messages.Writer.Complete(exception);

    public Task ConnectAsync(CancellationToken ct = default)
    {
        ConnectCalls++;
        State = MqttSessionState.Connected;
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken ct = default)
    {
        State = MqttSessionState.Disconnected;
        _messages.Writer.TryComplete();
        return Task.CompletedTask;
    }

    public Task SubscribeAsync(string topicFilter, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce, CancellationToken ct = default)
    {
        _subscriptions.Add((topicFilter, qos));
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
