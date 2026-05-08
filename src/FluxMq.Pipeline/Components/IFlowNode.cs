using FluxMq.Core.Ids;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Pipeline.Components;

public interface IFlowNode : IDataflowBlock
{
    FlowNodeId Id { get; }
}
