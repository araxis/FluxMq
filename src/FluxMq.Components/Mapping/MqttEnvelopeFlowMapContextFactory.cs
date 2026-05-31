using FluxMq.Core.Models;
using FluxFlow.Engine.Mapping;

namespace FluxMq.Components.Mapping;

public sealed class MqttEnvelopeFlowMapContextFactory : IFlowMapContextFactory<MqttEnvelope>
{
    public FlowMapContext Create(MqttEnvelope input)
        => MqttEnvelopeExpressionContextFactory.Create(input);
}
