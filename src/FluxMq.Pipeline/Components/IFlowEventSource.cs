using System.Threading.Tasks.Dataflow;

namespace FluxMq.Pipeline.Components;

public interface IFlowEventSource
{
    ISourceBlock<FlowEvent> Events { get; }
}
