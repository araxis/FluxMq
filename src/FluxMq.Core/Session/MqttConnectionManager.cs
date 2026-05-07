using FluxMq.Core.Models;
using System.Collections.Concurrent;

namespace FluxMq.Core.Session;

public sealed class MqttConnectionManager : IMqttConnectionManager
{
    private readonly Func<MqttConnectionProfile, IMqttSession> _sessionFactory;
    private readonly ConcurrentDictionary<Guid, IMqttSession> _sessions = new();

    public IReadOnlyDictionary<Guid, IMqttSession> Sessions => _sessions;

    public event EventHandler<SessionStateChangedEventArgs>? StateChanged;

    public MqttConnectionManager(Func<MqttConnectionProfile, IMqttSession>? sessionFactory = null)
    {
        _sessionFactory = sessionFactory ?? (profile => new MqttSession(profile));
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

    public async Task DisconnectAsync(Guid sessionId, CancellationToken ct = default)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
            await session.DisconnectAsync(ct);
    }

    public async Task RemoveAsync(Guid sessionId, CancellationToken ct = default)
    {
        if (!_sessions.TryRemove(sessionId, out var session))
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

        // Reconnect policy (Polly) will be wired here in a future step.
        StateChanged?.Invoke(this, new SessionStateChangedEventArgs(session.Profile.Id, session.Profile, state));
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var (_, session) in _sessions)
        {
            session.StateChanged -= OnSessionStateChanged;
            await session.DisposeAsync();
        }
        _sessions.Clear();
    }
}
