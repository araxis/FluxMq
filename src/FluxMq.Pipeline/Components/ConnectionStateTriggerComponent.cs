using FluxMq.Core.Ids;
using FluxMq.Core.Session;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Pipeline.Components;

public sealed class ConnectionStateTriggerComponent : IFlowNode, IDisposable
{
    private readonly IMqttConnectionManager _connectionManager;
    private readonly BroadcastBlock<SessionStateChangedEventArgs> _output;

    public ConnectionStateTriggerComponent(IMqttConnectionManager connectionManager, FlowNodeId? id = null)
    {
        Id = id ?? FlowNodeId.New();
        _connectionManager = connectionManager;
        _output = new BroadcastBlock<SessionStateChangedEventArgs>(static state => state);
        _connectionManager.StateChanged += OnStateChanged;
    }

    public FlowNodeId Id { get; }
    public Task Completion => _output.Completion;
    public ISourceBlock<SessionStateChangedEventArgs> Output => _output;

    private void OnStateChanged(object? sender, SessionStateChangedEventArgs e)
    {
        _output.Post(e);
    }

    public void Complete()
    {
        _connectionManager.StateChanged -= OnStateChanged;
        _output.Complete();
    }

    public void Fault(Exception exception)
    {
        _connectionManager.StateChanged -= OnStateChanged;
        ((IDataflowBlock)_output).Fault(exception);
    }

    public void Dispose() => Complete();
}
