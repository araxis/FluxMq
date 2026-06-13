using FluxFlow.Engine.Components;

namespace FluxMq.App.Metrics;

/// <summary>
/// A rolling buffer of recent events used by windowed metrics. It keeps events newer than the window and
/// prunes the rest; metrics read <see cref="Count"/> or <see cref="DistinctTopicCount"/>. Composed, not inherited.
/// </summary>
public sealed class SlidingEventWindow
{
    private readonly TimeSpan _window;
    private readonly List<FlowEvent> _events = [];

    public SlidingEventWindow(TimeSpan window)
        => _window = window <= TimeSpan.Zero ? TimeSpan.FromSeconds(60) : window;

    public void Add(FlowEvent flowEvent, DateTimeOffset now)
    {
        _events.Add(flowEvent);
        Prune(now);
    }

    public void Prune(DateTimeOffset now)
    {
        var threshold = now.Subtract(_window);
        _events.RemoveAll(flowEvent => flowEvent.Timestamp < threshold);
    }

    public int Count => _events.Count;

    public int DistinctTopicCount => _events
        .Where(static flowEvent => !string.IsNullOrWhiteSpace(flowEvent.Channel))
        .Select(static flowEvent => flowEvent.Channel!)
        .Distinct(StringComparer.Ordinal)
        .Count();

    public long TotalPayloadBytes => _events.Sum(static flowEvent => (long)(flowEvent.PayloadBytes ?? 0));
}
