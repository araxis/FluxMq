using FluxMq.Pipeline.Components;

namespace FluxMq.Pipeline.Scenarios;

public sealed record ScenarioEventMatch(FlowEvent Event, int Index);
