namespace FluxMq.Scenarios;

public sealed class EventExpectationScenarioStepRunner(
    string type,
    bool skipOnTimeout,
    string successMessage)
    : IScenarioStepRunner
{
    public string Type { get; } = string.IsNullOrWhiteSpace(type)
        ? throw new ArgumentException("Scenario step runner type cannot be empty.", nameof(type))
        : type;

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
                Status = skipOnTimeout ? ScenarioStepRunStatus.Skipped : ScenarioStepRunStatus.TimedOut,
                StartedAt = startedAt,
                FinishedAt = finishedAt,
                Message = skipOnTimeout
                    ? FlowEventExpectationMessages.DescribeWhenSkipped(expectation, observedEvents)
                    : FlowEventExpectationMessages.DescribeExpectTimeout(expectation, observedEvents),
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
            Message = successMessage,
            MatchedEvent = match.Event,
            MatchedEventIndex = match.Index,
            NextEventOffset = context.EventOffset
        };
    }
}
