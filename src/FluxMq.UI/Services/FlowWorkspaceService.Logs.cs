using FluxFlow.Engine.Components;
using FluxFlow.Engine.Definitions;
using FluxFlow.Engine.Runtime;
using FluxMq.Components.Logging;
using FluxMq.UI.Models;
using System.Text;

namespace FluxMq.UI.Services;

public sealed partial class FlowWorkspaceService
{
    private void AppendDiagnosticsToLogs(IEnumerable<WorkspaceDiagnostic> diagnostics, bool notify = true)
        => AppendLogs(diagnostics.Select(ToWorkspaceLogEntry), notify);

    private void AppendScenarioDiagnosticsToLogs(
        IEnumerable<WorkspaceDiagnostic> diagnostics,
        string scenarioName,
        bool notify = true)
        => AppendLogs(diagnostics.Select(diagnostic => WorkspaceLogEntry.FromDiagnostic(
            diagnostic,
            WorkspaceLogScopes.TestRunner,
            WorkspaceLogArtifactKinds.Test,
            scenarioName)), notify);

    private void AppendLog(WorkspaceLogEntry entry)
        => AppendLogs([entry]);

    private WorkspaceLogEntry ToWorkspaceLogEntry(WorkspaceDiagnostic diagnostic)
        => WorkspaceLogEntry.FromDiagnostic(
            diagnostic,
            artifactKind: ActivePipelineArtifactKindFor(diagnostic),
            artifactName: ActivePipelineArtifactNameFor(diagnostic));

    private string? ActivePipelineArtifactKindFor(WorkspaceDiagnostic diagnostic)
        => ShouldAttachActivePipelineArtifact(diagnostic) ? WorkspaceLogArtifactKinds.Pipeline : null;

    private string? ActivePipelineArtifactNameFor(WorkspaceDiagnostic diagnostic)
        => ShouldAttachActivePipelineArtifact(diagnostic) ? ActiveWorkflowName : null;

    private bool ShouldAttachActivePipelineArtifact(WorkspaceDiagnostic diagnostic)
        => string.IsNullOrWhiteSpace(diagnostic.WorkflowName) &&
           ActiveArtifactKind == WorkspaceArtifactKind.Pipeline &&
           !string.IsNullOrWhiteSpace(ActiveWorkflowName) &&
           IsProblemDiagnostic(diagnostic) &&
           IsValidationBuildDiagnostic(diagnostic);

