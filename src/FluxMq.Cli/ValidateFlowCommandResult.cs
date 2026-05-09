namespace FluxMq.Cli;

public sealed record ValidateFlowCommandResult(
    bool IsValid,
    int WorkflowCount,
    int ResourceCount,
    IReadOnlyList<ValidateFlowDiagnostic> Diagnostics);

public sealed record ValidateFlowDiagnostic(
    string Source,
    string Code,
    string Message,
    string? WorkflowName = null,
    string? NodeName = null,
    string? PortName = null);
