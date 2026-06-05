namespace FluxMq.Scenarios;

public sealed class ScenarioStepRunnerRegistry
{
    private readonly Dictionary<string, IScenarioStepRunner> _runners = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, IScenarioStepRunner> Runners => _runners;

    public static ScenarioStepRunnerRegistry CreateEventExpectationOnly()
        => new ScenarioStepRunnerRegistry()
            .Register(new WhenEventScenarioStepRunner())
            .Register(new ExpectEventScenarioStepRunner())
            .Register(new EventExpectationScenarioStepRunner(
                ScenarioStepTypes.WaitForEvent,
                skipOnTimeout: false,
                "Expected event observed."))
            .Register(new EventExpectationScenarioStepRunner(
                ScenarioStepTypes.ConditionalEvent,
                skipOnTimeout: true,
                "Conditional event matched; continuing scenario."))
            .Register(new EventExpectationScenarioStepRunner(
                ScenarioStepTypes.PayloadAssertion,
                skipOnTimeout: false,
                "Payload assertion matched."))
            .Register(new EventExpectationScenarioStepRunner(
                ScenarioStepTypes.JsonSchemaAssertion,
                skipOnTimeout: false,
                "JSON schema assertion matched."))
            .Register(new MetricThresholdScenarioStepRunner())
            .Register(new DelayScenarioStepRunner())
            .Register(new CleanupActionScenarioStepRunner());

    public ScenarioStepRunnerRegistry Register(IScenarioStepRunner runner)
    {
        ArgumentNullException.ThrowIfNull(runner);
        if (string.IsNullOrWhiteSpace(runner.Type))
        {
            throw new ArgumentException("Scenario step runner type cannot be empty.", nameof(runner));
        }

        _runners[runner.Type] = runner;
        return this;
    }

    public bool TryGet(string type, out IScenarioStepRunner runner)
        => _runners.TryGetValue(type, out runner!);
}
