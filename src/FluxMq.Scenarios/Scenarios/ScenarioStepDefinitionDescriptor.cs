namespace FluxMq.Scenarios;

public sealed record ScenarioStepDefinitionDescriptor(
    string Type,
    string DisplayName,
    string Category,
    string Description,
    string NamePrefix,
    IReadOnlyList<ScenarioStepFieldDescriptor> Fields,
    string DefaultPhase = ScenarioPhaseKinds.Stimulus);
