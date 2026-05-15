using FluxMq.Core.Models;
using FluxMq.Core.Payloads;
using FluxMq.Core.Session;
using FluxMq.Core.TopicIndex;
using FluxMq.Components.Storage.Models;
using FluxMq.Components.Storage.Repositories;
using FluxMq.UI.Models;
using MQTTnet.Protocol;
using System.Text;

namespace FluxMq.UI.Services;

public sealed class LiveMqttWorkspaceService : IAsyncDisposable
{
    private sealed class ConnectionEntry(ManagedConnection connection)
    {
        public ManagedConnection Connection { get; } = connection;
        public IMqttSession? Session { get; set; }
        public CancellationTokenSource? Cts { get; set; }
        public Task? ReaderTask { get; set; }
    }

    private readonly Dictionary<Guid, ConnectionEntry> _entries = new();
    private readonly ISessionRepository _sessionRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly WorkspaceMessageProjection _projection;
    private StoredSession? _recordingSession;
    private StoredSession? _selectedStoredSession;
    private IReadOnlyList<MqttEnvelope> _selectedSessionMessages = [];
    private long _recordedMessageCount;

    public LiveMqttWorkspaceService(
        ITopicIndex topicIndex,
        ISessionRepository sessionRepository,
        IMessageRepository messageRepository)
    {
        _sessionRepository = sessionRepository;
        _messageRepository = messageRepository;
        _projection = new WorkspaceMessageProjection(topicIndex);
    }

    public IReadOnlyList<ManagedConnection> Connections => [.. _entries.Values.Select(e => e.Connection)];

    public MqttSessionState State
    {
        get
        {
            if (_entries.Count == 0) return MqttSessionState.Disconnected;
            var states = _entries.Values.Select(e => e.Connection.State).ToArray();
            if (states.Any(s => s == MqttSessionState.Connected)) return MqttSessionState.Connected;
            if (states.Any(s => s is MqttSessionState.Connecting or MqttSessionState.Reconnecting)) return MqttSessionState.Connecting;
            if (states.Any(s => s == MqttSessionState.Faulted)) return MqttSessionState.Faulted;
            return MqttSessionState.Disconnected;
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

    public void AddConnection(MqttConnectionProfile profile, string subscription = "#")
    {
        var conn = new ManagedConnection(profile, subscription);
        _entries[conn.Id] = new ConnectionEntry(conn);
        NotifyChanged();
    }

    public async Task RemoveConnectionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_entries.TryGetValue(id, out var entry)) return;
        await DisconnectEntryAsync(entry, cancellationToken).ConfigureAwait(false);
        _entries.Remove(id);
        NotifyChanged();
    }

    public async Task ConnectAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_entries.TryGetValue(id, out var entry)) return;
        var conn = entry.Connection;

        await DisconnectEntryAsync(entry, cancellationToken).ConfigureAwait(false);

        entry.Cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var session = new MqttSession(conn.Profile);
        entry.Session = session;
        session.StateChanged += (_, state) => OnConnectionStateChanged(id, state);

        try
        {
            await session.ConnectAsync(cancellationToken).ConfigureAwait(false);

            var filters = ParseSubscriptionFilters(conn.Subscription);
            foreach (var filter in filters)
                await session.SubscribeAsync(filter, MqttQualityOfServiceLevel.AtMostOnce, cancellationToken).ConfigureAwait(false);

            conn.LastError = null;
            Diagnostics = [new WorkspaceDiagnostic("Info", "MQTT", "Subscribed", $"Connected to {conn.Profile.Host}:{conn.Profile.Port}.")];
            entry.ReaderTask = ReadConnectionAsync(entry, entry.Cts.Token);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            conn.State = MqttSessionState.Faulted;
            conn.LastError = exception.Message;
            Diagnostics = [new WorkspaceDiagnostic("Error", "MQTT", "ConnectFailed", exception.Message)];
            await DisconnectEntryAsync(entry, CancellationToken.None).ConfigureAwait(false);
            conn.State = MqttSessionState.Faulted;
            conn.LastError = exception.Message;
        }

        NotifyChanged();
    }

    public async Task DisconnectAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_entries.TryGetValue(id, out var entry)) return;
        await DisconnectEntryAsync(entry, cancellationToken).ConfigureAwait(false);
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

    public async Task PublishAsync(string topic, string payload, CancellationToken cancellationToken = default)
    {
        var session = _entries.Values
            .FirstOrDefault(e => e.Connection.State == MqttSessionState.Connected)?.Session;

        if (session is null)
        {
            Diagnostics = [new WorkspaceDiagnostic("Warning", "MQTT", "NotConnected", "Connect before publishing.")];
            NotifyChanged();
            return;
        }

        try
        {
            await session.PublishAsync(
                topic,
                Encoding.UTF8.GetBytes(payload),
                MqttQualityOfServiceLevel.AtMostOnce,
                retain: false,
                cancellationToken).ConfigureAwait(false);

            Diagnostics = [new WorkspaceDiagnostic("Info", "MQTT", "Published", $"Published to {topic}.")];
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Diagnostics = [new WorkspaceDiagnostic("Error", "MQTT", "PublishFailed", exception.Message)];
        }

        NotifyChanged();
    }

    public void StartRecording(string sessionName, string projectName)
    {
        if (_recordingSession is not null) return;

        var profile = _entries.Values
            .Where(e => e.Connection.State == MqttSessionState.Connected)
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
        foreach (var entry in _entries.Values)
            await DisconnectEntryAsync(entry, CancellationToken.None).ConfigureAwait(false);
        await _projection.DisposeAsync().ConfigureAwait(false);
    }

    private async Task DisconnectEntryAsync(ConnectionEntry entry, CancellationToken cancellationToken)
    {
        var session = entry.Session;
        entry.Session = null;

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

        if (session is not null)
        {
            session.StateChanged -= (_, state) => OnConnectionStateChanged(entry.Connection.Id, state);
            await session.DisposeAsync().ConfigureAwait(false);
        }

        entry.Connection.State = MqttSessionState.Disconnected;
    }

    private async Task ReadConnectionAsync(ConnectionEntry entry, CancellationToken cancellationToken)
    {
        var conn = entry.Connection;
        try
        {
            await foreach (var message in entry.Session!.Messages.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                RecordMessage(message);
                await _projection.ApplyAsync(message, cancellationToken).ConfigureAwait(false);
                NotifyChanged();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            conn.State = MqttSessionState.Faulted;
            conn.LastError = exception.Message;
            Diagnostics = [new WorkspaceDiagnostic("Error", "MQTT", "ReaderFailed", exception.Message)];
            NotifyChanged();
        }
    }

    private void OnConnectionStateChanged(Guid id, MqttSessionState state)
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

    private void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);

    private static string NormalizeProject(string? projectName)
        => string.IsNullOrWhiteSpace(projectName) ? "Default" : projectName.Trim();

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
