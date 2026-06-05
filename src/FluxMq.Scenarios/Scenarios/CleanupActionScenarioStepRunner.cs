namespace FluxMq.Scenarios;

public sealed class CleanupActionScenarioStepRunner : IScenarioStepRunner
{
    public string Type => ScenarioStepTypes.CleanupAction;

    public async Task<ScenarioStepResult> RunAsync(
        ScenarioStepRunContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var startedAt = DateTimeOffset.UtcNow;
        var graceMs = ScenarioStepConfigurationReader.ReadIntOrDefault(
            context.Step.Configuration,
            ScenarioStepConfigurationKeys.DelayMs,
            0,
            0);

        if (graceMs > 0)
        {
            await Task.Delay(graceMs, cancellationToken).ConfigureAwait(false);
        }

        return new ScenarioStepResult
        {
            Name = context.StepName,
            Type = Type,
            Status = ScenarioStepRunStatus.Passed,
            StartedAt = startedAt,
            FinishedAt = DateTimeOffset.UtcNow,
            Message = graceMs > 0 ? $"Cleanup grace waited {graceMs} ms." : "Cleanup action completed.",
            NextEventOffset = context.EventOffset
        };
    }
}
