using FluxMq.Core.Models;

namespace FluxMq.Core.Session;

public interface IMqttConnectionManager : IAsyncDisposable
{
    IReadOnlyDictionary<Guid, IMqttSession> Sessions { get; }

    /// <summary>
    /// Raised on any session state transition, including unexpected disconnects and faults.
    /// Reconnect policy (Polly) will subscribe here in a future step.
    /// </summary>
    event EventHandler<SessionStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Creates a session for the given profile, connects it, and begins tracking it.
    /// Throws if a session for this profile ID is already active.
    /// </summary>
    Task<IMqttSession> ConnectAsync(MqttConnectionProfile profile, CancellationToken ct = default);

    Task DisconnectAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>
    /// Disconnects (if connected), disposes, and removes the session from tracking.
    /// </summary>
    Task RemoveAsync(Guid sessionId, CancellationToken ct = default);
}
