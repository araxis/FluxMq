using FluxMq.Pipeline.Definitions;

namespace FluxMq.Pipeline.Runtime;

public sealed record FlowApplicationRuntimeBuildError(
    FlowApplicationRuntimeBuildErrorCode Code,
    string Message,
    string? WorkflowName = null,
    FlowNodeName? NodeName = null,
    FlowPortName? PortName = null);
