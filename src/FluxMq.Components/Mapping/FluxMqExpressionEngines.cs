using FluxFlow.Engine.Mapping;

namespace FluxMq.Components.Mapping;

public static class FluxMqExpressionEngines
{
    public static IFlowExpressionEngine CreateDefault()
        => new FluxMqDynamicExpressionEngine();

    public static IFlowExpressionEngine CreateJsonata()
        => new FluxMqJsonataExpressionEngine();
}
