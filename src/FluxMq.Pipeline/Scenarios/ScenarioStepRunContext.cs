using FluxMq.Pipeline.Definitions;

namespace FluxMq.Pipeline.Scenarios;

public sealed record ScenarioStepRunContext
{
    public required string ScenarioName { get; init; }
    public required string StepName { get; init; }
    public required ScenarioStepDefinition Step { get; init; }
    public required ScenarioEventJournal Events { get; init; }
    public ScenarioStepServices Services { get; init; } = ScenarioStepServices.Empty;
    public int EventOffset { get; init; }
}
