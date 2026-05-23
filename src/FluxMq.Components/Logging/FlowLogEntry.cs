using FluxMq.Core.Ids;

namespace FluxMq.Components.Logging;

public sealed record FlowLogEntry
{
    public required DateTimeOffset Timestamp { get; init; }
    public required FlowLogSeverity Severity { get; init; }
    public required string Source { get; init; }
    public required string Message { get; init; }
    public FlowNodeId? RelatedNodeId { get; init; }
    public int? ErrorCode { get; init; }
    public string? Topic { get; init; }
    public int? PayloadBytes { get; init; }
    public string? PayloadPreview { get; init; }
    public string? Context { get; init; }
}
