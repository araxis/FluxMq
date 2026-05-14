using FluxMq.Pipeline.Definitions;

namespace FluxMq.Pipeline.Runtime;

public sealed record WorkflowStateChanged(
    WorkflowName WorkflowName,
    WorkflowState Previous,
    WorkflowState Current,
    Exception? Exception = null)
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}
