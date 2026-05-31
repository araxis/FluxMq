using FluxFlow.Engine.Components;

namespace FluxMq.Pipeline.Scenarios;

public interface IScenarioEventObserver
{
    void Observe(FlowEvent flowEvent);
}
