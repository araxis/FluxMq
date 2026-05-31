using FluxMq.Core.Models;
using FluxMq.Core.Payloads;
using FluxMq.Core.Mqtt;
using FluxMq.Core.TopicIndex;
using FluxMq.Components.Storage.Models;
using FluxMq.Components.Storage.Repositories;
using FluxMq.UI.Models;
using MQTTnet.Protocol;
using System.Text;

namespace FluxMq.UI.Services;

public sealed class LiveMqttWorkspaceService : IAsyncDisposable
{
    public const string DefaultBrokerMonitorSubscription = "#,$SYS/#";
    private static readonly TimeSpan MessageChangeNotificationInterval = TimeSpan.FromMilliseconds(250);

    private sealed class ConnectionEntry(ManagedConnection connection)
    {
        public ManagedConnection Connection { get; } = connection;
        public IMqttBrokerClient? Client { get; set; }
        public CancellationTokenSource? Cts { get; set; }
        public Task? ReaderTask { get; set; }
        public EventHandler<MqttClientState>? StateChangedHandler { get; set; }
    }

    private readonly Dictionary<Guid, ConnectionEntry> _entries = new();
    private readonly ISessionRepository _sessionRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly WorkspaceMessageProjection _projection;
    private readonly Func<MqttConnectionProfile, IMqttBrokerClient> _clientFactory;
    private readonly HashSet<Guid> _autoStartedConnectionIds = [];
    private StoredSession? _recordingSession;
    private StoredSession? _selectedStoredSession;
    private IReadOnlyList<MqttEnvelope> _selectedSessionMessages = [];
    private long _recordedMessageCount;
    private int _messageChangeNotificationQueued;
    private int _disposed;

    public LiveMqttWorkspaceService(
        ITopicIndex topicIndex,
        ISessionRepository sessionRepository,
        IMessageRepository messageRepository)
        : this(topicIndex, sessionRepository, messageRepository, static profile => new MqttBrokerClient(profile))
    {
    }

    public LiveMqttWorkspaceService(
        ITopicIndex topicIndex,
        ISessionRepository sessionRepository,
        IMessageRepository messageRepository,
        Func<MqttConnectionProfile, IMqttBrokerClient> clientFactory)
    {
        _sessionRepository = sessionRepository;
        _messageRepository = messageRepository;
        _projection = new WorkspaceMessageProjection(topicIndex);
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
    }

    public IReadOnlyList<ManagedConnection> Connections => [.. _entries.Values.Select(e => e.Connection)];

    public MqttClientState State
    {
        get
        {
            if (_entries.Count == 0) return MqttClientState.Disconnected;
            var states = _entries.Values.Select(e => e.Connection.State).ToArray();
            if (states.Any(s => s == MqttClientState.Connected)) return MqttClientState.Connected;
            if (states.Any(s => s is MqttClientState.Connecting or MqttClientState.Reconnecting)) return MqttClientState.Connecting;
            if (states.Any(s => s == MqttClientState.Faulted)) return MqttClientState.Faulted;
            return MqttClientState.Disconnected;
        }
    }

    public string CurrentProjectName { get; private set; } = "Default";
    public MqttEnvelope? LatestMessage => _projection.LatestMessage;
    public PayloadInspectionResult LatestInspection => _projection.LatestInspection;
    public MqttEnvelope? SelectedMessage => _projection.SelectedMessage;
    public PayloadInspectionResult SelectedInspection => _projection.SelectedInspection;
    public bool IsRecording => _recordingSession is not null;
    public long RecordedMessageCount => _recordedMessageCount;
    public StoredSession? ActiveRecordingSession => _recordingSession;
    public StoredSession? SelectedStoredSession => _selectedStoredSession;
    public IReadOnlyList<MqttEnvelope> SelectedSessionMessages => _selectedSessionMessages;
    public IReadOnlyList<string> ProjectNames => StoredSessions
        .Select(s => string.IsNullOrWhiteSpace(s.ProjectName) ? "Default" : s.ProjectName)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(p => p)
        .ToArray();
    public IReadOnlyList<StoredSession> StoredSessions => _sessionRepository.GetAll();
    public IReadOnlyList<StoredSession> CurrentProjectSessions => StoredSessions
        .Where(s => string.Equals(NormalizeProject(s.ProjectName), CurrentProjectName, StringComparison.OrdinalIgnoreCase))
        .ToArray();
    public IReadOnlyList<MqttEnvelope> RecentMessages => _projection.RecentMessages;
    public IReadOnlyList<WorkspaceDiagnostic> Diagnostics { get; private set; } = [];

