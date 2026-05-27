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
    string? Context = null,
    string Scope = WorkspaceLogScopes.App,
    string? ArtifactKind = null,
    string? ArtifactName = null)
{
    public static WorkspaceLogEntry FromDiagnostic(
        WorkspaceDiagnostic diagnostic,
        string? scope = null,
        string? artifactKind = null,
        string? artifactName = null)
        => new(
            DateTimeOffset.UtcNow,
            diagnostic.Severity,
            diagnostic.Source,
            diagnostic.Code,
            diagnostic.Message,
            diagnostic.WorkflowName,
            diagnostic.NodeName,
            diagnostic.PortName,
            null,
            scope ?? InferScope(diagnostic),
            artifactKind ?? InferArtifactKind(diagnostic),
            artifactName ?? diagnostic.WorkflowName);

    private static string InferScope(WorkspaceDiagnostic diagnostic)
        => diagnostic.Source.Trim().ToLowerInvariant() switch
        {
            "scenario" => WorkspaceLogScopes.TestRunner,
            "runtime" or "runtimebuild" => WorkspaceLogScopes.App,
            "host" or "definition" or "validation" or "designer" or "file" => WorkspaceLogScopes.System,
            _ => string.IsNullOrWhiteSpace(diagnostic.WorkflowName)
                ? WorkspaceLogScopes.System
                : WorkspaceLogScopes.App
        };

    private static string? InferArtifactKind(WorkspaceDiagnostic diagnostic)
        => !string.IsNullOrWhiteSpace(diagnostic.WorkflowName)
            ? WorkspaceLogArtifactKinds.Pipeline
            : null;
}
