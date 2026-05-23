namespace FluxMq.UI.Models;

public sealed record WorkspaceDiagnostic(
    string Severity,
    string Source,
    string Code,
    string Message,
    string? WorkflowName = null,
    string? NodeName = null,
    string? PortName = null)
{
    public bool IsNodeScoped => !string.IsNullOrWhiteSpace(NodeName);
}
