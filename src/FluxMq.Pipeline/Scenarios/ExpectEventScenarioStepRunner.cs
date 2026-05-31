using FluxFlow.Engine.Components;

namespace FluxMq.Pipeline.Scenarios;

public sealed class ExpectEventScenarioStepRunner : IScenarioStepRunner
{
    public const string StepType = ScenarioStepTypes.ExpectEvent;

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
                Status = ScenarioStepRunStatus.TimedOut,
                StartedAt = startedAt,
                FinishedAt = finishedAt,
                Message = FlowEventExpectationMessages.DescribeExpectTimeout(expectation, observedEvents),
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
            Message = "Expected event observed.",
            MatchedEvent = match.Event,
            MatchedEventIndex = match.Index,
            NextEventOffset = context.EventOffset
        };
    }
}
