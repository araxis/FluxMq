using FluxMq.Core.Models;
using FluxMq.Core.Payloads;
using FluxMq.Core.TopicIndex;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.UI.Services;

public sealed class WorkspaceMessageProjection : IAsyncDisposable
{
    private readonly ITopicIndex _topicIndex;
    private readonly Lock _sync = new();
    private readonly List<MqttEnvelope> _messages = [];
    private readonly ActionBlock<MqttEnvelope> _input;
    private readonly BroadcastBlock<WorkspaceProjectionSnapshot> _updates;
    private MqttEnvelope? _latestMessage;
    private PayloadInspectionResult _latestInspection = PayloadInspector.Inspect([]);
    private MqttEnvelope? _selectedMessage;
    private PayloadInspectionResult _selectedInspection = PayloadInspector.Inspect([]);

    public WorkspaceMessageProjection(ITopicIndex topicIndex, int recentLimit = 200)
    {
        _topicIndex = topicIndex;
        RecentLimit = recentLimit;
        _updates = new BroadcastBlock<WorkspaceProjectionSnapshot>(static snapshot => snapshot);
        _input = new ActionBlock<MqttEnvelope>(
            Apply,
            new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = recentLimit,
                MaxDegreeOfParallelism = 1,
                EnsureOrdered = true
            });

        _input.Completion.ContinueWith(
            _ => _updates.Complete(),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    public int RecentLimit { get; }
    public ITargetBlock<MqttEnvelope> Input => _input;
    public ISourceBlock<WorkspaceProjectionSnapshot> Updates => _updates;
    public Task Completion => _input.Completion;
    public IReadOnlyList<MqttEnvelope> RecentMessages => Snapshot.RecentMessages;
    public MqttEnvelope? LatestMessage => Snapshot.LatestMessage;
    public PayloadInspectionResult LatestInspection => Snapshot.LatestInspection;
    public MqttEnvelope? SelectedMessage => Snapshot.SelectedMessage;
    public PayloadInspectionResult SelectedInspection => Snapshot.SelectedInspection;

    public WorkspaceProjectionSnapshot Snapshot
    {
        get
        {
            lock (_sync)
            {
                return CreateSnapshot();
            }
        }
    }

    public Task ApplyAsync(MqttEnvelope message, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Apply(message);
        return Task.CompletedTask;
    }

    public void SelectMessage(MqttEnvelope message)
    {
        lock (_sync)
        {
            _selectedMessage = message;
            _selectedInspection = PayloadInspector.Inspect(message.Payload);
            _latestMessage ??= message;
            if (_latestMessage == message)
            {
                _latestInspection = _selectedInspection;
            }

            _updates.Post(CreateSnapshot());
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            _messages.Clear();
            _latestMessage = null;
            _latestInspection = PayloadInspector.Inspect([]);
            _selectedMessage = null;
            _selectedInspection = _latestInspection;
            _updates.Post(CreateSnapshot());
        }
    }

    public void ClearSelection()
    {
        lock (_sync)
        {
            _selectedMessage = null;
            _selectedInspection = PayloadInspector.Inspect([]);
            _updates.Post(CreateSnapshot());
        }
    }

    public async ValueTask DisposeAsync()
    {
        _input.Complete();
        await _input.Completion.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
    }

    private void Apply(MqttEnvelope message)
    {
        _topicIndex.Process(message);
        lock (_sync)
        {
            _messages.Insert(0, message);
            if (_messages.Count > RecentLimit)
            {
                _messages.RemoveRange(RecentLimit, _messages.Count - RecentLimit);
            }

            _latestMessage = message;
            _latestInspection = PayloadInspector.Inspect(message.Payload);
            if (_selectedMessage is null)
            {
                _selectedMessage = message;
                _selectedInspection = _latestInspection;
            }

            _updates.Post(CreateSnapshot());
        }
    }

    private WorkspaceProjectionSnapshot CreateSnapshot()
        => new(
            _messages.ToArray(),
            _latestMessage,
            _latestInspection,
            _selectedMessage,
            _selectedInspection);
}
