using FluxMq.Pipeline.Components;
using FluxMq.Pipeline.Scenarios;
using FluxMq.UI.Models;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace FluxMq.UI.Services;

public static class ScenarioRunReportFormatter
{
    public const string CurrentSchemaVersion = "scenario-run-report.v1";

    private static readonly IReadOnlyDictionary<string, string> EmptyConfiguration =
        new Dictionary<string, string>(StringComparer.Ordinal);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static ScenarioRunReport Create(
        ScenarioRunResult result,
        TestScenarioSnapshot? scenario = null,
        DateTimeOffset? generatedAt = null)
    {
        ArgumentNullException.ThrowIfNull(result);

        var scenarioSteps = ScenarioSteps(result, scenario);
        var scenarioStepsByName = ScenarioStepsByName(scenarioSteps);
        var steps = result.Steps
            .Select((step, index) => CreateStep(step, index + 1, result.StartedAt, scenarioStepsByName))
            .ToArray();
        var plannedSteps = CreatePlannedSteps(scenarioSteps);
        var notRunSteps = CreateNotRunSteps(result.Steps, plannedSteps);

        return new ScenarioRunReport(
            CurrentSchemaVersion,
            CreateRunId(result),
            generatedAt ?? DateTimeOffset.UtcNow,
            result.Name,
            result.IsSuccess,
            result.Status.ToString(),
            result.StartedAt,
            result.FinishedAt,
            (result.FinishedAt - result.StartedAt).TotalMilliseconds,
            CreateStepSummary(result.Steps, plannedSteps, notRunSteps),
            plannedSteps,
            CreateIssues(steps),
            notRunSteps,
            CreateFirstIssue(steps),
            steps);
    }

    public static string ToJson(
        ScenarioRunResult result,
        TestScenarioSnapshot? scenario = null,
        DateTimeOffset? generatedAt = null)
        => ToJson(Create(result, scenario, generatedAt));

    public static string ToJson(ScenarioRunReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return JsonSerializer.Serialize(report, JsonOptions);
    }

    public static string ToText(
        ScenarioRunResult result,
        TestScenarioSnapshot? scenario = null,
        DateTimeOffset? generatedAt = null)
        => ToText(Create(result, scenario, generatedAt));

