namespace FluxMq.UI.Models;

public sealed record WorkspaceLogEntry(
    DateTimeOffset Timestamp,
    string Severity,
    string Source,
    string Code,
    string Message,
    string? WorkflowName = null,
    string? NodeName = null,
    string? PortName = null,
    string? Context = null)
{
    public static WorkspaceLogEntry FromDiagnostic(WorkspaceDiagnostic diagnostic)
        => new(
            DateTimeOffset.UtcNow,
            diagnostic.Severity,
            diagnostic.Source,
            diagnostic.Code,
            diagnostic.Message,
            diagnostic.WorkflowName,
            diagnostic.NodeName,
            diagnostic.PortName);
}
