using FluxMq.Core.Ids;

namespace FluxMq.Replay;

public sealed record RecordedSessionReplayOptions
{
    public FlowNodeId? NodeId { get; init; }
    public double Speed { get; init; } = 1;
    public int BoundedCapacity { get; init; } = 1000;
}
