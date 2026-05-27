namespace FluxMq.Pipeline.Scenarios;

public sealed class ScenarioStepRunnerRegistry
{
    private readonly Dictionary<string, IScenarioStepRunner> _runners = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, IScenarioStepRunner> Runners => _runners;

    public static ScenarioStepRunnerRegistry CreateDefault()
        => new ScenarioStepRunnerRegistry()
            .Register(new ExpectEventScenarioStepRunner());

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

    public ScenarioStepRunnerRegistry RegisterAlias(string aliasType, string registeredType)
    {
        if (string.IsNullOrWhiteSpace(aliasType))
        {
            throw new ArgumentException("Scenario step runner alias type cannot be empty.", nameof(aliasType));
        }

        if (string.IsNullOrWhiteSpace(registeredType))
        {
            throw new ArgumentException("Scenario step runner registered type cannot be empty.", nameof(registeredType));
        }

        if (!_runners.TryGetValue(registeredType, out var runner))
        {
            throw new InvalidOperationException(
                $"Scenario step runner type '{registeredType}' must be registered before alias '{aliasType}'.");
        }

        _runners[aliasType] = runner;
        return this;
    }

    public bool TryGet(string type, out IScenarioStepRunner runner)
        => _runners.TryGetValue(type, out runner!);
}
