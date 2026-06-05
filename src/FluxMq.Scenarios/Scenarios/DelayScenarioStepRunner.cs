namespace FluxMq.Scenarios;

public sealed class DelayScenarioStepRunner : IScenarioStepRunner
{
    public string Type => ScenarioStepTypes.Delay;

    public async Task<ScenarioStepResult> RunAsync(
        ScenarioStepRunContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var startedAt = DateTimeOffset.UtcNow;
        var delayMs = ScenarioStepConfigurationReader.ReadIntOrDefault(
            context.Step.Configuration,
            ScenarioStepConfigurationKeys.DelayMs,
            1000,
            0);

        if (delayMs > 0)
        {
            await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
        }

        return new ScenarioStepResult
        {
            Name = context.StepName,
            Type = Type,
            Status = ScenarioStepRunStatus.Passed,
            StartedAt = startedAt,
            FinishedAt = DateTimeOffset.UtcNow,
            Message = delayMs > 0 ? $"Waited {delayMs} ms." : "No delay configured.",
            NextEventOffset = context.EventOffset
        };
    }
}
