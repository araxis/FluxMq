using FluxMq.Core.Models;
using FluxMq.Core.Payloads;
using FluxMq.Core.Session;
using FluxMq.Core.TopicIndex;
using FluxMq.Storage.Models;
using FluxMq.Storage.Repositories;
using FluxMq.UI.Models;
using MQTTnet.Protocol;
using System.Text;

namespace FluxMq.UI.Services;

public sealed class LiveMqttWorkspaceService : IAsyncDisposable
{
    private readonly ITopicIndex _topicIndex;
    private readonly ISessionRepository _sessionRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly object _sync = new();
    private readonly List<MqttEnvelope> _messages = [];
    private IMqttSession? _session;
    private CancellationTokenSource? _readerCts;
    private Task? _readerTask;
    private StoredSession? _recordingSession;
    private long _recordedMessageCount;

    public LiveMqttWorkspaceService(
        ITopicIndex topicIndex,
        ISessionRepository sessionRepository,
        IMessageRepository messageRepository)
    {
        _topicIndex = topicIndex;
        _sessionRepository = sessionRepository;
        _messageRepository = messageRepository;
    }

    public MqttConnectionProfile Profile { get; private set; } = new()
    {
        Name = "local-broker",
        Host = "localhost",
        Port = 1883
    };

    public string Subscription { get; private set; } = "#";
    public MqttSessionState State { get; private set; } = MqttSessionState.Disconnected;
    public MqttEnvelope? SelectedMessage { get; private set; }
    public PayloadInspectionResult SelectedInspection { get; private set; } = PayloadInspector.Inspect([]);
    public bool IsRecording => _recordingSession is not null;
    public long RecordedMessageCount => _recordedMessageCount;
    public IReadOnlyList<StoredSession> StoredSessions => _sessionRepository.GetAll();
    public IReadOnlyList<MqttEnvelope> RecentMessages
    {
        get
        {
            lock (_sync)
            {
                return _messages.ToArray();
            }
        }
    }

    public IReadOnlyList<WorkspaceDiagnostic> Diagnostics { get; private set; } = [];

    public event EventHandler? Changed;

    public void UpdateProfile(MqttConnectionProfile profile, string subscription)
    {
        Profile = profile;
        Subscription = string.IsNullOrWhiteSpace(subscription) ? "#" : subscription;
        NotifyChanged();
    }

    public async Task TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        await using var session = new MqttSession(Profile);
        try
        {
            await session.ConnectAsync(cancellationToken).ConfigureAwait(false);
            await session.DisconnectAsync(cancellationToken).ConfigureAwait(false);
            Diagnostics =
            [
                new WorkspaceDiagnostic("Info", "MQTT", "ConnectionOk", $"Connected to {Profile.Host}:{Profile.Port}.")
            ];
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Diagnostics =
            [
                new WorkspaceDiagnostic("Error", "MQTT", "ConnectionFailed", exception.Message)
            ];
        }

        NotifyChanged();
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        await DisconnectAsync(cancellationToken).ConfigureAwait(false);

        _readerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _session = new MqttSession(Profile);
        _session.StateChanged += OnSessionStateChanged;

        try
        {
            await _session.ConnectAsync(cancellationToken).ConfigureAwait(false);
            await _session.SubscribeAsync(Subscription, MqttQualityOfServiceLevel.AtMostOnce, cancellationToken).ConfigureAwait(false);
            Diagnostics =
            [
                new WorkspaceDiagnostic("Info", "MQTT", "Subscribed", $"Subscribed to {Subscription}.")
            ];
            _readerTask = ReadMessagesAsync(_session, _readerCts.Token);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            State = MqttSessionState.Faulted;
            Diagnostics =
            [
                new WorkspaceDiagnostic("Error", "MQTT", "ConnectFailed", exception.Message)
            ];
            await DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
            State = MqttSessionState.Faulted;
        }

        NotifyChanged();
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        var session = _session;
        _session = null;

        if (_readerCts is not null)
        {
            await _readerCts.CancelAsync().ConfigureAwait(false);
            _readerCts.Dispose();
            _readerCts = null;
        }

