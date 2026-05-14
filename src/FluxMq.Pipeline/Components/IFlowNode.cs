using FluxMq.Core.Ids;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Pipeline.Components;

public interface IFlowNode : IDataflowBlock
{
    FlowNodeId Id { get; }
    ISourceBlock<FlowError> Errors { get; }
    Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