    public event EventHandler? Changed;

    public void AddConnection(MqttConnectionProfile profile, string subscription = DefaultBrokerMonitorSubscription, string? resourceName = null)
    {
        var conn = new ManagedConnection(profile, subscription, resourceName);
        _entries[conn.Id] = new ConnectionEntry(conn);
        NotifyChanged();
    }

    public ManagedConnection AddConnectionIfAbsent(MqttConnectionProfile profile, string subscription = DefaultBrokerMonitorSubscription, string? resourceName = null)
    {
        var normalizedResourceName = NormalizeResourceName(resourceName);
        var existing = _entries.Values.FirstOrDefault(e => ConnectionMatches(e.Connection, profile, normalizedResourceName));

        if (existing is not null)
        {
            return existing.Connection;
        }

        var conn = new ManagedConnection(profile, subscription, normalizedResourceName);
        _entries[conn.Id] = new ConnectionEntry(conn);
        NotifyChanged();
        return conn;
    }

    public Task<bool> EnsureConnectionsAsync(
        IEnumerable<(MqttConnectionProfile Profile, string Subscription)> connections,
        CancellationToken cancellationToken = default)
        => EnsureConnectionsAsync(
            connections.Select(static connection => (
                ResourceName: string.Empty,
                connection.Profile,
                connection.Subscription)),
            cancellationToken);

    public async Task<bool> EnsureConnectionsAsync(
        IEnumerable<(string ResourceName, MqttConnectionProfile Profile, string Subscription)> connections,
        CancellationToken cancellationToken = default)
    {
        var managedConnections = connections
            .Select(connection => AddConnectionIfAbsent(connection.Profile, connection.Subscription, connection.ResourceName))
            .DistinctBy(static connection => connection.Id)
            .ToArray();

        foreach (var connection in managedConnections)
        {
            if (connection.State != MqttClientState.Connected)
            {
                await ConnectAsync(connection.Id, cancellationToken).ConfigureAwait(false);
                if (connection.State == MqttClientState.Connected)
                {
                    _autoStartedConnectionIds.Add(connection.Id);
                }
            }
        }

        return managedConnections.All(static connection => connection.State == MqttClientState.Connected);
    }

