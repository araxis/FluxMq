namespace FluxMq.Scenarios;

public sealed class WhenEventScenarioStepRunner : IScenarioStepRunner
{
    public const string StepType = ScenarioStepTypes.WhenEvent;

    public string Type => StepType;

    public async Task<ScenarioStepResult> RunAsync(
        ScenarioStepRunContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var startedAt = DateTimeOffset.UtcNow;
        var expectation = FlowEventExpectation.FromConfiguration(context.Step.Configuration);
        var match = await context.Events.WaitForMatchAsync(
            context.EventOffset,
            expectation.Matches,
            expectation.Timeout,
            context.ConsumedEventIndexes,
            cancellationToken).ConfigureAwait(false);

        var finishedAt = DateTimeOffset.UtcNow;
        if (match is null)
        {
            var observedEvents = context.Events.SnapshotFrom(
                context.EventOffset,
                excludedIndexes: context.ConsumedEventIndexes);
            return new ScenarioStepResult
            {
                Name = context.StepName,
                Type = Type,
                Status = ScenarioStepRunStatus.Skipped,
                StartedAt = startedAt,
                FinishedAt = finishedAt,
                Message = FlowEventExpectationMessages.DescribeWhenSkipped(expectation, observedEvents),
                NextEventOffset = context.EventOffset
            };
        }

        return new ScenarioStepResult
        {
            Name = context.StepName,
            Type = Type,
            Status = ScenarioStepRunStatus.Passed,
            StartedAt = startedAt,
            FinishedAt = finishedAt,
            Message = "When condition matched; continuing scenario.",
            MatchedEvent = match.Event,
            MatchedEventIndex = match.Index,
            NextEventOffset = context.EventOffset
        };
    }
}
