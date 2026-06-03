using FluxMq.Core.Models;
using FluxFlow.Components.Secrets;
using FluxFlow.Components.Secrets.Contracts;
using MQTTnet;
using MQTTnet.Protocol;
using System.Threading.Channels;

namespace FluxMq.Core.Mqtt;

public sealed class MqttBrokerClient : IMqttBrokerClient
{
    private readonly IMqttClient _client;
    private readonly Channel<MqttEnvelope> _channel;
    private readonly ISecretResolver? _secretResolver;
    private volatile MqttClientState _state = MqttClientState.Disconnected;

    public MqttConnectionProfile Profile { get; }
    public MqttClientState State => _state;
    public ChannelReader<MqttEnvelope> Messages => _channel.Reader;

    public event EventHandler<MqttClientState>? StateChanged;

    public MqttBrokerClient(MqttConnectionProfile profile, ISecretResolver? secretResolver = null)
    {
        Profile = profile;
        _secretResolver = secretResolver;
        _channel = Channel.CreateBounded<MqttEnvelope>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleWriter = true
        });
        _client = new MqttClientFactory().CreateMqttClient();
        _client.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;
        _client.DisconnectedAsync += OnClientDisconnectedAsync;
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        SetState(MqttClientState.Connecting);
        try
        {
            var builder = new MqttClientOptionsBuilder()
                .WithTcpServer(Profile.Host, Profile.Port)
                .WithClientId(Profile.ClientId)
                .WithKeepAlivePeriod(Profile.KeepAlive)
                .WithCleanStart(Profile.CleanStart);

            var password = await ResolvePasswordAsync(ct).ConfigureAwait(false);
            if (Profile.Username is not null)
                builder = builder.WithCredentials(Profile.Username, password);

            if (Profile.UseTls)
                builder = builder.WithTlsOptions(o => o.UseTls());

            await _client.ConnectAsync(builder.Build(), ct);
            SetState(MqttClientState.Connected);
        }
        catch
        {
            SetState(MqttClientState.Faulted);
            throw;
        }
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        SetState(MqttClientState.Disconnecting);
        await _client.DisconnectAsync(cancellationToken: ct);
        SetState(MqttClientState.Disconnected);
        _channel.Writer.TryComplete();
    }

    public async Task SubscribeAsync(string topicFilter, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce, CancellationToken ct = default)
        => await SubscribeAsync(topicFilter, qos, receiveRetainedMessages: true, retainAsPublished: true, ct).ConfigureAwait(false);

    public async Task SubscribeAsync(
        string topicFilter,
        MqttQualityOfServiceLevel qos,
        bool receiveRetainedMessages,
        bool retainAsPublished = true,
        CancellationToken ct = default)
    {
        var options = new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter(f => f
                .WithTopic(topicFilter)
                .WithQualityOfServiceLevel(qos)
                .WithRetainHandling(receiveRetainedMessages
                    ? MqttRetainHandling.SendAtSubscribe
                    : MqttRetainHandling.DoNotSendOnSubscribe)
                .WithRetainAsPublished(retainAsPublished))
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

    private async ValueTask<string?> ResolvePasswordAsync(CancellationToken cancellationToken)
    {
        if (Profile.PasswordSecret is null)
        {
            return Profile.Password;
        }

        if (_secretResolver is null)
        {
            throw new InvalidOperationException(
                $"MQTT profile '{Profile.Name}' references 'passwordSecret' but no secret resolver is configured.");
        }

        var result = await SecretOptionResolver
            .ResolveRequiredAsync(_secretResolver, Profile.PasswordSecret, "profile.passwordSecret", cancellationToken)
            .ConfigureAwait(false);

        if (result.Resolved)
        {
            return result.Value.Reveal();
        }

        throw new InvalidOperationException(BuildSecretResolutionMessage(result));
    }

    private string BuildSecretResolutionMessage(SecretOptionResolution resolution)
        => resolution.Diagnostic is { } diagnostic
            ? $"MQTT profile '{Profile.Name}' password secret could not be resolved: {diagnostic.Message}"
            : $"MQTT profile '{Profile.Name}' password secret could not be resolved.";

    // Fired by MQTTnet on any disconnect — intentional or unexpected.
    // Reconnect policy (Polly) will hook here in a future step.
    private Task OnClientDisconnectedAsync(MqttClientDisconnectedEventArgs args)
    {
        if (_state is MqttClientState.Disconnecting or MqttClientState.Disconnected)
            return Task.CompletedTask;

        var next = args.Exception is not null
            ? MqttClientState.Faulted
            : MqttClientState.Disconnected;

        SetState(next);
        // Do NOT complete the channel here — reconnect will resume message flow.
        // Channel is only completed on intentional disconnect or dispose.
        return Task.CompletedTask;
    }

    private Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        var msg = args.ApplicationMessage;
        var seq = msg.Payload;
        using var ms = new MemoryStream((int)seq.Length);
        foreach (var segment in seq)
            ms.Write(segment.Span);

        _channel.Writer.TryWrite(new MqttEnvelope
        {
            Topic = msg.Topic,
            Payload = ms.ToArray(),
            QualityOfService = msg.QualityOfServiceLevel,
            Retain = msg.Retain
        });
        return Task.CompletedTask;
    }

    private void SetState(MqttClientState state)
    {
        _state = state;
        StateChanged?.Invoke(this, state);
    }

    public async ValueTask DisposeAsync()
    {
        _client.ApplicationMessageReceivedAsync -= OnMessageReceivedAsync;
        _client.DisconnectedAsync -= OnClientDisconnectedAsync;
        if (_client.IsConnected)
            await _client.DisconnectAsync();
        _client.Dispose();
        _channel.Writer.TryComplete();
    }
}