    public async Task RemoveConnectionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_entries.TryGetValue(id, out var entry)) return;
        _autoStartedConnectionIds.Remove(id);
        await DisconnectEntryAsync(entry, cancellationToken).ConfigureAwait(false);
        _entries.Remove(id);
        NotifyChanged();
    }

    public async Task RemoveConnectionsAsync(
        IEnumerable<(string ResourceName, MqttConnectionProfile Profile)> connections,
        CancellationToken cancellationToken = default)
    {
        var targets = connections
            .Where(connection => !string.IsNullOrWhiteSpace(connection.ResourceName))
            .Select(connection => (
                ResourceName: NormalizeResourceName(connection.ResourceName),
                connection.Profile))
            .ToArray();

        if (targets.Length == 0)
        {
            return;
        }

        var ids = _entries.Values
            .Where(entry => targets.Any(target => ConnectionMatches(entry.Connection, target.Profile, target.ResourceName)))
            .Select(entry => entry.Connection.Id)
            .ToArray();

        foreach (var id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await RemoveConnectionAsync(id, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task ConnectAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_entries.TryGetValue(id, out var entry)) return;
        var conn = entry.Connection;

        await DisconnectEntryAsync(entry, cancellationToken).ConfigureAwait(false);

        entry.Cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var client = _clientFactory(CreateWorkspaceClientProfile(conn.Profile, conn.Id));
        entry.Client = client;
        entry.StateChangedHandler = (_, state) => OnConnectionStateChanged(id, state);
        client.StateChanged += entry.StateChangedHandler;

        try
        {
            await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

            var filters = ParseSubscriptionFilters(conn.Subscription);
            foreach (var filter in filters)
                await client.SubscribeAsync(filter, MqttQualityOfServiceLevel.AtMostOnce, cancellationToken).ConfigureAwait(false);

            conn.LastError = null;
            Diagnostics = [new WorkspaceDiagnostic("Info", "MQTT", "Subscribed", $"Connected to {conn.Profile.Host}:{conn.Profile.Port}.")];
            entry.ReaderTask = ReadConnectionAsync(entry, entry.Cts.Token);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            conn.State = MqttClientState.Faulted;
            conn.LastError = exception.Message;
            Diagnostics = [new WorkspaceDiagnostic("Error", "MQTT", "ConnectFailed", exception.Message)];
            await DisconnectEntryAsync(entry, CancellationToken.None).ConfigureAwait(false);
            conn.State = MqttClientState.Faulted;
            conn.LastError = exception.Message;
        }

        NotifyChanged();
    }

    public async Task DisconnectAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_entries.TryGetValue(id, out var entry)) return;
        _autoStartedConnectionIds.Remove(id);
        await DisconnectEntryAsync(entry, cancellationToken).ConfigureAwait(false);
        NotifyChanged();
    }

    public async Task DisconnectAutoStartedConnectionsAsync(CancellationToken cancellationToken = default)
    {
        if (_autoStartedConnectionIds.Count == 0)
        {
            return;
        }

        var ids = _autoStartedConnectionIds.ToArray();
        _autoStartedConnectionIds.Clear();

        foreach (var id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_entries.TryGetValue(id, out var entry))
            {
                await DisconnectEntryAsync(entry, cancellationToken).ConfigureAwait(false);
            }
        }

        NotifyChanged();
    }

    public void SetProject(string projectName)
    {
        CurrentProjectName = NormalizeProject(projectName);
        _selectedStoredSession = null;
        _selectedSessionMessages = [];
        _projection.ClearSelection();
        NotifyChanged();
    }

    public void ClearStoredSessionSelection()
    {
        _selectedStoredSession = null;
        _selectedSessionMessages = [];
        _projection.ClearSelection();
        NotifyChanged();
    }

    public async Task<bool> PublishAsync(
        string topic,
        string payload,
        MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce,
        bool retain = false,
        CancellationToken cancellationToken = default)
        => await PublishAsync(
            connectionId: null,
            topic,
            payload,
            qos,
            retain,
            cancellationToken).ConfigureAwait(false);

    public async Task<bool> PublishAsync(
        Guid? connectionId,
        string topic,
        string payload,
        MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce,
        bool retain = false,
        CancellationToken cancellationToken = default)
    {
        var entry = connectionId is { } id
            ? _entries.Values.FirstOrDefault(e => e.Connection.Id == id && e.Connection.State == MqttClientState.Connected)
            : _entries.Values.FirstOrDefault(e => e.Connection.State == MqttClientState.Connected);

        if (entry?.Client is null)
        {
            Diagnostics = [new WorkspaceDiagnostic("Warning", "MQTT", "NotConnected", "Connect before publishing.")];
            NotifyChanged();
            return false;
        }

        try
        {
            await entry.Client.PublishAsync(
                topic,
                Encoding.UTF8.GetBytes(payload),
                qos,
                retain,
                cancellationToken).ConfigureAwait(false);

            Diagnostics = [new WorkspaceDiagnostic("Info", "MQTT", "Published", $"Published to {topic} through {entry.Connection.ResourceName}.")];
            NotifyChanged();
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Diagnostics = [new WorkspaceDiagnostic("Error", "MQTT", "PublishFailed", exception.Message)];
            NotifyChanged();
            return false;
        }
    }

    public void StartRecording(string sessionName, string projectName)
    {
        if (_recordingSession is not null) return;

        var profile = _entries.Values
            .Where(e => e.Connection.State == MqttClientState.Connected)
            .Select(e => e.Connection.Profile)
            .FirstOrDefault() ?? new MqttConnectionProfile { Name = "workspace" };

        try
        {
            CurrentProjectName = NormalizeProject(projectName);
            _recordingSession = _sessionRepository.Start(profile, sessionName, CurrentProjectName);
            _recordedMessageCount = 0;
            Diagnostics = [new WorkspaceDiagnostic("Info", "Recording", "Started", $"Recording session '{_recordingSession.Name}' started.")];
        }
        catch (Exception exception)
        {
            Diagnostics = [new WorkspaceDiagnostic("Error", "Recording", "StartFailed", exception.Message)];
        }

        NotifyChanged();
    }

    public void StopRecording()
    {
        if (_recordingSession is null) return;

        try
        {
            _sessionRepository.End(_recordingSession.Id);
            Diagnostics = [new WorkspaceDiagnostic("Info", "Recording", "Stopped", $"Recorded {_recordedMessageCount} messages.")];
        }
        catch (Exception exception)
        {
            Diagnostics = [new WorkspaceDiagnostic("Error", "Recording", "StopFailed", exception.Message)];
        }
        finally
        {
            _recordingSession = null;
        }

        NotifyChanged();
    }

    public void SelectMessage(MqttEnvelope message)
    {
        _projection.SelectMessage(message);
        NotifyChanged();
    }

    public Task SelectStoredSessionAsync(StoredSession session, CancellationToken cancellationToken = default)
        => LoadStoredSessionAsync(session, cancellationToken);

    public void SelectStoredSession(StoredSession session)
        => _ = LoadStoredSessionAsync(session, CancellationToken.None);

    public async Task LoadStoredSessionAsync(StoredSession session, CancellationToken cancellationToken = default)
    {
        try
        {
            _selectedStoredSession = session;
            _projection.Reset();
            var loaded = new List<MqttEnvelope>();
            await foreach (var message in _messageRepository.ReadEnvelopesBySessionAsync(session.Id, cancellationToken).ConfigureAwait(false))
            {
                loaded.Add(message);
                await _projection.ApplyAsync(message, cancellationToken).ConfigureAwait(false);
            }

            _selectedSessionMessages = loaded.ToArray();
            Diagnostics = [new WorkspaceDiagnostic("Info", "Recording", "SessionLoaded", $"Loaded {_selectedSessionMessages.Count} messages from '{session.Name}'.")];
        }
        catch (Exception exception)
        {
            Diagnostics = [new WorkspaceDiagnostic("Error", "Recording", "SessionLoadFailed", exception.Message)];
        }

        NotifyChanged();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        _autoStartedConnectionIds.Clear();
        foreach (var entry in _entries.Values)
            await DisconnectEntryAsync(entry, CancellationToken.None).ConfigureAwait(false);
        await _projection.DisposeAsync().ConfigureAwait(false);
    }

    private async Task DisconnectEntryAsync(ConnectionEntry entry, CancellationToken cancellationToken)
    {
        var client = entry.Client;
        entry.Client = null;

        if (entry.Cts is not null)
        {
            await entry.Cts.CancelAsync().ConfigureAwait(false);
            entry.Cts.Dispose();
            entry.Cts = null;
        }

        if (entry.ReaderTask is not null)
        {
            await entry.ReaderTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            entry.ReaderTask = null;
        }

        if (client is not null)
        {
            if (entry.StateChangedHandler is not null)
            {
                client.StateChanged -= entry.StateChangedHandler;
                entry.StateChangedHandler = null;
            }

            await client.DisposeAsync().ConfigureAwait(false);
        }

        entry.Connection.State = MqttClientState.Disconnected;
    }

    private async Task ReadConnectionAsync(ConnectionEntry entry, CancellationToken cancellationToken)
    {
        var conn = entry.Connection;
        try
        {
            await foreach (var message in entry.Client!.Messages.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                RecordMessage(message);
                await _projection.ApplyAsync(message, cancellationToken).ConfigureAwait(false);
                NotifyMessageChanged();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            conn.State = MqttClientState.Faulted;
            conn.LastError = exception.Message;
            Diagnostics = [new WorkspaceDiagnostic("Error", "MQTT", "ReaderFailed", exception.Message)];
            NotifyChanged();
        }
    }

    private void OnConnectionStateChanged(Guid id, MqttClientState state)
    {
        if (_entries.TryGetValue(id, out var entry))
        {
            entry.Connection.State = state;
            NotifyChanged();
        }
    }

    private void RecordMessage(MqttEnvelope message)
    {
        var recordingSession = _recordingSession;
        if (recordingSession is null) return;

        try
        {
            _messageRepository.Add(recordingSession.Id, message);
            _recordedMessageCount++;
        }
        catch (Exception exception)
        {
            Diagnostics = [new WorkspaceDiagnostic("Error", "Recording", "MessageStoreFailed", exception.Message)];
        }
    }

    private void NotifyMessageChanged()
    {
        if (Volatile.Read(ref _disposed) == 1)
        {
            return;
        }

        if (Interlocked.Exchange(ref _messageChangeNotificationQueued, 1) == 1)
        {
            return;
        }

        _ = NotifyMessageChangedAsync();
    }

    private async Task NotifyMessageChangedAsync()
    {
        try
        {
            await Task.Delay(MessageChangeNotificationInterval).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Exchange(ref _messageChangeNotificationQueued, 0);
        }

        NotifyChanged();
    }

    private void NotifyChanged()
    {
        if (Volatile.Read(ref _disposed) == 1)
        {
            return;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static string NormalizeProject(string? projectName)
        => string.IsNullOrWhiteSpace(projectName) ? "Default" : projectName.Trim();

    private static string? NormalizeResourceName(string? resourceName)
        => string.IsNullOrWhiteSpace(resourceName) ? null : resourceName.Trim();

    private static bool ConnectionMatches(
        ManagedConnection connection,
        MqttConnectionProfile profile,
        string? resourceName)
    {
        if (!string.IsNullOrWhiteSpace(resourceName))
        {
            return string.Equals(connection.ResourceName, resourceName, StringComparison.Ordinal);
        }

        return string.Equals(connection.Profile.Host, profile.Host, StringComparison.OrdinalIgnoreCase) &&
               connection.Profile.Port == profile.Port &&
               connection.Profile.UseTls == profile.UseTls &&
               string.Equals(connection.Profile.Username, profile.Username, StringComparison.Ordinal);
    }

    private static MqttConnectionProfile CreateWorkspaceClientProfile(MqttConnectionProfile profile, Guid connectionId)
    {
        var baseClientId = string.IsNullOrWhiteSpace(profile.ClientId)
            ? "fluxmq"
            : profile.ClientId.Trim();

        const int maxBaseLength = 48;
        if (baseClientId.Length > maxBaseLength)
        {
            baseClientId = baseClientId[..maxBaseLength];
        }

        return profile with
        {
            ClientId = $"{baseClientId}-workspace-{connectionId:N}"[..Math.Min(baseClientId.Length + 19, 64)]
        };
    }

    private static string[] ParseSubscriptionFilters(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return ["#"];
        var filters = raw
            .Split([',', ';', '\n', '\r'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return filters.Length == 0 ? ["#"] : filters;
    }
}
