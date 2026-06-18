namespace FluxMq.UI.Models;

public sealed record WorkspaceLogQuery(
    string Scope = WorkspaceLogFilter.All,
    string Severity = WorkspaceLogFilter.All,
    string? Search = null);

public static class WorkspaceLogFilter
{
    public const string All = "All";
    public const string Problems = "Problems";

    public static IReadOnlyList<WorkspaceLogEntry> Apply(
        IEnumerable<WorkspaceLogEntry> logs,
        WorkspaceLogQuery query,
        int maxEntries = 200)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxEntries);

        return logs
            .Where(entry => MatchesScope(entry, query.Scope))
            .Where(entry => MatchesSeverity(entry, query.Severity))
            .Where(entry => MatchesSearch(entry, query.Search))
            .Reverse()
            .Take(maxEntries)
            .ToArray();
    }

    public static string NormalizeSeverity(string? severity)
    {
        var normalized = severity?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "Info";
        }

        return normalized.ToLowerInvariant() switch
        {
            "fatal" => "Critical",
            "critical" => "Critical",
            "warn" => "Warning",
            "warning" => "Warning",
            "err" => "Error",
            "error" => "Error",
            "debug" => "Debug",
            "trace" => "Trace",
            _ => "Info"
        };
    }

    public static string NormalizeSeverity(WorkspaceLogEntry entry)
        => NormalizeSeverity(entry.Severity);

    private static bool MatchesScope(WorkspaceLogEntry entry, string? scope)
        => string.IsNullOrWhiteSpace(scope) ||
           string.Equals(scope, All, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(entry.Scope, scope, StringComparison.OrdinalIgnoreCase);

    private static bool MatchesSeverity(WorkspaceLogEntry entry, string? severity)
    {
        if (string.IsNullOrWhiteSpace(severity) ||
            string.Equals(severity, All, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var normalized = NormalizeSeverity(entry);
        return string.Equals(severity, Problems, StringComparison.OrdinalIgnoreCase)
            ? normalized is "Critical" or "Error" or "Warning"
            : string.Equals(normalized, NormalizeSeverity(severity), StringComparison.Ordinal);
    }

    private static bool MatchesSearch(WorkspaceLogEntry entry, string? search)
    {
        var filter = search?.Trim();
        if (string.IsNullOrWhiteSpace(filter))
        {
            return true;
        }

        return Contains(entry.Message, filter) ||
               Contains(entry.Source, filter) ||
               Contains(entry.Code, filter) ||
               Contains(entry.Scope, filter) ||
               Contains(entry.ArtifactKind, filter) ||
               Contains(entry.ArtifactName, filter) ||
               Contains(entry.WorkflowName, filter) ||
               Contains(entry.NodeName, filter) ||
               Contains(entry.PortName, filter) ||
               Contains(entry.Context, filter);
    }

    private static bool Contains(string? value, string filter)
        => value?.Contains(filter, StringComparison.OrdinalIgnoreCase) == true;
}
