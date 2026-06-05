namespace FluxMq.Components.Storage.Models;

public sealed class StoredScenarioRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ProjectName { get; set; } = "Default";
    public string ScenarioName { get; set; } = string.Empty;
    public string RunId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset FinishedAt { get; set; }
    public double DurationMilliseconds { get; set; }
    public int StepCount { get; set; }
    public int FailedStepCount { get; set; }
    public string ReportJson { get; set; } = string.Empty;
    public string ReportText { get; set; } = string.Empty;
    public string LogExcerpt { get; set; } = string.Empty;
}
