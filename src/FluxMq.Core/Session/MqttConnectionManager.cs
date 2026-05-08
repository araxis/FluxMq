using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using Polly;
using Polly.Retry;
using System.Collections.Concurrent;

namespace FluxMq.Core.Session;

public sealed class MqttConnectionManager : IMqttConnectionManager
{
    private readonly Func<MqttConnectionProfile, IMqttSession> _sessionFactory;
    private readonly ResiliencePipeline _reconnectPipeline;
    private readonly ConcurrentDictionary<ConnectionProfileId, IMqttSession> _sessions = new();
    private readonly ConcurrentDictionary<ConnectionProfileId, CancellationTokenSource> _reconnectCts = new();

    public IReadOnlyDictionary<ConnectionProfileId, IMqttSession> Sessions => _sessions;

    public event EventHandler<SessionStateChangedEventArgs>? StateChanged;

    public MqttConnectionManager(
        Func<MqttConnectionProfile, IMqttSession>? sessionFactory = null,
        ResiliencePipeline? reconnectPipeline = null)
    {
        _sessionFactory = sessionFactory ?? (profile => new MqttSession(profile));
        _reconnectPipeline = reconnectPipeline ?? BuildDefaultReconnectPipeline();
    }

    public async Task<IMqttSession> ConnectAsync(MqttConnectionProfile profile, CancellationToken ct = default)
    {
        var session = _sessionFactory(profile);

        if (!_sessions.TryAdd(profile.Id, session))
        {
            await session.DisposeAsync();
            throw new InvalidOperationException(
                $"A session for profile '{profile.Name}' ({profile.Id}) is already active.");
        }

        session.StateChanged += OnSessionStateChanged;

        try
        {
            await session.ConnectAsync(ct);
        }
        catch
        {
            _sessions.TryRemove(profile.Id, out _);
            session.StateChanged -= OnSessionStateChanged;
            await session.DisposeAsync();
            throw;
        }

        return session;
    }

    public async Task DisconnectAsync(ConnectionProfileId profileId, CancellationToken ct = default)
    {
        await CancelReconnectAsync(profileId);

        if (_sessions.TryGetValue(profileId, out var session))
            await session.DisconnectAsync(ct);
    }

    public async Task RemoveAsync(ConnectionProfileId profileId, CancellationToken ct = default)
    {
        await CancelReconnectAsync(profileId);

        if (!_sessions.TryRemove(profileId, out var session))
            return;

        session.StateChanged -= OnSessionStateChanged;

        if (session.State is MqttSessionState.Connected or MqttSessionState.Connecting)
            await session.DisconnectAsync(ct);

        await session.DisposeAsync();
    }

    private void OnSessionStateChanged(object? sender, MqttSessionState state)
    {
        if (sender is not IMqttSession session)
            return;

        StateChanged?.Invoke(this, new SessionStateChangedEventArgs(session.Profile.Id, session.Profile, state));

        if (state is MqttSessionState.Faulted or MqttSessionState.Disconnected
            && _sessions.ContainsKey(session.Profile.Id))
        {
            ScheduleReconnect(session);
        }
    }

    private void ScheduleReconnect(IMqttSession session)
    {
        var cts = new CancellationTokenSource();
        if (_reconnectCts.TryRemove(session.Profile.Id, out var old))
        {
            old.Cancel();
            old.Dispose();
        }
        _reconnectCts[session.Profile.Id] = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                await _reconnectPipeline.ExecuteAsync(async ct =>
                {
                    StateChanged?.Invoke(this, new SessionStateChangedEventArgs(
                        session.Profile.Id, session.Profile, MqttSessionState.Reconnecting));

                    await session.ConnectAsync(ct);
                }, cts.Token);
            }
            catch (OperationCanceledException) { }
            finally
            {
                _reconnectCts.TryRemove(session.Profile.Id, out _);
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

        foreach (var (_, session) in _sessions)
        {
            session.StateChanged -= OnSessionStateChanged;
            await session.DisposeAsync();
        }
        _sessions.Clear();
    }
}
