namespace FluxMq.Cli;

public sealed record RunFlowCommandResult(
    bool Started,
    int WorkflowCount,
    int ResourceCount,
    string ExitReason,
    string HostState,
    IReadOnlyList<ValidateFlowDiagnostic> Diagnostics);
