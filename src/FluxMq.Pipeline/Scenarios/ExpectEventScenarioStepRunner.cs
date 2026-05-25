namespace FluxMq.Pipeline.Scenarios;

public sealed class ExpectEventScenarioStepRunner : IScenarioStepRunner
{
    public const string StepType = "expect.event";

    public string Type => StepType;

    public async Task<ScenarioStepResult> RunAsync(
        ScenarioStepRunContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var startedAt = DateTimeOffset.UtcNow;
        var expectation = ReadExpectation(context.Step.Configuration);
        var match = await context.Events.WaitForMatchAsync(
            context.EventOffset,
            expectation.Matches,
            expectation.Timeout,
            cancellationToken).ConfigureAwait(false);

        var finishedAt = DateTimeOffset.UtcNow;
        if (match is null)
        {
            return new ScenarioStepResult
            {
                Name = context.StepName,
                Type = Type,
                Status = ScenarioStepRunStatus.TimedOut,
                StartedAt = startedAt,
                FinishedAt = finishedAt,
                Message = DescribeTimeout(expectation),
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
            NextEventOffset = match.Index + 1
        };
    }

    private static FlowEventExpectation ReadExpectation(
        IReadOnlyDictionary<string, System.Text.Json.JsonElement> configuration)
        => new()
        {
            EventType = ScenarioStepConfigurationReader.ReadString(configuration, "eventType"),
            TopicStartsWith = ScenarioStepConfigurationReader.ReadString(configuration, "topicStartsWith"),
            SubjectStartsWith = ScenarioStepConfigurationReader.ReadString(configuration, "subjectStartsWith"),
            Status = ScenarioStepConfigurationReader.ReadString(configuration, "status"),
            Source = ScenarioStepConfigurationReader.ReadString(configuration, "source"),
            PayloadContains = ScenarioStepConfigurationReader.ReadString(configuration, "payloadContains"),
            Attributes = ScenarioStepConfigurationReader.ReadStringMap(configuration, "attributes"),
            Timeout = TimeSpan.FromMilliseconds(ScenarioStepConfigurationReader.ReadIntOrDefault(
                configuration,
                "timeoutMs",
                5000,
                1))
        };

    private static string DescribeTimeout(FlowEventExpectation expectation)
    {
        var parts = new List<string>();
        Add(parts, "type", expectation.EventType);
        Add(parts, "topic", expectation.TopicStartsWith);
        Add(parts, "subject", expectation.SubjectStartsWith);
        Add(parts, "status", expectation.Status);
        Add(parts, "source", expectation.Source);
        Add(parts, "payload", expectation.PayloadContains);

        foreach (var attribute in expectation.Attributes)
        {
            Add(parts, $"attribute {attribute.Key}", attribute.Value);
        }

        var detail = parts.Count == 0
            ? "any event"
            : string.Join(", ", parts);

        return $"Expected event was not observed within {expectation.Timeout.TotalMilliseconds:0} ms ({detail}).";
    }

    private static void Add(List<string> parts, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parts.Add($"{label} '{value}'");
        }
    }
}
