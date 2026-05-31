using FluxFlow.Engine.Components;

namespace FluxMq.Scenarios;

public interface IScenarioEventObserver
{
    void Observe(FlowEvent flowEvent);
}