    private static bool IsProblemDiagnostic(WorkspaceDiagnostic diagnostic)
        => string.Equals(diagnostic.Severity, "Error", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(diagnostic.Severity, "Warning", StringComparison.OrdinalIgnoreCase);

    private static bool IsValidationBuildDiagnostic(WorkspaceDiagnostic diagnostic)
        => diagnostic.Source.Trim().ToLowerInvariant() is
            "host" or "definition" or "runtimebuild" or "validation" or "runtime";

    private void AppendLogs(IEnumerable<WorkspaceLogEntry> entries, bool notify = true)
    {
        var materialized = entries.ToArray();
        if (materialized.Length == 0)
        {
            return;
        }

        lock (_logSync)
        {
            foreach (var entry in materialized)
            {
                _logs.Add(entry);
            }

            if (_logs.Count > MaxWorkspaceLogs)
            {
                _logs.RemoveRange(0, _logs.Count - MaxWorkspaceLogs);
            }

            Logs = _logs.ToArray();
        }

        if (notify)
        {
            NotifyChanged();
        }
    }

    private static WorkspaceLogEntry ToWorkspaceLogEntry(NodeAddress address, FlowLogEntry entry)
        => new(
            entry.Timestamp,
            entry.Severity.ToString(),
            entry.Source,
            entry.ErrorCode?.ToString() ?? entry.Source,
            entry.Message,
            address.Scope,
            address.Node.Value,
            null,
            BuildRuntimeLogContext(entry),
            WorkspaceLogScopes.App,
            WorkspaceLogArtifactKinds.Pipeline,
            address.Scope);

    private static WorkspaceLogEntry ToWorkspaceLogEntry(NodeAddress address, string portName, FlowError error)
        => new(
            error.OccurredAt,
            "Error",
            "FlowError",
            error.Code.ToString(),
            error.Message,
            address.Scope,
            address.Node.Value,
            portName,
            BuildFlowErrorContext(error),
            WorkspaceLogScopes.App,
            WorkspaceLogArtifactKinds.Pipeline,
            address.Scope);

    private static WorkspaceLogEntry ToWorkspaceLogEntry(FlowEvent flowEvent, NodeAddress? address)
        => new(
            flowEvent.Timestamp,
            "Info",
            string.IsNullOrWhiteSpace(flowEvent.Source) ? "RuntimeEvent" : flowEvent.Source,
            flowEvent.Type,
            BuildRuntimeEventMessage(flowEvent),
            address?.Scope,
            address?.Node.Value,
            null,
            BuildRuntimeEventContext(flowEvent),
            WorkspaceLogScopes.App,
            address is null ? null : WorkspaceLogArtifactKinds.Pipeline,
            address?.Scope);

    private static WorkspaceLogEntry ToScenarioWorkspaceLogEntry(FlowEvent flowEvent, string scenarioName)
        => new(
            flowEvent.Timestamp,
            "Info",
            string.IsNullOrWhiteSpace(flowEvent.Source) ? "ScenarioEvent" : flowEvent.Source,
            flowEvent.Type,
            BuildScenarioEventMessage(flowEvent),
            null,
            null,
            null,
            BuildRuntimeEventContext(flowEvent),
            WorkspaceLogScopes.TestRunner,
            WorkspaceLogArtifactKinds.Test,
            scenarioName);

    private static string BuildRuntimeEventMessage(FlowEvent flowEvent)
    {
        var target = !string.IsNullOrWhiteSpace(flowEvent.Channel)
            ? flowEvent.Channel
            : flowEvent.Subject;

        return string.IsNullOrWhiteSpace(target)
            ? $"Observed runtime event '{flowEvent.Type}'."
            : $"Observed runtime event '{flowEvent.Type}' for '{target}'.";
    }

    private static string BuildScenarioEventMessage(FlowEvent flowEvent)
    {
        var target = !string.IsNullOrWhiteSpace(flowEvent.Channel)
            ? flowEvent.Channel
            : flowEvent.Subject;

        return string.IsNullOrWhiteSpace(target)
            ? $"Observed scenario event '{flowEvent.Type}'."
            : $"Observed scenario event '{flowEvent.Type}' for '{target}'.";
    }

    private static string? BuildRuntimeLogContext(FlowLogEntry entry)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(entry.Topic))
        {
            parts.Add($"topic={entry.Topic}");
        }

        if (entry.PayloadBytes is not null)
        {
            parts.Add($"payloadBytes={entry.PayloadBytes}");
        }

        if (!string.IsNullOrWhiteSpace(entry.Context))
        {
            parts.Add(entry.Context);
        }

        if (!string.IsNullOrWhiteSpace(entry.PayloadPreview))
        {
            parts.Add($"payload={entry.PayloadPreview}");
        }

        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    private static string? BuildRuntimeEventContext(FlowEvent flowEvent)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(flowEvent.Channel))
        {
            parts.Add($"topic={flowEvent.Channel}");
        }

        if (!string.IsNullOrWhiteSpace(flowEvent.Subject) &&
            !string.Equals(flowEvent.Subject, flowEvent.Channel, StringComparison.Ordinal))
        {
            parts.Add($"subject={flowEvent.Subject}");
        }

        if (!string.IsNullOrWhiteSpace(flowEvent.Status))
        {
            parts.Add($"status={flowEvent.Status}");
        }

        if (flowEvent.PayloadBytes is not null)
        {
            parts.Add($"payloadBytes={flowEvent.PayloadBytes}");
        }

        foreach (var attribute in flowEvent.Attributes.OrderBy(attribute => attribute.Key, StringComparer.Ordinal))
        {
            parts.Add($"{attribute.Key}={attribute.Value}");
        }

        if (!string.IsNullOrWhiteSpace(flowEvent.PayloadPreview))
        {
            parts.Add($"payload={flowEvent.PayloadPreview}");
        }

        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    private static string? CreatePayloadPreview(byte[] payload)
    {
        if (payload.Length == 0)
        {
            return string.Empty;
        }

        try
        {
            var text = StrictUtf8.GetString(payload);
            return text.Length <= MaxFlowEventPayloadPreviewChars
                ? text
                : text[..MaxFlowEventPayloadPreviewChars];
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    private static string? BuildFlowErrorContext(FlowError error)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(error.Context))
        {
            parts.Add(error.Context);
        }

        if (error.Exception is not null)
        {
            parts.Add($"exception={error.Exception.GetType().Name}: {error.Exception.Message}");
        }

        return parts.Count == 0 ? null : string.Join("; ", parts);
    }
}
