namespace FluxMq.UI.Models;

public sealed record WorkspaceDiagnostic(
    string Severity,
    string Source,
    string Code,
    string Message);
