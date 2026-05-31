using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using Polly;
using Polly.Retry;
using System.Collections.Concurrent;

namespace FluxMq.Core.Mqtt;

public sealed class MqttConnectionManager : IMqttConnectionManager
{
    private readonly Func<MqttConnectionProfile, IMqttBrokerClient> _clientFactory;
    private readonly ResiliencePipeline _reconnectPipeline;
    private readonly ConcurrentDictionary<ConnectionProfileId, IMqttBrokerClient> _clients = new();
    private readonly ConcurrentDictionary<ConnectionProfileId, CancellationTokenSource> _reconnectCts = new();

    public IReadOnlyDictionary<ConnectionProfileId, IMqttBrokerClient> Clients => _clients;

    public event EventHandler<MqttClientStateChangedEventArgs>? StateChanged;

    public MqttConnectionManager(
        Func<MqttConnectionProfile, IMqttBrokerClient>? clientFactory = null,
        ResiliencePipeline? reconnectPipeline = null)
    {
        _clientFactory = clientFactory ?? (profile => new MqttBrokerClient(profile));
        _reconnectPipeline = reconnectPipeline ?? BuildDefaultReconnectPipeline();
    }

    public async Task<IMqttBrokerClient> ConnectAsync(MqttConnectionProfile profile, CancellationToken ct = default)
    {
        var client = _clientFactory(profile);

        if (!_clients.TryAdd(profile.Id, client))
        {
            await client.DisposeAsync();
            throw new InvalidOperationException(
                $"An MQTT client for profile '{profile.Name}' ({profile.Id}) is already active.");
        }

        client.StateChanged += OnClientStateChanged;

        try
        {
            await client.ConnectAsync(ct);
        }
        catch
        {
            _clients.TryRemove(profile.Id, out _);
            client.StateChanged -= OnClientStateChanged;
            await client.DisposeAsync();
            throw;
        }

        return client;
    }

    public async Task DisconnectAsync(ConnectionProfileId profileId, CancellationToken ct = default)
    {
        await CancelReconnectAsync(profileId);

        if (_clients.TryGetValue(profileId, out var client))
            await client.DisconnectAsync(ct);
    }

    public async Task RemoveAsync(ConnectionProfileId profileId, CancellationToken ct = default)
    {
        await CancelReconnectAsync(profileId);

        if (!_clients.TryRemove(profileId, out var client))
            return;

        client.StateChanged -= OnClientStateChanged;

        if (client.State is MqttClientState.Connected or MqttClientState.Connecting)
            await client.DisconnectAsync(ct);

        await client.DisposeAsync();
    }

    private void OnClientStateChanged(object? sender, MqttClientState state)
    {
        if (sender is not IMqttBrokerClient client)
            return;

        StateChanged?.Invoke(this, new MqttClientStateChangedEventArgs(client.Profile.Id, client.Profile, state));

        if (state is MqttClientState.Faulted or MqttClientState.Disconnected
            && _clients.ContainsKey(client.Profile.Id))
        {
            ScheduleReconnect(client);
        }
    }

    private void ScheduleReconnect(IMqttBrokerClient client)
    {
        var cts = new CancellationTokenSource();
        if (_reconnectCts.TryRemove(client.Profile.Id, out var old))
        {
            old.Cancel();
            old.Dispose();
        }
        _reconnectCts[client.Profile.Id] = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                await _reconnectPipeline.ExecuteAsync(async ct =>
                {
                    StateChanged?.Invoke(this, new MqttClientStateChangedEventArgs(
                        client.Profile.Id, client.Profile, MqttClientState.Reconnecting));

                    await client.ConnectAsync(ct);
                }, cts.Token);
            }
            catch (OperationCanceledException) { }
            finally
            {
                _reconnectCts.TryRemove(client.Profile.Id, out _);
                cts.Dispose();
            }
        });
    }

    private async Task CancelReconnectAsync(ConnectionProfileId profileId)
    {
        if (_reconnectCts.TryRemove(profileId, out var cts))
        {
            await cts.CancelAsync();
            cts.Dispose();
        }
    }

    private static ResiliencePipeline BuildDefaultReconnectPipeline() =>
        new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = int.MaxValue,
                Delay = TimeSpan.FromSeconds(1),
                MaxDelay = TimeSpan.FromSeconds(30),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true
            })
            .Build();

    public async ValueTask DisposeAsync()
    {
        foreach (var (id, _) in _reconnectCts)
            await CancelReconnectAsync(id);

        foreach (var (_, client) in _clients)
        {
            client.StateChanged -= OnClientStateChanged;
            await client.DisposeAsync();
        }
        _clients.Clear();
    }
}
