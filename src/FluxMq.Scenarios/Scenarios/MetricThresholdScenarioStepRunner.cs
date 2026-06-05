using FluxFlow.Engine.Components;
using System.Globalization;
using System.Text;

namespace FluxMq.Scenarios;

public sealed class MetricThresholdScenarioStepRunner : IScenarioStepRunner
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    public string Type => ScenarioStepTypes.MetricThresholdAssertion;

    public async Task<ScenarioStepResult> RunAsync(
        ScenarioStepRunContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var startedAt = DateTimeOffset.UtcNow;
        var expectation = FlowEventExpectation.FromConfiguration(context.Step.Configuration);
        var aggregation = ScenarioStepConfigurationReader.ReadString(
            context.Step.Configuration,
            ScenarioStepConfigurationKeys.Aggregation) ?? "count";
        var comparisonOperator = ScenarioStepConfigurationReader.ReadString(
            context.Step.Configuration,
            ScenarioStepConfigurationKeys.Operator) ?? ">=";
        var threshold = ReadThreshold(context.Step.Configuration);
        var timeout = TimeSpan.FromMilliseconds(ScenarioStepConfigurationReader.ReadIntOrDefault(
            context.Step.Configuration,
            ScenarioStepConfigurationKeys.WindowMs,
            5000,
            1));
        var deadline = startedAt + timeout;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var matches = context.Events.SnapshotMatchingFrom(
                context.EventOffset,
                expectation.Matches,
                context.ConsumedEventIndexes);
            var value = CalculateMetric(matches, aggregation, timeout);
            if (Compare(value, threshold, comparisonOperator))
            {
                return new ScenarioStepResult
                {
                    Name = context.StepName,
                    Type = Type,
                    Status = ScenarioStepRunStatus.Passed,
                    StartedAt = startedAt,
                    FinishedAt = DateTimeOffset.UtcNow,
                    Message = $"Metric {aggregation} was {FormatNumber(value)} {comparisonOperator} {FormatNumber(threshold)}.",
                    NextEventOffset = context.EventOffset
                };
            }

            if (DateTimeOffset.UtcNow >= deadline)
            {
                return new ScenarioStepResult
                {
                    Name = context.StepName,
                    Type = Type,
                    Status = ScenarioStepRunStatus.TimedOut,
                    StartedAt = startedAt,
                    FinishedAt = DateTimeOffset.UtcNow,
                    Message = $"Metric {aggregation} was {FormatNumber(value)} and did not satisfy {comparisonOperator} {FormatNumber(threshold)}.",
                    NextEventOffset = context.EventOffset
                };
            }

            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    private static double CalculateMetric(
        IReadOnlyList<FlowEvent> events,
        string aggregation,
        TimeSpan window)
        => aggregation switch
        {
            "rate" => window.TotalSeconds <= 0 ? 0 : events.Count / window.TotalSeconds,
            "payloadBytes" => events.Sum(static flowEvent => Encoding.UTF8.GetByteCount(flowEvent.PayloadPreview ?? string.Empty)),
            "topics" => events.Select(static flowEvent => flowEvent.Channel)
                .Where(static channel => !string.IsNullOrWhiteSpace(channel))
                .Distinct(StringComparer.Ordinal)
                .Count(),
            _ => events.Count
        };

    private static double ReadThreshold(IReadOnlyDictionary<string, System.Text.Json.JsonElement> configuration)
    {
        if (!configuration.TryGetValue(ScenarioStepConfigurationKeys.Threshold, out var threshold))
        {
            return 1;
        }

        if (threshold.ValueKind == System.Text.Json.JsonValueKind.Number &&
            threshold.TryGetDouble(out var number))
        {
            return number;
        }

        if (threshold.ValueKind == System.Text.Json.JsonValueKind.String &&
            double.TryParse(
                threshold.GetString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var textNumber))
        {
            return textNumber;
        }

        throw new InvalidOperationException("Scenario configuration value 'threshold' must be a number.");
    }

    private static bool Compare(double actual, double expected, string comparisonOperator)
        => comparisonOperator switch
        {
            ">" => actual > expected,
            "<=" => actual <= expected,
            "<" => actual < expected,
            "=" => Math.Abs(actual - expected) < 0.000001d,
            _ => actual >= expected
        };

    private static string FormatNumber(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);
}
