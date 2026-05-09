using FluxMq.Pipeline.Definitions;

namespace FluxMq.Pipeline.Runtime;

public delegate FlowRuntimeNode FlowRuntimeNodeFactory(FlowNodeName name, FlowNodeDefinition definition);
