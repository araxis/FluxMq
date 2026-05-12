using FluxMq.Pipeline.Definitions;

namespace FluxMq.Pipeline.Runtime;

public sealed record ApplicationRuntimeBuildError(
    ApplicationRuntimeBuildErrorCode Code,
    string Message,
    string? WorkflowName = null,
    NodeName? NodeName = null,
    PortName? PortName = null);
