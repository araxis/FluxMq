using FluxMq.Pipeline.Components;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Pipeline.Scenarios;

public sealed class ScenarioEventJournal : IDisposable
{
    private readonly object _gate = new();
    private readonly List<FlowEvent> _events = [];
    private readonly ActionBlock<FlowEvent> _target;
    private readonly IDisposable _link;
    private TaskCompletionSource _changed = NewChangeSource();
    private bool _completed;
    private bool _disposed;

    public ScenarioEventJournal(ISourceBlock<FlowEvent> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        _target = new ActionBlock<FlowEvent>(Record);
        _link = source.LinkTo(_target, new DataflowLinkOptions { PropagateCompletion = true });
        _ = _target.Completion.ContinueWith(
            _ => Complete(),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _events.Count;
            }
        }
    }

    public async Task<ScenarioEventMatch?> WaitForMatchAsync(
        int startIndex,
        Predicate<FlowEvent> predicate,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Timeout must be greater than zero.");
        }

        var cursor = Math.Max(0, startIndex);
        var timeoutTask = Task.Delay(timeout, cancellationToken);

        while (true)
        {
            Task changedTask;
            lock (_gate)
            {
                for (var index = cursor; index < _events.Count; index++)
                {
                    if (predicate(_events[index]))
                    {
                        return new ScenarioEventMatch(_events[index], index);
                    }
                }

                cursor = _events.Count;
                if (_completed)
                {
                    return null;
                }

                changedTask = _changed.Task;
            }

            var completed = await Task.WhenAny(changedTask, timeoutTask).ConfigureAwait(false);
            if (completed == timeoutTask)
            {
                await timeoutTask.ConfigureAwait(false);
                return null;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _link.Dispose();
        _target.Complete();
        Complete();
    }

    private static TaskCompletionSource NewChangeSource()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private void Record(FlowEvent flowEvent)
    {
        TaskCompletionSource changed;
        lock (_gate)
        {
            _events.Add(flowEvent);
            changed = _changed;
            _changed = NewChangeSource();
        }

        changed.TrySetResult();
    }

    private void Complete()
    {
        TaskCompletionSource changed;
        lock (_gate)
        {
            if (_completed)
            {
                return;
            }

            _completed = true;
            changed = _changed;
            _changed = NewChangeSource();
        }

        changed.TrySetResult();
    }
}
