using FluxMq.Core.Models;
using MQTTnet;
using MQTTnet.Protocol;
using System.Threading.Channels;

namespace FluxMq.Core.Session;

public sealed class MqttSession : IMqttSession
{
    private readonly IMqttClient _client;
    private readonly Channel<MqttEnvelope> _channel;
    private volatile MqttSessionState _state = MqttSessionState.Disconnected;

    public MqttConnectionProfile Profile { get; }
    public MqttSessionState State => _state;
    public ChannelReader<MqttEnvelope> Messages => _channel.Reader;

    public MqttSession(MqttConnectionProfile profile)
    {
        Profile = profile;
        _channel = Channel.CreateBounded<MqttEnvelope>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleWriter = true
        });
        _client = new MqttClientFactory().CreateMqttClient();
        _client.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        _state = MqttSessionState.Connecting;
        try
        {
            var builder = new MqttClientOptionsBuilder()
                .WithTcpServer(Profile.Host, Profile.Port)
                .WithClientId(Profile.ClientId)
                .WithKeepAlivePeriod(Profile.KeepAlive)
                .WithCleanStart(Profile.CleanStart);

            if (Profile.Username is not null)
                builder = builder.WithCredentials(Profile.Username, Profile.Password);

            if (Profile.UseTls)
                builder = builder.WithTlsOptions(o => o.UseTls());

            await _client.ConnectAsync(builder.Build(), ct);
            _state = MqttSessionState.Connected;
        }
        catch
        {
            _state = MqttSessionState.Faulted;
            throw;
        }
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        _state = MqttSessionState.Disconnecting;
        await _client.DisconnectAsync(cancellationToken: ct);
        _state = MqttSessionState.Disconnected;
        _channel.Writer.TryComplete();
    }

    public async Task SubscribeAsync(string topicFilter, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce, CancellationToken ct = default)
    {
        var options = new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter(f => f.WithTopic(topicFilter).WithQualityOfServiceLevel(qos))
            .Build();
        await _client.SubscribeAsync(options, ct);
    }

    public async Task UnsubscribeAsync(string topicFilter, CancellationToken ct = default)
    {
        var options = new MqttClientUnsubscribeOptionsBuilder()
            .WithTopicFilter(topicFilter)
            .Build();
        await _client.UnsubscribeAsync(options, ct);
    }

    public async Task PublishAsync(string topic, byte[] payload, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce, bool retain = false, CancellationToken ct = default)
    {
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(qos)
            .WithRetainFlag(retain)
            .Build();
        await _client.PublishAsync(message, ct);
    }

    private Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        var msg = args.ApplicationMessage;
        var seq = msg.Payload;
        using var ms = new MemoryStream((int)seq.Length);
        foreach (var segment in seq)
            ms.Write(segment.Span);
        var payload = ms.ToArray();

        var envelope = new MqttEnvelope
        {
            Topic = msg.Topic,
            Payload = payload,
            QualityOfService = msg.QualityOfServiceLevel,
            Retain = msg.Retain
        };
        _channel.Writer.TryWrite(envelope);
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _client.ApplicationMessageReceivedAsync -= OnMessageReceivedAsync;
        if (_client.IsConnected)
            await _client.DisconnectAsync();
        _client.Dispose();
        _channel.Writer.TryComplete();
    }
}