    public static string ToText(ScenarioRunReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var builder = new StringBuilder();
        builder.AppendLine(
            $"Report {report.SchemaVersion} run {report.RunId} generated {report.GeneratedAt:O}.");
        builder.AppendLine(
            $"Scenario '{report.Name}' {report.Status}. Steps: {FormatStepSummary(report.StepSummary)}. Duration: {report.DurationMilliseconds:0} ms.");

        if (report.FirstIssue is not null)
        {
            var firstIssueMessage = string.IsNullOrWhiteSpace(report.FirstIssue.Message)
                ? string.Empty
                : $": {report.FirstIssue.Message}";
            builder.AppendLine(
                $"First issue: {report.FirstIssue.StepName} [{report.FirstIssue.StepType}] {report.FirstIssue.Status}{firstIssueMessage}");
        }

        if (report.Issues.Count > 0)
        {
            builder.AppendLine($"Issues: {report.Issues.Count}.");
            foreach (var issue in report.Issues)
            {
                builder.AppendLine($"- {FormatIssue(issue)}");
            }
        }

        if (report.NotRunSteps.Count > 0)
        {
            builder.AppendLine($"Not run: {report.NotRunSteps.Count}.");
            foreach (var step in report.NotRunSteps)
            {
                builder.AppendLine($"- #{step.Sequence} {step.StepName} [{step.StepType}]");
                if (step.Configuration.Count > 0)
                {
                    builder.AppendLine($"  config: {FormatConfiguration(step.Configuration)}");
                }
            }
        }

        foreach (var step in report.Steps)
        {
            var message = string.IsNullOrWhiteSpace(step.Message) ? string.Empty : $": {step.Message}";
            builder.AppendLine($"- {step.Name} [{step.Type}] {step.Status}{message}");

            if (step.Configuration.Count > 0)
            {
                builder.AppendLine($"  config: {FormatConfiguration(step.Configuration)}");
            }

            builder.AppendLine(
                $"  timing: #{step.Sequence}, start {FormatOffset(step.StartedOffsetMilliseconds)} ms, finish {FormatOffset(step.FinishedOffsetMilliseconds)} ms, duration {step.DurationMilliseconds:0} ms");

            if (step.MatchedEvent is not null)
            {
                AppendMatchedEvent(builder, step.MatchedEvent);
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static ScenarioStepRunReport CreateStep(
        ScenarioStepResult step,
        int sequence,
        DateTimeOffset scenarioStartedAt,
        IReadOnlyDictionary<string, ScenarioStepSnapshot> scenarioSteps)
    {
        scenarioSteps.TryGetValue(step.Name, out var scenarioStep);
        return new ScenarioStepRunReport(
            step.Name,
            step.Type,
            sequence,
            CopyConfiguration(scenarioStep),
            step.Status.ToString(),
            step.StartedAt,
            step.FinishedAt,
            (step.StartedAt - scenarioStartedAt).TotalMilliseconds,
            (step.FinishedAt - scenarioStartedAt).TotalMilliseconds,
            (step.FinishedAt - step.StartedAt).TotalMilliseconds,
            step.Message,
            step.MatchedEvent is null ? null : CreateEvent(step.MatchedEvent, scenarioStartedAt, step.StartedAt));
    }

    private static IReadOnlyList<ScenarioStepSnapshot> ScenarioSteps(
        ScenarioRunResult result,
        TestScenarioSnapshot? scenario)
    {
        if (scenario is null || !string.Equals(result.Name, scenario.Name, StringComparison.Ordinal))
        {
            return [];
        }

        return scenario.Steps;
    }

    private static IReadOnlyDictionary<string, ScenarioStepSnapshot> ScenarioStepsByName(
        IReadOnlyList<ScenarioStepSnapshot> scenarioSteps)
        => scenarioSteps
            .GroupBy(step => step.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, string> CopyConfiguration(ScenarioStepSnapshot? step)
    {
        if (step?.Configuration.Count is not > 0)
        {
            return EmptyConfiguration;
        }

        return step.Configuration
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
    }

    private static string FormatConfiguration(IReadOnlyDictionary<string, string> configuration)
        => string.Join(
            "; ",
            configuration
                .OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => $"{item.Key}={item.Value}"));

    private static string FormatOffset(double milliseconds)
        => milliseconds >= 0 ? $"+{milliseconds:0}" : $"{milliseconds:0}";

    private static void AppendMatchedEvent(StringBuilder builder, ScenarioMatchedEventReport matchedEvent)
    {
        var eventOffsets =
            $"({FormatOffset(matchedEvent.ScenarioOffsetMilliseconds)} ms scenario, {FormatOffset(matchedEvent.StepOffsetMilliseconds)} ms step)";
        builder.AppendLine(
            $"  matched: {matchedEvent.Type} / {matchedEvent.Topic ?? "no topic"} / {matchedEvent.Status ?? "no status"} {eventOffsets}");

        var details = new List<string>
        {
            $"source={matchedEvent.Source}",
            $"subject={matchedEvent.Subject ?? "no subject"}"
        };

        if (matchedEvent.PayloadBytes is not null)
        {
            details.Add($"payloadBytes={matchedEvent.PayloadBytes.Value}");
        }

        if (!string.IsNullOrWhiteSpace(matchedEvent.PayloadPreview))
        {
            details.Add($"payload={matchedEvent.PayloadPreview}");
        }

        builder.AppendLine($"  event: {string.Join("; ", details)}");

        if (matchedEvent.Attributes.Count > 0)
        {
            builder.AppendLine($"  attributes: {FormatConfiguration(matchedEvent.Attributes)}");
        }
    }

    private static string CreateRunId(ScenarioRunResult result)
    {
        var startedAt = result.StartedAt
            .ToUniversalTime()
            .ToString("yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture);

        return $"{SanitizeRunIdPart(result.Name)}-{startedAt}";
    }

    private static string SanitizeRunIdPart(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim())
        {
            var normalized = char.ToLowerInvariant(character);
            if ((normalized >= 'a' && normalized <= 'z') ||
                (normalized >= '0' && normalized <= '9'))
            {
                builder.Append(normalized);
                continue;
            }

            if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        while (builder.Length > 0 && builder[^1] == '-')
        {
            builder.Length--;
        }

        return builder.Length == 0 ? "scenario" : builder.ToString();
    }

    private static string FormatIssue(ScenarioIssueReport issue)
    {
        var message = string.IsNullOrWhiteSpace(issue.Message) ? string.Empty : $": {issue.Message}";
        return $"#{issue.Sequence} {issue.StepName} [{issue.StepType}] {issue.Status}{message}";
    }

    private static ScenarioStepStatusSummary CreateStepSummary(
        IReadOnlyList<ScenarioStepResult> steps,
        IReadOnlyList<ScenarioPlannedStepReport> plannedSteps,
        IReadOnlyList<ScenarioNotRunStepReport> notRunSteps)
    {
        var executed = steps.Count;
        var planned = plannedSteps.Count > 0 ? plannedSteps.Count : executed;

        return new ScenarioStepStatusSummary(
            executed,
            planned,
            executed,
            notRunSteps.Count,
            steps.Count(step => step.Status == ScenarioStepRunStatus.Passed),
            steps.Count(step => step.Status == ScenarioStepRunStatus.Skipped),
            steps.Count(step => step.Status == ScenarioStepRunStatus.Failed),
            steps.Count(step => step.Status == ScenarioStepRunStatus.TimedOut),
            steps.Count(step => step.Status == ScenarioStepRunStatus.Canceled));
    }

    private static string FormatStepSummary(ScenarioStepStatusSummary summary)
    {
        var parts = new List<string>();
        if (summary.Planned != summary.Executed || summary.NotRun > 0)
        {
            parts.Add($"{summary.Planned} planned");
            parts.Add($"{summary.Executed} run");

            if (summary.NotRun > 0)
            {
                parts.Add($"{summary.NotRun} not run");
            }
        }
        else
        {
            parts.Add($"{summary.Total} total");
        }

        parts.Add($"{summary.Passed} passed");

        if (summary.Skipped > 0)
        {
            parts.Add($"{summary.Skipped} skipped");
        }

        if (summary.Failed > 0)
        {
            parts.Add($"{summary.Failed} failed");
        }

        if (summary.TimedOut > 0)
        {
            parts.Add($"{summary.TimedOut} timed out");
        }

        if (summary.Canceled > 0)
        {
            parts.Add($"{summary.Canceled} canceled");
        }

        return string.Join(", ", parts);
    }

    private static ScenarioFirstIssueReport? CreateFirstIssue(IReadOnlyList<ScenarioStepRunReport> steps)
    {
        var firstIssue = steps.FirstOrDefault(step =>
            IsIssueStatus(step.Status));

        return firstIssue is null
            ? null
            : new ScenarioFirstIssueReport(
                firstIssue.Name,
                firstIssue.Type,
                firstIssue.Status,
                firstIssue.Message);
    }

    private static IReadOnlyList<ScenarioIssueReport> CreateIssues(IReadOnlyList<ScenarioStepRunReport> steps)
        => steps
            .Where(step => IsIssueStatus(step.Status))
            .Select(step => new ScenarioIssueReport(
                step.Sequence,
                step.Name,
                step.Type,
                step.Status,
                step.Message))
            .ToArray();

    private static bool IsIssueStatus(string status)
        => !string.Equals(status, ScenarioStepRunStatus.Passed.ToString(), StringComparison.Ordinal) &&
           !string.Equals(status, ScenarioStepRunStatus.Skipped.ToString(), StringComparison.Ordinal);

    private static IReadOnlyList<ScenarioPlannedStepReport> CreatePlannedSteps(
        IReadOnlyList<ScenarioStepSnapshot> scenarioSteps)
        => scenarioSteps
            .Select((step, index) => new ScenarioPlannedStepReport(
                index + 1,
                step.Name,
                step.Type,
                CopyConfiguration(step)))
            .ToArray();

    private static IReadOnlyList<ScenarioNotRunStepReport> CreateNotRunSteps(
        IReadOnlyList<ScenarioStepResult> executedSteps,
        IReadOnlyList<ScenarioPlannedStepReport> plannedSteps)
    {
        if (plannedSteps.Count == 0)
        {
            return [];
        }

        var executedNames = executedSteps
            .Select(step => step.Name)
            .ToHashSet(StringComparer.Ordinal);

        return plannedSteps
            .Where(step => !executedNames.Contains(step.StepName))
            .Select(item => new ScenarioNotRunStepReport(
                item.Sequence,
                item.StepName,
                item.StepType,
                item.Configuration))
            .ToArray();
    }

    private static ScenarioMatchedEventReport CreateEvent(
        FlowEvent flowEvent,
        DateTimeOffset scenarioStartedAt,
        DateTimeOffset stepStartedAt)
        => new(
            flowEvent.Timestamp,
            (flowEvent.Timestamp - scenarioStartedAt).TotalMilliseconds,
            (flowEvent.Timestamp - stepStartedAt).TotalMilliseconds,
            flowEvent.Type,
            flowEvent.Source,
            flowEvent.Subject,
            flowEvent.Status,
            flowEvent.Topic,
            flowEvent.PayloadBytes,
            flowEvent.PayloadPreview,
            flowEvent.Attributes);
}

public sealed record ScenarioRunReport(
    string SchemaVersion,
    string RunId,
    DateTimeOffset GeneratedAt,
    string Name,
    bool IsSuccess,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    double DurationMilliseconds,
    ScenarioStepStatusSummary StepSummary,
    IReadOnlyList<ScenarioPlannedStepReport> PlannedSteps,
    IReadOnlyList<ScenarioIssueReport> Issues,
    IReadOnlyList<ScenarioNotRunStepReport> NotRunSteps,
    ScenarioFirstIssueReport? FirstIssue,
    IReadOnlyList<ScenarioStepRunReport> Steps);

public sealed record ScenarioStepStatusSummary(
    int Total,
    int Planned,
    int Executed,
    int NotRun,
    int Passed,
    int Skipped,
    int Failed,
    int TimedOut,
    int Canceled);

public sealed record ScenarioFirstIssueReport(
    string StepName,
    string StepType,
    string Status,
    string? Message);

public sealed record ScenarioIssueReport(
    int Sequence,
    string StepName,
    string StepType,
    string Status,
    string? Message);

public sealed record ScenarioPlannedStepReport(
    int Sequence,
    string StepName,
    string StepType,
    IReadOnlyDictionary<string, string> Configuration);

public sealed record ScenarioNotRunStepReport(
    int Sequence,
    string StepName,
    string StepType,
    IReadOnlyDictionary<string, string> Configuration);

public sealed record ScenarioStepRunReport(
    string Name,
    string Type,
    int Sequence,
    IReadOnlyDictionary<string, string> Configuration,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    double StartedOffsetMilliseconds,
    double FinishedOffsetMilliseconds,
    double DurationMilliseconds,
    string? Message,
    ScenarioMatchedEventReport? MatchedEvent);

public sealed record ScenarioMatchedEventReport(
    DateTimeOffset Timestamp,
    double ScenarioOffsetMilliseconds,
    double StepOffsetMilliseconds,
    string Type,
    string Source,
    string? Subject,
    string? Status,
    string? Topic,
    int? PayloadBytes,
    string? PayloadPreview,
    IReadOnlyDictionary<string, string> Attributes);