        if (_readerTask is not null)
        {
            await _readerTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            _readerTask = null;
        }

        if (session is not null)
        {
            session.StateChanged -= OnSessionStateChanged;
            await session.DisposeAsync().ConfigureAwait(false);
        }

        State = MqttSessionState.Disconnected;
        NotifyChanged();
    }

    public async Task PublishAsync(string topic, string payload, CancellationToken cancellationToken = default)
    {
        if (_session is null || State != MqttSessionState.Connected)
        {
            Diagnostics =
            [
                new WorkspaceDiagnostic("Warning", "MQTT", "NotConnected", "Connect before publishing.")
            ];
            NotifyChanged();
            return;
        }

        try
        {
            await _session.PublishAsync(
                topic,
                Encoding.UTF8.GetBytes(payload),
                MqttQualityOfServiceLevel.AtMostOnce,
                retain: false,
                cancellationToken).ConfigureAwait(false);

            Diagnostics =
            [
                new WorkspaceDiagnostic("Info", "MQTT", "Published", $"Published to {topic}.")
            ];
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Diagnostics =
            [
                new WorkspaceDiagnostic("Error", "MQTT", "PublishFailed", exception.Message)
            ];
        }

        NotifyChanged();
    }

    public void StartRecording()
    {
        if (_recordingSession is not null)
        {
            return;
        }

        try
        {
            _recordingSession = _sessionRepository.Start(Profile);
            _recordedMessageCount = 0;
            Diagnostics =
            [
                new WorkspaceDiagnostic("Info", "Recording", "Started", $"Recording session started for {Profile.Name}.")
            ];
        }
        catch (Exception exception)
        {
            Diagnostics =
            [
                new WorkspaceDiagnostic("Error", "Recording", "StartFailed", exception.Message)
            ];
        }

        NotifyChanged();
    }

    public void StopRecording()
    {
        if (_recordingSession is null)
        {
            return;
        }

        try
        {
            _sessionRepository.End(_recordingSession.Id);
            Diagnostics =
            [
                new WorkspaceDiagnostic("Info", "Recording", "Stopped", $"Recorded {_recordedMessageCount} messages.")
            ];
        }
        catch (Exception exception)
        {
            Diagnostics =
            [
                new WorkspaceDiagnostic("Error", "Recording", "StopFailed", exception.Message)
            ];
        }
        finally
        {
            _recordingSession = null;
        }

        NotifyChanged();
    }

    public void SelectMessage(MqttEnvelope message)
    {
        SelectedMessage = message;
        SelectedInspection = PayloadInspector.Inspect(message.Payload);
        NotifyChanged();
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private async Task ReadMessagesAsync(IMqttSession session, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var message in session.Messages.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                _topicIndex.Process(message);
                lock (_sync)
                {
                    _messages.Insert(0, message);
                    if (_messages.Count > 200)
                    {
                        _messages.RemoveRange(200, _messages.Count - 200);
                    }
                }

                RecordMessage(message);
                SelectedMessage ??= message;
                SelectedInspection = PayloadInspector.Inspect(SelectedMessage.Payload);
                NotifyChanged();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            State = MqttSessionState.Faulted;
            Diagnostics =
            [
                new WorkspaceDiagnostic("Error", "MQTT", "ReaderFailed", exception.Message)
            ];
            NotifyChanged();
        }
    }

    private void OnSessionStateChanged(object? sender, MqttSessionState state)
    {
        State = state;
        NotifyChanged();
    }

    private void RecordMessage(MqttEnvelope message)
    {
        var recordingSession = _recordingSession;
        if (recordingSession is null)
        {
            return;
        }

        try
        {
            _messageRepository.Add(recordingSession.Id, message);
            _recordedMessageCount++;
        }
        catch (Exception exception)
        {
            Diagnostics =
            [
                new WorkspaceDiagnostic("Error", "Recording", "MessageStoreFailed", exception.Message)
            ];
        }
    }

    private void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
